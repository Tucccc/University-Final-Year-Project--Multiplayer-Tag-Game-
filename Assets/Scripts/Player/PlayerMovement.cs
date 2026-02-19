using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private Transform cameraTransform;

    [Header("Mouse Pivots")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform cameraPivot;

    [Header("Audio")]
    [SerializeField] private float walkStepInterval = 0.5f;

    [Header("Camera Feel")]
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobFrequency = 10f;
    [SerializeField] private float bobSmoothing = 10f;
    [SerializeField] private float turnSwayAmount = 2.0f;
    [SerializeField] private float turnSwaySmoothing = 12f;


    private Vector3 camBaseLocalPos;
    private float bobTime;
    private float targetRoll;
    private float stepTimer = 0f;
    private Vector3 lastPos;
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;
    private bool moving;

    private bool _lastControlsEnabled;
    private float _mouseX, _mouseY;

    private float yaw;
    private float pitch;
    private float rollVel;
    private float currentRoll;

    private void LateUpdate()
    {
        if (!IsOwner) return;

        bool gameplayActive = RoundOverUI.ControlsEnabled && !LocalPauseMenu.IsPauseOpen;
        if (!gameplayActive) return;

        ApplyLook();
        HeadBobbing();
    }

    private void ApplyLook()
    {
        float mx = _mouseX * mouseSensitivity;
        float my = _mouseY * mouseSensitivity;

        _mouseX = 0f;
        _mouseY = 0f;

        // Accumulate
        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        // Yaw: rotate the yaw pivot (NOT the network root)
        if (yawPivot != null)
            yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // Roll (sway)
        float targetRollLocal = Mathf.Clamp(-mx * turnSwayAmount, -turnSwayAmount, turnSwayAmount);

        // Use a real smoothTime (seconds). Much nicer than 1f/smoothing.
        float smoothTime = 0.08f;
        currentRoll = Mathf.SmoothDamp(currentRoll, targetRollLocal, ref rollVel, smoothTime);

        // Pitch + Roll: rotate camera pivot only (owner-only)
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, currentRoll);
    }



    public override void OnStartClient()
    {
        base.OnStartClient();

        controller = GetComponent<CharacterController>();
        lastPos = transform.position;

        if (cameraPivot == null)
            Debug.LogError("CameraPivot not assigned on PlayerMovement.");
        else
            camBaseLocalPos = cameraPivot.localPosition;

        if (IsOwner)
        {
            if (cameraTransform) cameraTransform.gameObject.SetActive(true);
            SetCursorForGameplay(RoundOverUI.ControlsEnabled && !LocalPauseMenu.IsPauseOpen);
        }
        else
        {
            if (cameraTransform) cameraTransform.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        bool gameplayActive = RoundOverUI.ControlsEnabled && !LocalPauseMenu.IsPauseOpen;
        SetCursorForGameplay(gameplayActive);

        if (!gameplayActive)
        {
            _mouseX = 0f;
            _mouseY = 0f;
            return;
        }

        HandleMovement();
        ReadMouse();
    }

    private void SetCursorForGameplay(bool gameplayActive)
    {
        Cursor.lockState = gameplayActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplayActive;
    }

    private void ReadMouse()
    {
        _mouseX += Input.GetAxisRaw("Mouse X");
        _mouseY += Input.GetAxisRaw("Mouse Y");
    }


    private void HandleMovement()
    {
        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // Movement input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Transform basis = (yawPivot != null) ? yawPivot : transform;
        Vector3 move = basis.right * moveX + basis.forward * moveZ;
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Track movement for footsteps
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        delta.y = 0f;
        moving = delta.sqrMagnitude > 0.00001f;
    }

    private void HeadBobbing()
    {
        // Camera bobbing
        bool doBob = isGrounded && moving;
        if (doBob)
        {
            bobTime += Time.deltaTime * bobFrequency;
            float x = Mathf.Sin(bobTime) * bobAmount * 0.5f;
            float y = Mathf.Abs(Mathf.Cos(bobTime)) * bobAmount;
            Vector3 bobOffset = new Vector3(x, y, 0f);
            Vector3 targetPos = camBaseLocalPos + bobOffset;
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, targetPos, bobSmoothing * Time.deltaTime);
        }
        else
        {
            bobTime = 0f;
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, camBaseLocalPos, bobSmoothing * Time.deltaTime);
        }

        //// Footstep audio (client-side only for owner)
        //if (!isGrounded || !moving)
        //{
        //    stepTimer = walkStepInterval * 0.5f;
        //    return;
        //}

        //stepTimer += Time.deltaTime;
        //if (stepTimer >= walkStepInterval)
        //{
        //    stepTimer = 0f;
        //    string stepName = (Random.value < 0.5f) ? "Step1" : "Step2";
        //    if (AudioManager.instance != null)
        //        AudioManager.instance.Play(stepName);
        //}
    }

}
