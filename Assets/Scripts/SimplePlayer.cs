using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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

    [Header("Look")]
    public float mouseSensitivity = 0.1f;

    [Header("Respawn")]
    public float respawnBelowY = -6f; // fall past this (into the pit) -> back to spawn

    [HideInInspector] public bool canMove = true; // look still works when false (elevator intro)

    CharacterController cc;
    Transform cam;
    Vector3 spawnPos;
    float pitch;
    float vSpeed;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        spawnPos = transform.position;

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Look();
        Move();

        if (transform.position.y < respawnBelowY) Respawn();
    }

    void Respawn()
    {
        vSpeed = 0f;
        cc.enabled = false;             // CharacterController fights direct moves
        transform.position = spawnPos;
        cc.enabled = true;
    }

    void Look()
    {
        if (Mouse.current == null) return;
        Vector2 d = Mouse.current.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up, d.x);          // yaw on body
        pitch = Mathf.Clamp(pitch - d.y, -90f, 90f); // pitch on camera
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    void Move()
    {
        var k = Keyboard.current;
        if (k == null || !canMove) return;

        float h = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float v = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        Vector3 move = (transform.right * h + transform.forward * v).normalized * moveSpeed;

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
}
