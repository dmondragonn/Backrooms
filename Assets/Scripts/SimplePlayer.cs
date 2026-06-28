using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Self-contained first-person controller. Put it on a Capsule, press Play, walk.
// CharacterController = no Rigidbody, no ground LayerMask, no orientation rig.
// Creates its own camera if the capsule has none.
[RequireComponent(typeof(CharacterController))]
public class SimplePlayer : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 4f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Sprint")]
    public float sprintMultiplier = 1.6f;
    public float sprintDuration = 1.5f;
    public float tiredDuration = 2f;
    public float doubleTapWindow = 0.3f;
    public float staminaRegenPerSecond = 0.9f;

    [Header("Look")]
    public float mouseSensitivity = 0.1f;

    [Header("Camera Motion")]
    public float cameraBobAmount = 0.035f;
    public float cameraBobSpeed = 11f;
    public float cameraSwayAmount = 0.025f;
    public float cameraSwaySpeed = 2.2f;
    public float cameraTiltAmount = 1.5f;
    public float cameraTiltSpeed = 6f;
    public float cameraMotionResponse = 10f;
    public float walkCameraIntensity = 0.45f;
    public float sprintCameraIntensity = 1f;

    [Header("Stamina UI")]
    public bool showStaminaBar = true;
    public Vector2 staminaBarSize = new Vector2(220f, 16f);
    public Vector2 staminaBarOffset = new Vector2(24f, 24f);
    public Color staminaBarFillColor = new Color(0.85f, 0.93f, 0.95f, 0.95f);

    [Header("Respawn")]
    public float respawnBelowY = -6f; // fall past this (into the pit) -> back to spawn

    [HideInInspector] public bool canMove = true; // look still works when false (elevator intro)

    CharacterController cc;
    Transform cam;
    Vector3 camBaseLocalPos;
    Vector3 spawnPos;
    float pitch;
    float yaw;
    float vSpeed;
    float cameraBobTime;
    float currentMoveAmount;
    float smoothedMoveAmount;
    float smoothedMoveAmountVelocity;
    float currentStamina;
    Image staminaFillImage;
    bool isSprinting;
    bool isTired;
    float sprintTimer;
    float tiredTimer;
    float lastWPressTime = -10f;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        spawnPos = transform.position;
        currentStamina = sprintDuration;

        cam = GetComponentInChildren<Camera>()?.transform;
        if (cam == null)   // make one at eye height if the capsule has no camera
        {
            var go = new GameObject("PlayerCamera");
            var camComp = go.AddComponent<Camera>();
            camComp.clearFlags = CameraClearFlags.SolidColor; // black exterior, no skybox leak
            camComp.backgroundColor = Color.black;
            camComp.farClipPlane = 42f;                       // cull distant maze (fog hides the edge)
            var camData = camComp.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;              // enable the global Volume effects
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            go.AddComponent<AudioListener>();
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0, 0.6f, 0);
            cam = go.transform;
        }

        camBaseLocalPos = cam.localPosition;
        if (showStaminaBar)
        {
            CreateStaminaUI();
            UpdateStaminaUI();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleSprintState();
        Look();
        HandleWorldClicks();
        Move();
        UpdateCameraMotion();
        UpdateStaminaUI();

        if (transform.position.y < respawnBelowY) Respawn();
    }

    void Respawn()
    {
        vSpeed = 0f;
        currentStamina = sprintDuration;
        currentMoveAmount = 0f;
        smoothedMoveAmount = 0f;
        smoothedMoveAmountVelocity = 0f;
        isSprinting = false;
        isTired = false;
        sprintTimer = 0f;
        tiredTimer = 0f;
        lastWPressTime = -10f;
        cc.enabled = false;             // CharacterController fights direct moves
        transform.position = spawnPos;
        cc.enabled = true;
    }

    void Look()
    {
        if (Mouse.current == null) return;
        Vector2 d = Mouse.current.delta.ReadValue() * mouseSensitivity;

        yaw += d.x;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);          // yaw on body
        pitch = Mathf.Clamp(pitch - d.y, -90f, 90f); // pitch on camera
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    void HandleWorldClicks()
    {
        if (Mouse.current == null || cam == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 5f))
        {
            var elevatorButton = hit.collider.GetComponentInParent<ElevatorButton>();
            if (elevatorButton != null)
            {
                elevatorButton.Press();
            }
        }
    }

    void Move()
    {
        var k = Keyboard.current;
        if (k == null || !canMove) return;

        if (k.wKey.wasPressedThisFrame)
        {
            float timeSinceLastPress = Time.time - lastWPressTime;

            if (cc.isGrounded && !isTired && currentStamina > 0.05f && timeSinceLastPress <= doubleTapWindow)
            {
                StartSprint();
            }

            lastWPressTime = Time.time;
        }

        float h = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float v = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        float currentMoveSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        currentMoveAmount = Mathf.Clamp01(new Vector2(h, v).magnitude);
        Vector3 move = (transform.right * h + transform.forward * v).normalized * currentMoveSpeed;

        if (cc.isGrounded)
        {
            vSpeed = -1f; // small stick-to-ground force
            if (k.spaceKey.wasPressedThisFrame)
                vSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        vSpeed += gravity * Time.deltaTime;

        move.y = vSpeed;
        cc.Move(move * Time.deltaTime);
    }

    void UpdateCameraMotion()
    {
        if (cam == null) return;

        float targetMotionAmount = (cc.isGrounded && canMove) ? currentMoveAmount : 0f;
        smoothedMoveAmount = Mathf.SmoothDamp(smoothedMoveAmount, targetMotionAmount, ref smoothedMoveAmountVelocity, 1f / cameraMotionResponse);
        bool moving = smoothedMoveAmount > 0.01f;
        float cameraIntensity = isSprinting ? sprintCameraIntensity : walkCameraIntensity;
        float motionAmount = smoothedMoveAmount * cameraIntensity;

        cameraBobTime += Time.deltaTime * (moving ? cameraBobSpeed * (isSprinting ? 1.25f : 1f) : cameraBobSpeed * 0.35f);

        float bobY = Mathf.Sin(cameraBobTime * 2f) * cameraBobAmount * motionAmount;
        float bobX = Mathf.Cos(cameraBobTime * 1.2f) * cameraBobAmount * 0.5f * motionAmount;

        float swayWeight = Mathf.Lerp(0.12f, 1f, motionAmount);
        float swayX = (Mathf.PerlinNoise(Time.time * cameraSwaySpeed, 0f) - 0.5f) * 2f * cameraSwayAmount * swayWeight;
        float swayY = (Mathf.PerlinNoise(0f, Time.time * cameraSwaySpeed) - 0.5f) * 2f * cameraSwayAmount * 0.65f * swayWeight;
        float tilt = (Mathf.PerlinNoise(Time.time * cameraTiltSpeed, 10f) - 0.5f) * 2f * cameraTiltAmount * motionAmount * (isSprinting ? 1.2f : 1f);

        cam.localPosition = camBaseLocalPos + new Vector3(bobX + swayX, bobY + swayY, 0f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, tilt);
    }

    void HandleSprintState()
    {
        if (isSprinting)
        {
            currentStamina -= Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isSprinting = false;
                isTired = true;
                tiredTimer = tiredDuration;
                lastWPressTime = -10f;
            }
        }
        else if (isTired)
        {
            tiredTimer -= Time.deltaTime;

            if (tiredTimer <= 0f)
            {
                isTired = false;
                lastWPressTime = -10f;
            }
        }
        else if (currentStamina < sprintDuration)
        {
            currentStamina = Mathf.MoveTowards(currentStamina, sprintDuration, staminaRegenPerSecond * Time.deltaTime);
        }
    }

    void StartSprint()
    {
        isSprinting = true;
        isTired = false;
        sprintTimer = sprintDuration;
        lastWPressTime = -10f;
    }

    void CreateStaminaUI()
    {
        var canvasObject = new GameObject("StaminaCanvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        var fillObject = new GameObject("StaminaFill");
        fillObject.transform.SetParent(canvasObject.transform, false);
        staminaFillImage = fillObject.AddComponent<Image>();
        var whiteTexture = Texture2D.whiteTexture;
        staminaFillImage.sprite = Sprite.Create(
            whiteTexture,
            new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        staminaFillImage.type = Image.Type.Filled;
        staminaFillImage.fillMethod = Image.FillMethod.Horizontal;
        staminaFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        staminaFillImage.color = staminaBarFillColor;

        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 0f);
        fillRect.pivot = new Vector2(0f, 0f);
        fillRect.anchoredPosition = staminaBarOffset;
        fillRect.sizeDelta = staminaBarSize;
    }

    void UpdateStaminaUI()
    {
        if (staminaFillImage == null) return;

        float normalizedStamina = sprintDuration > 0f ? currentStamina / sprintDuration : 0f;
        staminaFillImage.fillAmount = Mathf.Clamp01(normalizedStamina);
    }
}
