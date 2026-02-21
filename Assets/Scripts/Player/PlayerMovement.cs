using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1f;
    [SerializeField] private float sprintSpeed = 5f;
    [SerializeField] private float crouchSpeed = 0.6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Crouch")]
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float colliderLerpSpeed = 12f;
    [Tooltip("Extra headroom needed to stand up (prevents standing into ceilings).")]
    [SerializeField] private float standCheckPadding = 0.05f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 3f;

    [Header("Look Limits (Owner Only)")]
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Camera (Owner Only)")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Pivot used for pitch (up/down) + bob. Should be parent of the Camera.")]
    [SerializeField] private Transform cameraPivot;

    [Header("Head Follow (Owner Only)")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Vector3 headLocalOffset = new Vector3(0f, 0.08f, 0.02f);
    [Range(0f, 0.25f)]
    [SerializeField] private float headFollowSmoothing = 0.05f;

    [Header("Camera Anti-Clipping (Owner Only)")]
    [Tooltip("Push camera forward from head bone so you don't see inside your mesh when looking down.")]
    [SerializeField] private float cameraForwardOffset = 0.08f;
    [Tooltip("Optional small upward push.")]
    [SerializeField] private float cameraUpOffset = 0.02f;
    [Tooltip("Optional: lower near clip for FPS. 0.01-0.05 is common.")]
    [SerializeField] private float ownerNearClipPlane = 0.02f;

    [Header("Owner Self-View (Hide Head/Hair)")]
    [SerializeField] private bool hideHeadForOwner = true;

    [Tooltip("Bones to scale to zero for the owner (HeadMesh, Hair bones, Helmet bones, etc).\nDO NOT add headBone here.")]
    [SerializeField] private Transform[] hideBonesForOwner;

    [Tooltip("Optional renderers to disable for the owner (hair cards, helmet mesh, etc).")]
    [SerializeField] private Renderer[] hideRenderersForOwner;

    private Vector3[] _originalBoneScales;

    [Header("Camera Feel (Owner Only)")]
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobFrequency = 10f;
    [SerializeField] private float bobSmoothing = 10f;

    [Header("Camera Tilt (Owner Only)")]
    [SerializeField] private float turnSwayAmount = 2f;
    [SerializeField] private float turnSwaySmoothTime = 0.08f;
    private float roll;
    private float rollVel;

    [Header("Animations")]
    [Tooltip("Expected parameters: Horizontal(float), Vertical(float), IsSprinting(bool), IsCrouching(bool), IsGrounded(bool), Jump(trigger), Fall(bool)")]
    [SerializeField] private Animator animator;

    // Animator parameter names
    private const string P_H = "Horizontal";
    private const string P_V = "Vertical";
    private const string P_SPRINT = "IsSprinting";
    private const string P_CROUCH = "IsCrouching";
    private const string P_GROUNDED = "IsGrounded";
    private const string P_JUMP = "Jump";
    private const string P_FALL = "Fall";

    private CharacterController controller;

    private Vector3 velocity;
    private bool isGrounded;
    private bool moving;

    private float mouseX, mouseY;
    private float yaw;
    private float pitch;

    // Camera bob
    private Vector3 camBaseLocalPos;
    private float bobTime;

    // Head follow smoothing
    private Vector3 camFollowVel;

    // Crouch state
    private bool isCrouching;
    private float targetControllerHeight;
    private float defaultControllerHeight;
    private Vector3 defaultControllerCenter;

    private bool loggedAnimator;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (cameraPivot != null)
            camBaseLocalPos = cameraPivot.localPosition;

        defaultControllerHeight = controller.height;
        defaultControllerCenter = controller.center;

        targetControllerHeight = Mathf.Max(0.1f, standHeight > 0f ? standHeight : defaultControllerHeight);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(IsOwner);

        if (!IsOwner) return;

        // Optional: near clip tweak for owner FPS camera
        if (cameraTransform != null)
        {
            var cam = cameraTransform.GetComponent<Camera>();
            if (cam != null && ownerNearClipPlane > 0f)
                cam.nearClipPlane = ownerNearClipPlane;
        }

        // Hide head/hair for owner WITHOUT breaking camera follow
        ApplyOwnerHide();

        if (cameraPivot == null)
            Debug.LogError("CameraPivot not assigned on PlayerMovement.");
        else
            camBaseLocalPos = cameraPivot.localPosition;

        UpdateCursorState();
    }

    private void OnDestroy()
    {
        if (IsOwner)
            RestoreOwnerHide();
    }

    private void Update()
    {
        if (!IsOwner) return;

        bool roundAllowsControls = RoundOverUI.ControlsEnabled;
        bool paused = LocalPauseMenu.IsPauseOpen;

        UpdateCursorState();

        if (!loggedAnimator)
        {
            Debug.Log($"[PlayerMovement] Using Animator: {(animator ? animator.name : "NULL")}", this);
            loggedAnimator = true;
        }

        float inputX = 0f;
        float inputZ = 0f;
        bool tryingSprint = false;
        bool wantsCrouch = false;
        bool wantsJump = false;

        if (roundAllowsControls && !paused)
        {
            inputX = Input.GetAxisRaw("Horizontal");
            inputZ = Input.GetAxisRaw("Vertical");
            tryingSprint = Input.GetKey(KeyCode.LeftShift);
            wantsCrouch = Input.GetKey(crouchKey);
            wantsJump = Input.GetButtonDown("Jump");
            ReadMouse();
        }
        else
        {
            mouseX = 0f;
            mouseY = 0f;
        }

        UpdateCrouchState(wantsCrouch);

        bool sprint = tryingSprint && !isCrouching;         // affects SPEED
        bool sprintAnim = sprint && inputZ >= -0.1f;        // affects ANIM ONLY

        float speedToUse = isCrouching ? crouchSpeed : (sprint ? sprintSpeed : walkSpeed);

        if (roundAllowsControls)
        {
            HandleMovement(inputX, inputZ, speedToUse, allowJump: !isCrouching, wantsJump: wantsJump);
        }

        if (!roundAllowsControls || paused)
            SetLocomotionParams(0f, 0f, false, isCrouching);
        else
            SetLocomotionParams(inputX, inputZ, sprintAnim, isCrouching);

        bool falling = !isGrounded && velocity.y < -0.1f;
        SetGroundParams(isGrounded, falling);

        ApplyCrouchCollider();
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        bool lookAllowed = RoundOverUI.ControlsEnabled && !LocalPauseMenu.IsPauseOpen;
        if (!lookAllowed) return;

        ApplyLook();
        FollowHead();
        HeadBobbing();
    }

    private void UpdateCursorState()
    {
        bool gameplayActive = RoundOverUI.ControlsEnabled && !LocalPauseMenu.IsPauseOpen;
        Cursor.lockState = gameplayActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplayActive;
    }

    private void ReadMouse()
    {
        mouseX += Input.GetAxisRaw("Mouse X");
        mouseY += Input.GetAxisRaw("Mouse Y");
    }

    private void ApplyLook()
    {
        float mx = mouseX * mouseSensitivity;
        float my = mouseY * mouseSensitivity;

        mouseX = 0f;
        mouseY = 0f;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        float targetRoll = Mathf.Clamp(-mx * turnSwayAmount, -turnSwayAmount, turnSwayAmount);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVel, turnSwaySmoothTime);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    private void FollowHead()
    {
        if (cameraPivot == null || headBone == null) return;

        // IMPORTANT: keep TransformPoint so camera follows lean/animation exactly how you liked
        Vector3 targetWorldPos = headBone.TransformPoint(headLocalOffset);

        targetWorldPos += headBone.forward * cameraForwardOffset;
        targetWorldPos += headBone.up * cameraUpOffset;

        if (headFollowSmoothing <= 0f)
        {
            cameraPivot.position = targetWorldPos;
        }
        else
        {
            cameraPivot.position = Vector3.SmoothDamp(
                cameraPivot.position,
                targetWorldPos,
                ref camFollowVel,
                headFollowSmoothing
            );
        }
    }

    private void HandleMovement(float inputX, float inputZ, float speedToUse, bool allowJump, bool wantsJump)
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        Vector3 moveDir = transform.right * inputX + transform.forward * inputZ;
        moveDir.y = 0f;

        controller.Move(moveDir * speedToUse * Time.deltaTime);

        moving = (Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputZ) > 0.01f);

        if (allowJump && wantsJump && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (animator != null)
                animator.SetTrigger(P_JUMP);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        isGrounded = controller.isGrounded;
    }

    private void SetLocomotionParams(float h, float v, bool sprint, bool crouch)
    {
        if (animator == null) return;

        h = Mathf.Clamp(h, -1f, 1f);
        v = Mathf.Clamp(v, -1f, 1f);

        animator.SetFloat(P_H, h, 0.08f, Time.deltaTime);
        animator.SetFloat(P_V, v, 0.08f, Time.deltaTime);
        animator.SetBool(P_SPRINT, sprint);
        animator.SetBool(P_CROUCH, crouch);
    }

    private void SetGroundParams(bool grounded, bool falling)
    {
        if (animator == null) return;

        animator.SetBool(P_GROUNDED, grounded);
        animator.SetBool(P_FALL, falling);
    }

    private void HeadBobbing()
    {
        if (cameraPivot == null) return;

        bool doBob = isGrounded && moving && !isCrouching;

        if (doBob)
        {
            bobTime += Time.deltaTime * bobFrequency;
            float x = Mathf.Sin(bobTime) * bobAmount * 0.5f;
            float y = Mathf.Abs(Mathf.Cos(bobTime)) * bobAmount;

            Vector3 targetPos = camBaseLocalPos + new Vector3(x, y, 0f);
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, targetPos, bobSmoothing * Time.deltaTime);
        }
        else
        {
            bobTime = 0f;
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, camBaseLocalPos, bobSmoothing * Time.deltaTime);
        }
    }

    // --------- Owner hide helpers ---------

    private void ApplyOwnerHide()
    {
        if (!hideHeadForOwner) return;

        // Cache original scales once
        if (hideBonesForOwner != null && hideBonesForOwner.Length > 0 && _originalBoneScales == null)
        {
            _originalBoneScales = new Vector3[hideBonesForOwner.Length];
            for (int i = 0; i < hideBonesForOwner.Length; i++)
                _originalBoneScales[i] = hideBonesForOwner[i] ? hideBonesForOwner[i].localScale : Vector3.one;
        }

        // Scale bones to zero for owner (BUT NEVER SCALE headBone - camera anchor)
        if (hideBonesForOwner != null)
        {
            foreach (var b in hideBonesForOwner)
            {
                if (!b) continue;
                if (b == headBone) continue; // <-- critical: keep camera follow working

                b.localScale = Vector3.zero;
            }
        }

        // Disable renderers (hair cards / helmets etc.)
        if (hideRenderersForOwner != null)
        {
            foreach (var r in hideRenderersForOwner)
                if (r) r.enabled = false;
        }
    }

    private void RestoreOwnerHide()
    {
        if (_originalBoneScales != null && hideBonesForOwner != null)
        {
            for (int i = 0; i < hideBonesForOwner.Length; i++)
            {
                if (!hideBonesForOwner[i]) continue;
                if (hideBonesForOwner[i] == headBone) continue; // keep anchor unchanged

                hideBonesForOwner[i].localScale = _originalBoneScales[i];
            }
        }

        if (hideRenderersForOwner != null)
        {
            foreach (var r in hideRenderersForOwner)
                if (r) r.enabled = true;
        }
    }

    // --------- Crouch helpers ---------

    private void UpdateCrouchState(bool wantsCrouch)
    {
        if (wantsCrouch)
        {
            isCrouching = true;
            targetControllerHeight = crouchHeight;
        }
        else
        {
            if (CanStandUp())
            {
                isCrouching = false;
                targetControllerHeight = standHeight;
            }
            else
            {
                isCrouching = true;
                targetControllerHeight = crouchHeight;
            }
        }
    }

    private bool CanStandUp()
    {
        if (controller.height >= standHeight - 0.01f) return true;

        float current = controller.height;
        float neededExtra = Mathf.Max(0f, standHeight - current);

        Vector3 origin = transform.position + controller.center;
        float radius = Mathf.Max(0.01f, controller.radius - 0.01f);

        return !Physics.SphereCast(origin, radius, Vector3.up, out _, neededExtra + standCheckPadding,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
    }

    private void ApplyCrouchCollider()
    {
        float newHeight = Mathf.Lerp(controller.height, targetControllerHeight, colliderLerpSpeed * Time.deltaTime);
        float heightDelta = newHeight - controller.height;

        controller.height = newHeight;
        controller.center += new Vector3(0f, heightDelta * 0.5f, 0f);

        if (Mathf.Abs(controller.center.y - defaultControllerCenter.y) > 1.5f)
            controller.center = defaultControllerCenter;
    }
}