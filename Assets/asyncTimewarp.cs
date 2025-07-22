using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class asyncTimewarp : MonoBehaviour
{
    public GameObject projectorSphere;
    public Projector projector;
    public Camera projectorCamera;
    public Camera cam;

    public RenderTexture targetTexture;
    public RenderTexture targetTextureDepth;

    private Vector2 resolution;

    void Start()
    {
        resolution = new Vector2(Screen.width, Screen.height);
        targetTexture = makeBackbufferTexture(Screen.width, Screen.height);
        targetTextureDepth = makeDepthTexture(Screen.width, Screen.height);
        cam.SetTargetBuffers(targetTexture.colorBuffer, targetTextureDepth.depthBuffer);
    }

    RenderTexture makeBackbufferTexture(int width, int height)
    {
        return new RenderTexture(width, height, 0, RenderTextureFormat.Default);
    }

    RenderTexture makeDepthTexture(int width, int height)
    {
        return new RenderTexture(width, height, 24, RenderTextureFormat.Depth);
    }

    void setCamera()
    {
        DrawTextureMat.SetFloat("_NearClip", cam.nearClipPlane);
        DrawTextureMat.SetFloat("_FarClip", cam.farClipPlane);
        DrawTextureMat.SetVector("_CameraPos", cam.transform.position);
        DrawTextureMat.SetVector("_CameraForward", cam.transform.forward);
        DrawTextureMat.SetMatrix("_WorldToCameraMatrix", cam.worldToCameraMatrix);
        DrawTextureMat.SetMatrix("_ProjectionMatrix", cam.projectionMatrix);

        DrawTextureMat.SetVector("_TopLeft", cam.ViewportPointToRay(new Vector3(0, 1)).direction);
        DrawTextureMat.SetVector("_TopRight", cam.ViewportPointToRay(new Vector3(1, 1)).direction);
        DrawTextureMat.SetVector("_BottomLeft", cam.ViewportPointToRay(new Vector3(0, 0)).direction);
        DrawTextureMat.SetVector("_BottomRight", cam.ViewportPointToRay(new Vector3(1, 0)).direction);
    }

    void setFrozenCamera()
    {
        DrawTextureMat.SetFloat("_NearClip", cam.nearClipPlane);
        DrawTextureMat.SetFloat("_FarClip", cam.farClipPlane);
        DrawTextureMat.SetVector("_FrozenCameraPos", cam.transform.position);
        DrawTextureMat.SetVector("_FrozenCameraForward", cam.transform.forward);
        DrawTextureMat.SetMatrix("_FrozenWorldToCameraMatrix", cam.worldToCameraMatrix);
        DrawTextureMat.SetMatrix("_FrozenProjectionMatrix", cam.projectionMatrix);

        DrawTextureMat.SetVector("_FrozenTopLeft", cam.ViewportPointToRay(new Vector3(0, 1)).direction);
        DrawTextureMat.SetVector("_FrozenTopRight", cam.ViewportPointToRay(new Vector3(1, 1)).direction);
        DrawTextureMat.SetVector("_FrozenBottomLeft", cam.ViewportPointToRay(new Vector3(0, 0)).direction);
        DrawTextureMat.SetVector("_FrozenBottomRight", cam.ViewportPointToRay(new Vector3(1, 0)).direction);
    }

    bool ReprojectMovement = false;
    float accumulatedX = 0;
    float accumulatedY = 0;
    float accumulatedDT;
    float finalDT;
    int SlowFPS = 30;
    float lagIntensity = 0.5f;
    float renderScale = 1.0f; // New: scale factor for Option 7
    float t = 0.0f;

    void Update()
    {
        DrawTextureMat.SetFloat("_StretchBorders", StretchBorders ? 1.0f : 0.0f);
        DrawTextureMat.SetFloat("_ReprojectMovement", ReprojectMovement ? 1.0f : 0.0f);

        if (resolution.x != Screen.width || resolution.y != Screen.height)
        {
            targetTexture = makeBackbufferTexture(Screen.width, Screen.height);
            targetTextureDepth = makeDepthTexture(Screen.width, Screen.height);
            cam.SetTargetBuffers(targetTexture.colorBuffer, targetTextureDepth.depthBuffer);
            resolution.x = Screen.width;
            resolution.y = Screen.height;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Mode switching
        if (Input.GetKeyDown(KeyCode.Alpha1)) state = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) state = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) state = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) state = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) state = 5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) state = 6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) state = 7; // NEW: GPU-bound supersample

        cam.depthTextureMode = DepthTextureMode.Depth;
        projectorCamera.transform.rotation = cam.transform.rotation;
        projectorCamera.fieldOfView = cam.fieldOfView;

        DrawTextureMat.SetTexture("_ColorTex", targetTexture);
        DrawTextureMat.SetTexture("_DepthTex", targetTextureDepth);

        float MouseX = Input.GetAxis("Mouse X");
        float MouseY = -Input.GetAxis("Mouse Y");

        float MovementX = 0, MovementY = 0;
        if (Input.GetKey(KeyCode.A)) MovementX--;
        if (Input.GetKey(KeyCode.D)) MovementX++;
        if (Input.GetKey(KeyCode.W)) MovementY++;
        if (Input.GetKey(KeyCode.S)) MovementY--;

        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;

        accumulatedDT += Time.deltaTime;
        accumulatedX += MouseX;
        accumulatedY += MouseY;

        // Handle modes
        if (state == 0) normalRender(MouseX, MouseY, MovementX, MovementY);
        else if (state == 1) freezeAndWarp(MouseX, MouseY, MovementX, MovementY);
        else if (state == 2) slowRenderFixed(MouseX, MouseY, MovementX, MovementY);
        else if (state == 3) slowRenderWarp(MouseX, MouseY, MovementX, MovementY);
        else if (state == 5) lagSimNoWarp(MouseX, MouseY, MovementX, MovementY);
        else if (state == 6) lagSimWithWarp(MouseX, MouseY, MovementX, MovementY);
        else if (state == 7) renderSupersample(MouseX, MouseY, MovementX, MovementY);

        if (Input.GetKeyDown(KeyCode.Q)) drawCustom = !drawCustom;
    }

    void normalRender(float MouseX, float MouseY, float MX, float MY)
    {
        t = 0;
        setCamera();
        setFrozenCamera();
        movePlayer(MouseX, MouseY, MX, MY);
        this.GetComponent<Camera>().Render();
        resetAccumulation();
        finalDT = Time.deltaTime;
    }

    void freezeAndWarp(float MouseX, float MouseY, float MX, float MY)
    {
        t = 0;
        setCamera();
        movePlayer(MouseX, MouseY, MX, MY);
        resetAccumulation();
        finalDT = 1;
    }

    void slowRenderFixed(float MouseX, float MouseY, float MX, float MY)
    {
        float DeltaTimeTarget = 1.0f / SlowFPS;
        t += Time.deltaTime;
        setCamera();
        setFrozenCamera();
        if (t > DeltaTimeTarget)
        {
            t -= DeltaTimeTarget;
            movePlayer(accumulatedX, accumulatedY, MX, MY, accumulatedDT);
            this.GetComponent<Camera>().Render();
            finalDT = accumulatedDT;
            resetAccumulation();
        }
    }

    void slowRenderWarp(float MouseX, float MouseY, float MX, float MY)
    {
        float DeltaTimeTarget = 1.0f / SlowFPS;
        t += Time.deltaTime;
        setCamera();
        movePlayer(MouseX, MouseY, MX, MY);
        if (t > DeltaTimeTarget)
        {
            t -= DeltaTimeTarget;
            setFrozenCamera();
            this.GetComponent<Camera>().Render();
            finalDT = accumulatedDT;
            resetAccumulation();
        }
    }

    void lagSimNoWarp(float MouseX, float MouseY, float MX, float MY)
    {
        float fluctuation = UnityEngine.Random.Range(1.0f - lagIntensity, 1.0f + lagIntensity);
        float dynamicFPS = Mathf.Max(5, SlowFPS * fluctuation);
        float DeltaTimeTarget = 1.0f / dynamicFPS;

        t += Time.deltaTime;
        setCamera();
        setFrozenCamera();
        movePlayer(MouseX, MouseY, MX, MY);

        if (t > DeltaTimeTarget)
        {
            t -= DeltaTimeTarget;
            this.GetComponent<Camera>().Render();
            finalDT = accumulatedDT;
            resetAccumulation();
        }
    }

    void lagSimWithWarp(float MouseX, float MouseY, float MX, float MY)
    {
        float fluctuation = UnityEngine.Random.Range(1.0f - lagIntensity, 1.0f + lagIntensity);
        float dynamicFPS = Mathf.Max(5, SlowFPS * fluctuation);
        float DeltaTimeTarget = 1.0f / dynamicFPS;

        t += Time.deltaTime;
        setCamera();
        movePlayer(MouseX, MouseY, MX, MY);

        if (t > DeltaTimeTarget)
        {
            t -= DeltaTimeTarget;
            setFrozenCamera();
            this.GetComponent<Camera>().Render();
            finalDT = accumulatedDT;
            resetAccumulation();
        }
    }

    // Option 7: GPU-bound supersample rendering
    void renderSupersample(float MouseX, float MouseY, float MX, float MY)
    {
        int width = Mathf.RoundToInt(Screen.width * renderScale);
        int height = Mathf.RoundToInt(Screen.height * renderScale);

        // Clamp to avoid driver crashes
        width = Mathf.Clamp(width, 1, 16384);
        height = Mathf.Clamp(height, 1, 16384);

        if (targetTexture == null || targetTexture.width != width || targetTexture.height != height)
        {
            targetTexture = makeBackbufferTexture(width, height);
            targetTextureDepth = makeDepthTexture(width, height);
        }

        movePlayer(MouseX, MouseY, MX, MY);
        setCamera();
        setFrozenCamera();

        cam.SetTargetBuffers(targetTexture.colorBuffer, targetTextureDepth.depthBuffer);
        cam.Render();

        Graphics.Blit(targetTexture, (RenderTexture)null);
        finalDT = Time.deltaTime;
        resetAccumulation();
    }

    void movePlayer(float MouseX, float MouseY, float MX, float MY, float scale = -1)
    {
        if (scale < 0) scale = Time.deltaTime;
        if (!Cursor.visible)
        {
            this.transform.parent.transform.Rotate(Vector3.up, MouseX * sensitivity);
            this.transform.Rotate(Vector3.right, MouseY * sensitivity, Space.Self);
            this.transform.parent.Translate(this.transform.parent.forward * MY * scale * 5.0f, Space.World);
            this.transform.parent.Translate(this.transform.parent.right * MX * scale * 5.0f, Space.World);
        }
    }

    void resetAccumulation()
    {
        accumulatedDT = 0;
        accumulatedX = 0;
        accumulatedY = 0;
    }

    bool drawCustom = false;
    int state = 0;
    float sensitivity = 4.0f;
    bool StretchBorders = false;

    public Material DrawTextureMat;
    public Texture testTexture;

    void OnGUI()
    {
        using (var horizontalScope = new GUILayout.VerticalScope("box"))
        {
            GUIStyle boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUIStyle style = new GUIStyle(GUI.skin.label);

            GUILayout.Label("Esc to unlock mouse");
            GUILayout.Label("1 = Render uncapped fps", (state == 0) ? boldStyle : style);
            GUILayout.Label("2 = Freeze render & warp", (state == 1) ? boldStyle : style);
            GUILayout.Label("3 = " + SlowFPS + " fps fixed", (state == 2) ? boldStyle : style);
            GUILayout.Label("4 = " + SlowFPS + " fps + timewarp", (state == 3) ? boldStyle : style);
            GUILayout.Label("5 = Lag simulation (no warp)", (state == 5) ? boldStyle : style);
            GUILayout.Label("6 = Lag simulation (with timewarp)", (state == 6) ? boldStyle : style);
            GUILayout.Label("7 = GPU-bound supersample mode", (state == 7) ? boldStyle : style);

            StretchBorders = GUILayout.Toggle(StretchBorders, "Stretch Timewarp borders");
            ReprojectMovement = GUILayout.Toggle(ReprojectMovement, "Include player movement in reprojection");

            GUILayout.Label("Frame Time: " + Math.Round(finalDT * 1000.0f) + "ms " + Math.Round(1.0f / finalDT) + "fps");

            GUILayout.Label("Target FPS: " + SlowFPS);
            SlowFPS = (int)GUILayout.HorizontalSlider(SlowFPS, 2, 200);

            GUILayout.Label("Lag Intensity: " + Math.Round(lagIntensity * 100) + "%");
            lagIntensity = GUILayout.HorizontalSlider(lagIntensity, 0.0f, 1.0f);

            GUILayout.Label("Render Scale (1x–25 x): " + renderScale.ToString("F1") + "x");
            renderScale = GUILayout.HorizontalSlider(renderScale, 1.0f, 25.0f); // This is the range of render scale

            GUILayout.Label("Mouse Sensitivity: " + sensitivity);
            sensitivity = GUILayout.HorizontalSlider(sensitivity, 0, 10.0f);

            if (Cursor.visible && GUILayout.Button("Click to control player"))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
