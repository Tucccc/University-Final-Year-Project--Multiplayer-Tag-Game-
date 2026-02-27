using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Controls")]
    private KeyCode altLookKeyRuntime;
    [SerializeField] private float mouseSensitivity = 3f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1f;
    [SerializeField] private float sprintSpeed = 5f;
    [SerializeField] private float crouchSpeed = 0.6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    [SerializeField] private float externalDrag = 6f;        // how fast knockback fades (bigger = stops sooner)
    [SerializeField] private float maxExternalSpeed = 25f;   // clamp so blasts don't send you to orbit

    private Vector3 externalVelocity; // this is the knockback/launch velocity

    public void AddExternalImpulse(Vector3 impulse)
    {
        externalVelocity += impulse;

        // Clamp so it doesn't get stupidly fast.
        float mag = externalVelocity.magnitude;
        if (mag > maxExternalSpeed)
            externalVelocity = (externalVelocity / mag) * maxExternalSpeed;
    }


    [Header("Crouch")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float colliderLerpSpeed = 12f;
    [SerializeField] private float standCheckPadding = 0.05f;

    [Header("Slide")]
    [SerializeField] private float slideSpeed = 7.5f;
    [SerializeField] private float slideDuration = 0.65f;
    [SerializeField] private float slideCooldown = 0.35f;
    private bool _isSliding;
    private float _slideTimer;
    private float _slideCooldownTimer;
    private Vector3 _slideDir;
    private const string P_SLIDE = "IsSliding";

    [SerializeField] private float slideQueueTime = 0.25f; // how long we remember the request

    private bool _slideQueued;
    private float _slideQueueUntil;
    private bool _wasGrounded;

    [Header("Look Limits (Owner Only)")]
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Camera (Owner Only)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform cameraPivot;

    [Header("Head Follow (Owner Only)")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Vector3 headLocalOffset = new Vector3(0f, 0.08f, 0.02f);
    [Range(0f, 0.25f)]
    [SerializeField] private float headFollowSmoothing = 0.05f;

    [Header("Camera Framing (Owner Only)")]
    [SerializeField] private float cameraForwardOffset = 0.08f;
    [SerializeField] private float cameraUpOffset = 0.02f;
    [SerializeField] private float ownerNearClipPlane = 0.02f;

    [Header("Owner Head/Face Hide (Recommended)")]
    [SerializeField] private bool hideHeadForOwner = true;
    [SerializeField] private SkinnedMeshRenderer[] headRenderersForOwner;
    [SerializeField] private Transform[] hideBonesForOwner;
    [SerializeField] private Renderer[] hideRenderersForOwner;
    private Vector3[] _originalBoneScales;

    [Header("Other Cams")]
    [SerializeField] private Camera pauseCam;
    [SerializeField] private Camera roundOverCam;

    // -------------------- Ablities --------------------



    // -------------------- Net Look Replication --------------------
    [Header("Sync FreeLook (v4 RPC)")]
    [SerializeField] private float lookSendRate = 20f;
    [SerializeField] private float lookSendEpsilon = 0.05f;

    private float _nextLookSendTime;
    private float _lastSentPivotPitch;
    private float _lastSentYawOffset;
    private float _lastSentRoll;

    private float _localPivotPitch;
    private float _localYawOffset;
    private float _localRoll;

    private float _netPivotPitch;
    private float _netYawOffset;
    private float _netRoll;
    private bool _hasNetLook;

    [Header("Camera Tilt (Owner Only)")]
    [SerializeField] private float turnSwayAmount = 2f;
    [SerializeField] private float turnSwaySmoothTime = 0.08f;
    private float roll;
    private float rollVel;

    [Header("Alt Look (Owner Only)")]
    [SerializeField] private KeyCode altLookKey = KeyCode.LeftAlt;
    [SerializeField] private float altLookYawLimit = 170f;
    [SerializeField] private float altLookReturnSpeed = 160f;

    [Header("Alt Look Vertical (Owner Only)")]
    [SerializeField] private float altLookPitchLimit = 30f;
    [SerializeField] private float altLookPitchScale = 0.35f;
    [SerializeField] private float altLookPitchReturnSpeed = 120f;

    private float cameraYawOffset;
    private float altPitch;

    [Header("Animations")]
    [SerializeField] private Animator animator;

    [Header("Optional Head Look IK (Owner Only)")]
    [SerializeField] private HeadLookIK headLookIK;

    private const string P_H = "Horizontal";
    private const string P_V = "Vertical";
    private const string P_SPRINT = "IsSprinting";
    private const string P_CROUCH = "IsCrouching";
    private const string P_GROUNDED = "IsGrounded";
    private const string P_JUMP = "Jump";
    private const string P_FALL = "Fall";

    private const string P_SCRAMBLE = "IsScrambling";
    private const string P_LEDGEHANG = "IsLedgeHanging";
    private const string T_CLIMBUP = "ClimbUp";

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    private float mouseX, mouseY;
    private float yaw;
    private float pitch;

    private Vector3 camFollowVel;

    private bool isCrouching;
    private float targetControllerHeight;
    private Vector3 defaultControllerCenter;

    private Camera playerCam;
    public StatusController status;

    // -------------------- Traversal Lockout --------------------
    [Header("Traversal Lockout (Disable Tagging / FingerGun)")]
    [SerializeField] private MonoBehaviour[] disableWhileTraversing;
    [SerializeField] private MonoBehaviour fingerGunPoseScript;
    [SerializeField] private bool lockoutOwnerOnly = true;

    // -------------------- Parkour --------------------
    private enum ParkourMode { None, Scramble, Hang, ClimbUp }

    [Header("Parkour Layers")]
    [SerializeField] private LayerMask climbMask = ~0;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Wall Scramble")]
    [SerializeField] private float wallCheckDistance = 0.9f;
    [Range(0f, 1f)][SerializeField] private float wallLookDotMin = 0.55f;
    [SerializeField] private float scrambleUpSpeed = 6.5f;
    [SerializeField] private float scrambleDuration = 0.35f;
    [SerializeField] private float scrambleCooldown = 0.25f;
    [SerializeField] private float wallStick = 0.35f;
    [SerializeField] private float scrambleRegrabBlockTime = 0.2f;
    private float _noScrambleUntil;

    [Header("Scramble Jump Off")]
    [SerializeField] private float scrambleJumpUp = 4.5f;
    [SerializeField] private float scrambleJumpAway = 3.0f;
    [SerializeField] private float scrambleJumpLateral = 2.0f;

    [Header("Ledge Grab")]
    [SerializeField] private float ledgeProbeForward = 0.55f;
    [SerializeField] private float ledgeProbeUp = 1.5f;
    [SerializeField] private float ledgeGrabDistance = 0.75f;
    [SerializeField] private float hangOffsetFromWall = 0.35f;
    [SerializeField] private float hangVerticalOffset = -1.05f;

    [Header("Hang Lock (keeps capsule fixed while hanging)")]
    [SerializeField] private float hangSnapSpeed = 25f;
    [SerializeField] private float hangStickDistance = 0.02f;
    private Vector3 _hangLockPos;

    [Header("Hang Facing")]
    [SerializeField] private float hangFaceSpeed = 25f;
    [SerializeField] private bool hardLockHangYaw = true;
    private float _hangLockedYaw;

    [Header("Falling Grab (Jump Required)")]
    [SerializeField] private float jumpGrabBuffer = 0.18f;
    [SerializeField] private float fallingSpeedMin = -1.5f;

    [Header("Hang Input")]
    [SerializeField] private float hangInputGrace = 0.15f;
    private float _hangIgnoreMoveUntil;

    [Header("Hang Alignment")]
    [SerializeField] private Transform hangHandAnchor;
    [SerializeField] private float handInsetIntoLedge = 0.04f;
    [SerializeField] private float handVerticalAdjust = 0.00f;

    [Header("Climb Up")]
    [SerializeField] private float climbUpTime = 0.35f;

    [Header("Visual Root (optional)")]
    [SerializeField] private Transform visualRoot;
    private Vector3 _visualRootLocalPos;
    private Quaternion _visualRootLocalRot;

    [Header("Parkour Debug")]
    [SerializeField] private bool debugParkourLogs = false;
    [SerializeField] private bool debugParkourGizmos = true;
    [SerializeField] private float debugLogThrottle = 0.15f;

    [Header("Live Debug (read-only in playmode)")]
    [SerializeField] private string ledgeFailReason = "";
    [SerializeField] private float ledgeFacingDot = 0f;
    [SerializeField] private bool dbg_wallHit;
    [SerializeField] private bool dbg_topHit;
    [SerializeField] private bool dbg_standRoom;
    [SerializeField] private bool dbg_hangRoom;

    private float _lastDbgLogTime;

    private ParkourMode _mode = ParkourMode.None;
    private float _modeTimer;
    private float _cooldownTimer;
    private float _jumpBufferTimer;

    private Vector3 _hangPos;
    private Vector3 _hangWallNormal;
    private Vector3 _standPos;

    // feet-to-controller helpers (works with CC center)
    private Vector3 FeetToControllerPos(Vector3 feetPos, float height)
    {
        return feetPos + Vector3.up * (height * 0.5f) - controller.center;
    }

    private Vector3 _climbEnd;
    private float _climbT;

    private CollisionFlags _lastMoveFlags;
    private RaycastHit _lastWallHit;
    private bool _hasLastWallHit;

    // gizmo debug points
    private Vector3 _dbgProbeOrigin;
    private Vector3 _dbgProbeDir;
    private RaycastHit _dbgWallHit;
    private Vector3 _dbgAbovePoint;
    private RaycastHit _dbgTopHit;
    private Vector3 _dbgHangCandidate;
    private Vector3 _dbgStandCandidate;

    public bool IsTraversalLocked =>
        _mode == ParkourMode.Scramble ||
        _mode == ParkourMode.Hang ||
        _mode == ParkourMode.ClimbUp;

    private bool _lastTraversalLocked;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (headLookIK == null)
            headLookIK = GetComponentInChildren<HeadLookIK>(true);

        if (visualRoot != null)
        {
            _visualRootLocalPos = visualRoot.localPosition;
            _visualRootLocalRot = visualRoot.localRotation;
        }

        defaultControllerCenter = controller.center;
        targetControllerHeight = Mathf.Max(0.1f, standHeight > 0f ? standHeight : controller.height);

        altLookKeyRuntime = (KeyCode)PlayerPrefs.GetInt("FreeLook", (int)KeyCode.LeftAlt);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(IsOwner);

        if (!IsOwner) return;

        if (cameraTransform != null)
        {
            playerCam = cameraTransform.GetComponent<Camera>();
            if (playerCam != null && ownerNearClipPlane > 0f)
                playerCam.nearClipPlane = ownerNearClipPlane;
        }

        pauseCam = FindSceneCameraByName("PauseCamera");
        roundOverCam = FindSceneCameraByName("RoundOverCamera");

        if (hideHeadForOwner)
            ApplyOwnerHeadHide();

        UpdateCursorState();

        // Ensure only the player cam is on at start (if round running)
        if (pauseCam != null) pauseCam.enabled = false;
        if (roundOverCam != null) roundOverCam.enabled = false;
        if (playerCam != null) playerCam.enabled = true;
    }

    private Camera FindSceneCameraByName(string camName)
    {
        var cams = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (var c in cams)
        {
            if (c == null) continue;
            if (c.name != camName) continue;
            if (!c.gameObject.scene.IsValid() || !c.gameObject.scene.isLoaded) continue;
            return c;
        }
        return null;
    }

    private void Update()
    {
        if (!IsOwner)
        {
            ApplyRemoteLookVisuals();
            ApplyTraversalLockoutIfChanged();
            return;
        }

        //Abilities
        if (status != null && status.IsFrozen)
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            return; // stops all movement processing while frozen
        }



        bool roundAllowsControls = RoundManager.RoundRunningClient || RoundOverUI.ControlsEnabled;
        bool paused = LocalPauseMenu.IsPauseOpen;

        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 3f);
        UpdateCursorState();

        if (controller == null || !controller.enabled)
        {
            ApplyCameraSwitch(paused, roundAllowsControls);
            return;
        }

        bool groundedNow = controller.isGrounded;

        // Always switch cams first (this is what you broke before).
        ApplyCameraSwitch(paused, roundAllowsControls);

        // Timers
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
        if (_modeTimer > 0f) _modeTimer -= Time.deltaTime;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;
        if (_slideCooldownTimer > 0f) _slideCooldownTimer -= Time.deltaTime;

        // Inputs
        float inputX = 0f;
        float inputZ = 0f;
        bool tryingSprint = false;
        bool wantsCrouchHeld = false;
        bool crouchDown = false;
        bool wantsJumpDown = false;

        if (roundAllowsControls && !paused)
        {
            KeyCode sprintKeyLocal = (KeyCode)PlayerPrefs.GetInt("SprintKey", (int)KeyCode.LeftShift);
            KeyCode crouchKeyLocal = (KeyCode)PlayerPrefs.GetInt("CrouchKey", (int)KeyCode.LeftControl);
            KeyCode freeLookKeyLocal = (KeyCode)PlayerPrefs.GetInt("FreeLook", (int)KeyCode.LeftAlt);
            altLookKeyRuntime = freeLookKeyLocal;
            KeyCode jumpKey = (KeyCode)PlayerPrefs.GetInt("JumpKey", (int)KeyCode.Space);

            inputX = Input.GetAxisRaw("Horizontal");
            inputZ = Input.GetAxisRaw("Vertical");
            tryingSprint = Input.GetKey(sprintKeyLocal);
            wantsCrouchHeld = Input.GetKey(crouchKeyLocal);
            crouchDown = Input.GetKeyDown(crouchKeyLocal);
            wantsJumpDown = Input.GetKeyDown(jumpKey);

            if (Time.time > _slideQueueUntil) _slideQueued = false;
            if (_mode != ParkourMode.None) _slideQueued = false;          // don’t queue during hang/scramble/climb
            if (!roundAllowsControls || paused) _slideQueued = false;     // don’t queue while paused/round-over

            ReadMouse();
        }
        else
        {
            mouseX = 0f;
            mouseY = 0f;
        }

        // Queue slide request while airborne (sprint + hold crouch + forward)
        if (!groundedNow && !_isSliding && _mode == ParkourMode.None)
        {
            if (tryingSprint && wantsCrouchHeld && inputZ > 0.15f)
            {
                _slideQueued = true;
                _slideQueueUntil = Time.time + slideQueueTime;
            }
        }
        bool justLanded = (groundedNow && !_wasGrounded);

        if (justLanded && _slideQueued)
        {
            _slideQueued = false;

            if (Time.time <= _slideQueueUntil)
            {
                // Only start if it’s actually allowed right now
                if (CanStartSlide(roundAllowsControls, paused, wantsCrouchHeld, tryingSprint, inputX, inputZ))
                    StartSlide(inputX, inputZ);
            }
        }

        if (Time.time > _slideQueueUntil) _slideQueued = false;
        if (_mode != ParkourMode.None) _slideQueued = false;          // don’t queue during hang/scramble/climb
        if (!roundAllowsControls || paused) _slideQueued = false;     // don’t queue while paused/round-over

        if (headLookIK != null)
            headLookIK.enableLook = (_mode == ParkourMode.None);

        // If paused: NO locomotion input, but physics should still behave (falling),
        // and hang/climb should stay pinned if you're in those modes.
        if (paused || !roundAllowsControls)
        {
            // keep parkour pinned if currently in it
            if (_mode != ParkourMode.None)
                UpdateParkour(0f, 0f, false);

            // end slide if you paused mid-slide (optional, but usually desired)
            if (_isSliding)
            {

            }
                EndSlide();

            // still apply gravity if airborne and not hanging
            if (_mode == ParkourMode.None)
                ApplyGravityOnly();

            // keep anims stable
            SetLocomotionParams(0f, 0f, false, isCrouching);
            UpdateAnimFlags(0f, 0f, false);
            ApplyTraversalLockoutIfChanged();
            return;
        }

        // Slide update (only when not paused)
        if (_isSliding)
        {
            if (_isSliding)
            {
                // cancel if crouch released
                if (!wantsCrouchHeld)
                {
                    CancelSlide();
                }
                else
                {
                    _slideTimer -= Time.deltaTime;

                    controller.Move(_slideDir * slideSpeed * Time.deltaTime);

                    velocity.y += gravity * Time.deltaTime;
                    controller.Move(Vector3.up * (velocity.y + externalVelocity.y) * Time.deltaTime);
                    if (_slideTimer <= 0f)
                        CancelSlide();

                    ApplyCrouchCollider();
                    ApplyTraversalLockoutIfChanged();
                    return;
                }
            }

            UpdateSlide(tryingSprint, wantsJumpDown);
            ApplyCrouchCollider();
            UpdateAnimFlags(0f, 1f, tryingSprint);
            ApplyTraversalLockoutIfChanged();
            return;
        }

        // Slide start (FIXED: only on crouch DOWN)
        if (CanStartSlide(roundAllowsControls, paused, crouchDown, tryingSprint, inputX, inputZ))
            StartSlide(inputX, inputZ);

        isGrounded = controller.isGrounded;

        if (wantsJumpDown)
            _jumpBufferTimer = jumpGrabBuffer;

        // Parkour runs before normal movement if already in a mode
        if (_mode != ParkourMode.None)
        {
            UpdateParkour(inputX, inputZ, wantsJumpDown);
            ApplyCrouchCollider();
            UpdateAnimFlags(inputX, inputZ, tryingSprint);
            ApplyTraversalLockoutIfChanged();
            return;
        }

        UpdateCrouchState(wantsCrouchHeld);

        bool sprint = tryingSprint && !isCrouching;
        bool sprintAnim = sprint && inputZ >= -0.1f;
        float speedToUse = isCrouching ? crouchSpeed : (sprint ? sprintSpeed : walkSpeed);

        HandleMovement(inputX, inputZ, speedToUse, allowJump: !isCrouching, wantsJump: wantsJumpDown);

        SetLocomotionParams(inputX, inputZ, sprintAnim, isCrouching);

        bool falling = !controller.isGrounded && velocity.y < -0.1f;
        SetGroundParams(controller.isGrounded, falling);

        // Falling ledge grab / scramble
        if (_mode == ParkourMode.None)
        {
            if (TryStartHangFromFallingJumpBuffer() || TryStartScrambleFromWallHit())
            {
                ApplyCrouchCollider();
                UpdateAnimFlags(inputX, inputZ, tryingSprint);
                ApplyTraversalLockoutIfChanged();
                return;
            }
        }

        if (debugParkourGizmos && ((_lastMoveFlags & CollisionFlags.Sides) != 0 || _hasLastWallHit))
            TryComputeLedge(out _, out _, out _, commit: false);

        ApplyCrouchCollider();
        UpdateAnimFlags(inputX, inputZ, tryingSprint);
        ApplyTraversalLockoutIfChanged();

        _wasGrounded = groundedNow;
    }

    // -------------------- Cameras --------------------
    private void ApplyCameraSwitch(bool paused, bool roundAllowsControls)
    {
        // Round over means gameplay input is not allowed (your project uses these flags)
        bool isRoundOver = !roundAllowsControls;

        if (paused)
        {
            if (pauseCam != null) pauseCam.enabled = true;
            if (playerCam != null) playerCam.enabled = false;
            if (roundOverCam != null) roundOverCam.enabled = false;

            // if you use these:
            ScoreboardUI.HideRoundOver();
            RoundOverUI.SetRoundActiveClient(true);
            return;
        }

        if (isRoundOver)
        {
            if (roundOverCam != null) roundOverCam.enabled = true;
            if (playerCam != null) playerCam.enabled = false;
            if (pauseCam != null) pauseCam.enabled = false;

            RoundOverUI.SetRoundActiveClient(false);
            ScoreboardUI.ShowRoundOver();
        }
        else
        {
            if (playerCam != null) playerCam.enabled = true;
            if (pauseCam != null) pauseCam.enabled = false;
            if (roundOverCam != null) roundOverCam.enabled = false;

            RoundOverUI.SetRoundActiveClient(true);
            ScoreboardUI.HideRoundOver();
        }
    }

    // -------------------- Slide --------------------
    private bool CanStartSlide(bool roundAllowsControls, bool paused, bool crouchDown, bool tryingSprint, float inputX, float inputZ)
    {
        if (!roundAllowsControls || paused) return false;
        if (_mode != ParkourMode.None) return false;
        if (_isSliding) return false;
        if (_slideCooldownTimer > 0f) return false;
        if (!controller.isGrounded) return false;
        if (!tryingSprint) return false;
        if (!crouchDown) return false;          // IMPORTANT: only on press
        if (inputZ <= 0.15f) return false;
        return true;
    }

    private void StartSlide(float inputX, float inputZ)
    {
        _isSliding = true;
        _slideTimer = slideDuration;
        _slideCooldownTimer = slideCooldown;

        _slideDir = (transform.forward * Mathf.Max(0.2f, inputZ) + transform.right * inputX);
        _slideDir.y = 0f;
        _slideDir = (_slideDir.sqrMagnitude > 0.0001f) ? _slideDir.normalized : transform.forward;

        isCrouching = true;
        targetControllerHeight = crouchHeight;

        if (animator != null)
            animator.SetBool(P_SLIDE, true);
    }

    private void CancelSlide()
    {
        if (!_isSliding) return;

        _isSliding = false;
        _slideTimer = 0f;
        _slideCooldownTimer = slideCooldown;

        if (animator != null)
            animator.SetBool(P_SLIDE, false);

        // If we cancelled, try to stand up (optional)
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

    private void UpdateSlide(bool tryingSprint, bool jumpDown)
    {
        _slideTimer -= Time.deltaTime;

        // Jump cancels slide immediately
        if (jumpDown && controller.isGrounded)
        {
            EndSlide();

            // do a normal jump
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null)
                animator.SetTrigger(P_JUMP);

            return;
        }

        // lock crouch collider during slide
        isCrouching = true;
        targetControllerHeight = crouchHeight;

        // move
        controller.Move(_slideDir * slideSpeed * Time.deltaTime);

        // gravity (with ground clamp)
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * (velocity.y + externalVelocity.y) * Time.deltaTime);
        if (_slideTimer <= 0f || !tryingSprint)
            EndSlide();
    }

    private void EndSlide()
    {
        _isSliding = false;
        if (animator != null) animator.SetBool(P_SLIDE, false);
    }

    // -------------------- Parkour --------------------
    private bool TryStartHangFromFallingJumpBuffer()
    {
        if (_jumpBufferTimer <= 0f) return false;
        if (controller.isGrounded) return false;
        if (velocity.y > fallingSpeedMin) return false;

        if (TryComputeLedge(out Vector3 hangPos, out Vector3 standPos, out Vector3 wallNormal, commit: false))
        {
            BeginHang(hangPos, standPos, wallNormal);
            _jumpBufferTimer = 0f;
            return true;
        }
        return false;
    }

    private bool TryStartScrambleFromWallHit()
    {
        if (Time.time < _noScrambleUntil) return false;
        if (_cooldownTimer > 0f) return false;
        if ((_lastMoveFlags & CollisionFlags.Sides) == 0) return false;

        if (!TryGetWallInFront(out RaycastHit hit)) return false;

        float dot = Vector3.Dot(transform.forward, -hit.normal);
        if (dot < wallLookDotMin) return false;

        BeginScramble(hit);
        return true;
    }

    private void BeginScramble(RaycastHit wallHit)
    {
        _mode = ParkourMode.Scramble;
        _modeTimer = scrambleDuration;
        _cooldownTimer = scrambleCooldown;

        _hasLastWallHit = true;
        _lastWallHit = wallHit;

        if (velocity.y < 0f) velocity.y = 0f;
        velocity.y = Mathf.Max(velocity.y, scrambleUpSpeed);

        if (debugParkourLogs) DbgLog("[Parkour] Scramble start");

        if (TryComputeLedge(out Vector3 hangPos, out Vector3 standPos, out Vector3 wallNormal, commit: false))
            BeginHang(hangPos, standPos, wallNormal);
    }

    private void JumpOffFromScramble(float inputX, float inputZ)
    {
        // end scramble immediately
        _mode = ParkourMode.None;

        // block re-scramble for a moment
        _noScrambleUntil = Time.time + scrambleRegrabBlockTime;

        // also force cooldown so it can't restart instantly
        _cooldownTimer = Mathf.Max(_cooldownTimer, scrambleCooldown);

        // push away from last wall if we have it
        Vector3 away = _hasLastWallHit
            ? Vector3.ProjectOnPlane(_lastWallHit.normal, Vector3.up).normalized
            : -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        // lateral steering from input
        Vector3 lateral = (transform.right * inputX + transform.forward * inputZ);
        lateral.y = 0f;
        lateral = Vector3.ClampMagnitude(lateral, 1f);

        Vector3 push = (away * scrambleJumpAway) + (lateral * scrambleJumpLateral);

        velocity = push;
        velocity.y = scrambleJumpUp;


        // animator flags
        if (animator != null)
            animator.SetBool(P_SCRAMBLE, false);
    }

    private void UpdateParkour(float inputX, float inputZ, bool jumpDown)
    {
        switch (_mode)
        {
            case ParkourMode.Scramble:
                {
                    if (jumpDown)
                    {
                        JumpOffFromScramble(inputX, inputZ);
                        return;
                    }

                    if (TryGetWallInFront(out RaycastHit fresh))
                    {
                        _hasLastWallHit = true;
                        _lastWallHit = fresh;
                    }

                    if (TryComputeLedge(out Vector3 hangPos, out Vector3 standPos, out Vector3 wallNormal, commit: false))
                    {
                        BeginHang(hangPos, standPos, wallNormal);
                        return;
                    }

                    Vector3 stickDir = Vector3.zero;
                    if (_hasLastWallHit)
                        stickDir = -_lastWallHit.normal * wallStick;

                    controller.Move(stickDir * Time.deltaTime);

                    velocity.y += gravity * Time.deltaTime;
                    controller.Move(Vector3.up * (velocity.y + externalVelocity.y) * Time.deltaTime);
                    if (_modeTimer <= 0f)
                    {
                        _mode = ParkourMode.None;
                        _cooldownTimer = scrambleCooldown;
                        if (debugParkourLogs) DbgLog("[Parkour] Scramble end");
                    }
                    break;
                }

            case ParkourMode.Hang:
                {
                    Vector3 delta = _hangLockPos - transform.position;
                    if (delta.sqrMagnitude > (hangStickDistance * hangStickDistance))
                    {
                        Vector3 step = delta * (hangSnapSpeed * Time.deltaTime);
                        if (step.sqrMagnitude > delta.sqrMagnitude) step = delta;
                        controller.Move(step);
                    }
                    else
                    {
                        controller.Move(delta);
                    }

                    if (hardLockHangYaw)
                    {
                        yaw = _hangLockedYaw;
                        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                    else
                    {
                        Quaternion target = Quaternion.Euler(0f, _hangLockedYaw, 0f);
                        transform.rotation = Quaternion.Slerp(transform.rotation, target, hangFaceSpeed * Time.deltaTime);
                        yaw = transform.eulerAngles.y;
                    }

                    velocity = Vector3.zero;

                    if (jumpDown)
                    {
                        if (inputZ > 0.35f)
                        {
                            BeginClimbUp();
                            return;
                        }

                        DropFromHang(new Vector3(inputX, 0f, inputZ), jumpOff: true);
                        return;
                    }

                    if (visualRoot != null)
                    {
                        visualRoot.localPosition = _visualRootLocalPos;
                        visualRoot.localRotation = _visualRootLocalRot;
                    }
                    break;
                }

            case ParkourMode.ClimbUp:
                {
                    // pinned while anim plays
                    controller.Move(_hangLockPos - transform.position);
                    velocity = Vector3.zero;

                    if (visualRoot != null)
                    {
                        visualRoot.localPosition = _visualRootLocalPos;
                        visualRoot.localRotation = _visualRootLocalRot;
                    }

                    _climbT += Time.deltaTime;
                    if (_climbT >= climbUpTime)
                    {
                        controller.enabled = false;
                        transform.position = _climbEnd;
                        controller.enabled = true;

                        _mode = ParkourMode.None;

                        if (CanStandUp())
                        {
                            isCrouching = false;
                            targetControllerHeight = standHeight;
                        }
                    }
                    break;
                }
        }

        bool falling = !controller.isGrounded && velocity.y < -0.1f;
        SetGroundParams(controller.isGrounded, falling);
    }

    private void BeginHang(Vector3 hangPos, Vector3 standPos, Vector3 wallNormal)
    {
        _mode = ParkourMode.Hang;
        _cooldownTimer = scrambleCooldown;

        _hangWallNormal = wallNormal;
        Vector3 faceDir = Vector3.ProjectOnPlane(-_hangWallNormal, Vector3.up);
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            _hangLockedYaw = Quaternion.LookRotation(faceDir.normalized, Vector3.up).eulerAngles.y;

            if (hardLockHangYaw)
            {
                yaw = _hangLockedYaw;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                cameraYawOffset = 0f;
            }
        }

        _standPos = standPos;
        _hangPos = hangPos;

        // force crouch collider NOW
        isCrouching = true;
        targetControllerHeight = crouchHeight;
        ApplyCrouchCollider();

        _hangLockPos = FeetToControllerPos(hangPos, controller.height);
        controller.Move(_hangLockPos - transform.position);
        _hangLockPos = transform.position;

        _hangIgnoreMoveUntil = Time.time + hangInputGrace;
        velocity = Vector3.zero;

        if (animator != null)
        {
            animator.SetBool(P_SCRAMBLE, false);
            animator.SetBool(P_LEDGEHANG, true);
        }

        if (hangHandAnchor != null && dbg_topHit)
        {
            Vector3 desiredHandWorld = _dbgTopHit.point;
            desiredHandWorld -= _hangWallNormal.normalized * handInsetIntoLedge;
            desiredHandWorld += Vector3.up * handVerticalAdjust;

            Vector3 delta = desiredHandWorld - hangHandAnchor.position;

            _hangLockPos += delta;
            _hangPos += delta;
            controller.Move(_hangLockPos - transform.position);
            _hangLockPos = transform.position;
        }

        if (debugParkourLogs) DbgLog("[Parkour] Hang");
    }

    private void BeginClimbUp()
    {
        _mode = ParkourMode.ClimbUp;
        _climbT = 0f;

        // stay crouched through climb
        isCrouching = true;
        targetControllerHeight = crouchHeight;
        ApplyCrouchCollider();

        _climbEnd = FeetToControllerPos(_standPos, controller.height);

        if (animator != null)
        {
            animator.SetBool(P_LEDGEHANG, false);
            animator.SetTrigger(T_CLIMBUP);
        }
    }

    private void DropFromHang(Vector3 inputDir, bool jumpOff)
    {
        if (animator != null)
        {
            animator.SetBool(P_LEDGEHANG, false);
            animator.SetBool(P_SCRAMBLE, false);
        }

        _mode = ParkourMode.None;
        _cooldownTimer = scrambleCooldown;

        Vector3 lateral = (transform.right * inputDir.x + transform.forward * inputDir.z);
        lateral = Vector3.ClampMagnitude(lateral, 1f);

        Vector3 away = Vector3.ProjectOnPlane(-_hangWallNormal, Vector3.up).normalized;

        Vector3 push = (away * 3.0f) + (lateral * 2.0f);
        velocity = push;
        velocity.y = jumpOff ? 4.5f : -1.5f;

        if (debugParkourLogs) DbgLog("[Parkour] Drop (jumpOff=" + jumpOff + ")");
    }

    private bool TryGetWallInFront(out RaycastHit hit)
    {
        float originHeight = Mathf.Clamp(controller.height * 0.55f, 0.9f, controller.height - 0.2f);
        Vector3 origin = transform.position + Vector3.up * originHeight;

        Vector3 dir = (cameraPivot != null) ? cameraPivot.forward : transform.forward;
        float radius = Mathf.Max(0.15f, controller.radius * 0.9f);

        return Physics.SphereCast(origin, radius, dir, out hit, wallCheckDistance, climbMask, QueryTriggerInteraction.Ignore);
    }

    private bool TryComputeLedge(out Vector3 hangPos, out Vector3 standPos, out Vector3 wallNormal, bool commit)
    {
        hangPos = default;
        standPos = default;
        wallNormal = default;

        ledgeFailReason = "";
        dbg_wallHit = dbg_topHit = dbg_standRoom = dbg_hangRoom = false;

        if (cameraPivot == null)
        {
            ledgeFailReason = "No cameraPivot";
            return false;
        }

        RaycastHit wallHit;

        float chestY = Mathf.Clamp(controller.height * 0.60f, 0.9f, 1.35f);
        Vector3 originBase = transform.position + Vector3.up * chestY;

        float radius = Mathf.Max(0.18f, controller.radius * 0.95f);
        float dist = Mathf.Max(ledgeGrabDistance, 1.0f);

        bool hitFound = false;
        RaycastHit best = default;

        Vector3 dir1 = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (Physics.SphereCast(originBase - dir1 * 0.1f, radius, dir1, out RaycastHit h1, dist, climbMask, QueryTriggerInteraction.Ignore))
        {
            hitFound = true;
            best = h1;
        }

        Vector3 camDir = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
        if (camDir.sqrMagnitude > 0.0001f)
        {
            camDir.Normalize();

            if (!hitFound && Physics.SphereCast(originBase - camDir * 0.1f, radius, camDir, out RaycastHit h2, dist, climbMask, QueryTriggerInteraction.Ignore))
            {
                hitFound = true;
                best = h2;
            }
        }

        if (!hitFound)
        {
            ledgeFailReason = "NoWallHit";
            _dbgProbeOrigin = originBase;
            _dbgProbeDir = dir1;
            return false;
        }

        wallHit = best;
        wallNormal = wallHit.normal.normalized;
        dbg_wallHit = true;
        _dbgWallHit = wallHit;

        float minWallHitHeight = transform.position.y + (controller.height * 0.45f);
        if (wallHit.point.y < minWallHitHeight)
        {
            ledgeFailReason = "TooLowOnWall";
            return false;
        }

        Vector3 lookDir = cameraPivot.forward;
        Vector3 lookFlat = Vector3.ProjectOnPlane(lookDir, Vector3.up);
        if (lookFlat.sqrMagnitude < 0.0001f)
            lookFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        lookFlat.Normalize();

        Vector3 wallNormalFlat = Vector3.ProjectOnPlane(wallNormal, Vector3.up).normalized;

        ledgeFacingDot = Vector3.Dot(lookFlat, -wallNormalFlat);
        if (ledgeFacingDot < wallLookDotMin)
        {
            ledgeFailReason = $"FacingDotFail ({ledgeFacingDot:0.00})";
            return false;
        }

        Vector3 forwardFlat = -wallNormalFlat;

        Vector3 insetIntoWall = -wallNormal * 0.08f;

        float probeHeight = transform.position.y + controller.height + 0.15f;
        Vector3 baseStart = new Vector3(wallHit.point.x, probeHeight, wallHit.point.z) + insetIntoWall;

        float downDist = controller.height + 2.0f;

        Vector3 right = Vector3.Cross(Vector3.up, wallNormalFlat).normalized;
        Vector3 alongWall = Vector3.Cross(wallNormalFlat, Vector3.up).normalized;

        Vector3[] starts =
        {
            baseStart,
            baseStart + right * 0.18f,
            baseStart - right * 0.18f,
            baseStart + alongWall * 0.18f,
            baseStart - alongWall * 0.18f
        };

        dbg_topHit = false;

        for (int i = 0; i < starts.Length; i++)
        {
            if (Physics.Raycast(starts[i], Vector3.down, out _dbgTopHit, downDist, climbMask, QueryTriggerInteraction.Ignore))
            {
                dbg_topHit = true;
                _dbgAbovePoint = starts[i];
                break;
            }
        }

        if (!dbg_topHit)
        {
            ledgeFailReason = "NoTopHit";
            return false;
        }

        _dbgStandCandidate = _dbgTopHit.point + forwardFlat * 0.22f;
        dbg_standRoom = HasStandingRoomAt(_dbgStandCandidate);
        if (!dbg_standRoom)
        {
            ledgeFailReason = "NoStandRoom";
            return false;
        }

        _dbgHangCandidate = _dbgTopHit.point
                            + (-wallNormal * hangOffsetFromWall)
                            + (Vector3.up * hangVerticalOffset);

        dbg_hangRoom = HasHangingRoomAt(_dbgHangCandidate);
        if (!dbg_hangRoom)
        {
            ledgeFailReason = "NoHangRoom";
            return false;
        }

        standPos = _dbgStandCandidate;
        hangPos = _dbgHangCandidate;

        ledgeFailReason = "OK";
        return true;
    }

    private bool HasHangingRoomAt(Vector3 worldPos)
    {
        float radius = Mathf.Max(0.05f, controller.radius * 0.85f);
        float height = Mathf.Max(crouchHeight * 0.9f, 0.85f);

        Vector3 bottom = worldPos + Vector3.up * (radius + 0.02f);
        Vector3 top = bottom + Vector3.up * (height - radius * 2f);

        return !Physics.CheckCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private bool HasStandingRoomAt(Vector3 worldPos)
    {
        float radius = Mathf.Max(0.05f, controller.radius - 0.02f);
        float height = standHeight;

        Vector3 bottom = worldPos + Vector3.up * (radius + 0.02f);
        Vector3 top = bottom + Vector3.up * (height - radius * 2f);

        return !Physics.CheckCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // -------------------- Locomotion --------------------
    private void HandleMovement(float inputX, float inputZ, float speedToUse, bool allowJump, bool wantsJump)
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        Vector3 moveDir = transform.right * inputX + transform.forward * inputZ;
        moveDir.y = 0f;
        Vector3 extHoriz = new Vector3(externalVelocity.x, 0f, externalVelocity.z);

        _lastMoveFlags = controller.Move((moveDir * speedToUse + extHoriz) * Time.deltaTime);


        if (allowJump && wantsJump && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null)
                animator.SetTrigger(P_JUMP);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * (velocity.y + externalVelocity.y) * Time.deltaTime);
        isGrounded = controller.isGrounded;

        if (TryGetWallInFront(out RaycastHit wallHit))
        {
            _hasLastWallHit = true;
            _lastWallHit = wallHit;
        }
        else
        {
            _hasLastWallHit = false;
        }

        // Stop external downward push when grounded.
        if (controller.isGrounded && externalVelocity.y < 0f)
            externalVelocity.y = 0f;

        // Decay knockback/launch over time.
        externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, externalDrag * Time.deltaTime);
    }

    private void ApplyGravityOnly()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * (velocity.y + externalVelocity.y) * Time.deltaTime);

        if (controller.isGrounded && externalVelocity.y < 0f)
            externalVelocity.y = 0f;

        externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, externalDrag * Time.deltaTime);
    }

    private void UpdateCrouchState(bool wantsCrouch)
    {
        if (_mode != ParkourMode.None || _isSliding)
        {
            isCrouching = true;
            targetControllerHeight = crouchHeight;
            return;
        }

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

    private void UpdateAnimFlags(float inputX, float inputZ, bool tryingSprint)
    {
        if (animator == null) return;

        // IMPORTANT: crouch should NOT fight slide
        bool animCrouch = isCrouching && (_mode == ParkourMode.None) && !_isSliding;
        animator.SetBool(P_CROUCH, animCrouch);

        animator.SetBool(P_SCRAMBLE, _mode == ParkourMode.Scramble);
        animator.SetBool(P_LEDGEHANG, _mode == ParkourMode.Hang);

        bool sprintAnim = tryingSprint && !isCrouching && inputZ >= -0.1f;
        animator.SetBool(P_SPRINT, sprintAnim);

        animator.SetBool(P_SLIDE, _isSliding);
    }

    // -------------------- Traversal Lockout --------------------
    private void ApplyTraversalLockoutIfChanged()
    {
        bool locked = IsTraversalLocked;
        if (locked == _lastTraversalLocked) return;
        _lastTraversalLocked = locked;

        if (lockoutOwnerOnly && !IsOwner) return;

        if (disableWhileTraversing != null)
        {
            foreach (var b in disableWhileTraversing)
            {
                if (b == null) continue;
                b.enabled = !locked;
            }
        }

        if (fingerGunPoseScript != null)
            fingerGunPoseScript.SendMessage("ForceDisable", locked, SendMessageOptions.DontRequireReceiver);
    }

    // -------------------- Owner head hide --------------------
    private void ApplyOwnerHeadHide()
    {
        if (headRenderersForOwner != null)
            foreach (var smr in headRenderersForOwner)
                if (smr) smr.enabled = false;

        if (hideBonesForOwner != null && hideBonesForOwner.Length > 0 && _originalBoneScales == null)
        {
            _originalBoneScales = new Vector3[hideBonesForOwner.Length];
            for (int i = 0; i < hideBonesForOwner.Length; i++)
                _originalBoneScales[i] = hideBonesForOwner[i] ? hideBonesForOwner[i].localScale : Vector3.one;
        }

        if (hideBonesForOwner != null)
        {
            foreach (var b in hideBonesForOwner)
            {
                if (!b) continue;
                if (b == headBone) continue;
                b.localScale = Vector3.zero;
            }
        }

        if (hideRenderersForOwner != null)
            foreach (var r in hideRenderersForOwner)
                if (r) r.enabled = false;
    }

    // -------------------- Look + camera follow --------------------
    private void ReadMouse()
    {
        mouseX += Input.GetAxisRaw("Mouse X");
        mouseY += Input.GetAxisRaw("Mouse Y");
    }

    private void LateUpdate()
    {
        if (!IsOwner)
        {
            ApplyRemoteLookVisuals();
            return;
        }

        bool roundAllowsControls = RoundManager.RoundRunningClient || RoundOverUI.ControlsEnabled;
        bool lookAllowed = roundAllowsControls && !LocalPauseMenu.IsPauseOpen;
        if (!lookAllowed) return;

        // lock turning while scrambling/hanging/climbup
        if (_mode == ParkourMode.Scramble || _mode == ParkourMode.Hang || _mode == ParkourMode.ClimbUp)
        {
            // keep remote look/IK stable; don't rotate owner camera/pivot from input
            mouseX = 0f;
            mouseY = 0f;
            return;
        }
        if (controller == null || !controller.enabled) return;

        ApplyLook();
        TrySendLook();
        FollowHead_NoBob();
    }

    private void UpdateCursorState()
    {
        bool roundAllowsControls = RoundManager.RoundRunningClient || RoundOverUI.ControlsEnabled;
        bool gameplayActive = roundAllowsControls && !LocalPauseMenu.IsPauseOpen;

        Cursor.lockState = gameplayActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplayActive;
    }

    private void ApplyLook()
    {
        float mx = mouseX * mouseSensitivity;
        float my = mouseY * mouseSensitivity;

        mouseX = 0f;
        mouseY = 0f;

        bool altLook = Input.GetKey(altLookKeyRuntime);
        if (altLook)
        {
            cameraYawOffset += mx;
            cameraYawOffset = Mathf.Clamp(cameraYawOffset, -altLookYawLimit, altLookYawLimit);

            altPitch -= my * altLookPitchScale;
            altPitch = Mathf.Clamp(altPitch, -altLookPitchLimit, altLookPitchLimit);
        }
        else
        {
            if (_mode != ParkourMode.Hang)
            {
                yaw += mx;
                yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            }

            pitch -= my;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            cameraYawOffset = Mathf.MoveTowards(cameraYawOffset, 0f, altLookReturnSpeed * Time.deltaTime);
            altPitch = Mathf.MoveTowards(altPitch, 0f, altLookPitchReturnSpeed * Time.deltaTime);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        float mxForSway = altLook ? 0f : mx;
        float targetRoll = Mathf.Clamp(-mxForSway * turnSwayAmount, -turnSwayAmount, turnSwayAmount);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVel, turnSwaySmoothTime);

        float pivotPitch = altLook ? altPitch : pitch;

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pivotPitch, cameraYawOffset, roll);

        _localPivotPitch = pivotPitch;
        _localYawOffset = cameraYawOffset;
        _localRoll = roll;
    }

    private void FollowHead_NoBob()
    {
        if (cameraPivot == null || headBone == null) return;

        Vector3 targetWorldPos = headBone.TransformPoint(headLocalOffset);
        targetWorldPos += cameraPivot.forward * cameraForwardOffset;
        targetWorldPos += headBone.up * cameraUpOffset;

        if (headFollowSmoothing <= 0f)
            cameraPivot.position = targetWorldPos;
        else
            cameraPivot.position = Vector3.SmoothDamp(cameraPivot.position, targetWorldPos, ref camFollowVel, headFollowSmoothing);
    }

    // -------------------- FishNet look sync --------------------
    private void TrySendLook()
    {
        float now = Time.time;
        float interval = (lookSendRate <= 0f) ? 0.05f : (1f / lookSendRate);
        if (now < _nextLookSendTime) return;

        bool changed =
            Mathf.Abs(_localPivotPitch - _lastSentPivotPitch) > lookSendEpsilon ||
            Mathf.Abs(_localYawOffset - _lastSentYawOffset) > lookSendEpsilon ||
            Mathf.Abs(_localRoll - _lastSentRoll) > lookSendEpsilon;

        if (!changed) return;

        _nextLookSendTime = now + interval;

        _lastSentPivotPitch = _localPivotPitch;
        _lastSentYawOffset = _localYawOffset;
        _lastSentRoll = _localRoll;

        SendLookServerRpc(_localPivotPitch, _localYawOffset, _localRoll);
    }

    [ServerRpc(RequireOwnership = true)]
    private void SendLookServerRpc(float pivotPitch, float yawOffset, float rollDeg)
    {
        SendLookObserversRpc(pivotPitch, yawOffset, rollDeg);
    }

    [ObserversRpc(BufferLast = false)]
    private void SendLookObserversRpc(float pivotPitch, float yawOffset, float rollDeg)
    {
        if (IsOwner) return;
        _netPivotPitch = pivotPitch;
        _netYawOffset = yawOffset;
        _netRoll = rollDeg;
        _hasNetLook = true;
    }

    private void ApplyRemoteLookVisuals()
    {
        if (!_hasNetLook) return;
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(_netPivotPitch, _netYawOffset, _netRoll);
    }

    // -------------------- Debug helpers --------------------
    private void DbgLog(string msg)
    {
        if (!debugParkourLogs) return;
        if (Time.time - _lastDbgLogTime < debugLogThrottle) return;
        _lastDbgLogTime = Time.time;
        Debug.Log($"{msg} | ledge={ledgeFailReason} wallHit={dbg_wallHit} topHit={dbg_topHit} stand={dbg_standRoom} hang={dbg_hangRoom}", this);
    }

    // -------------------- Gizmos --------------------
    private void OnDrawGizmosSelected()
    {
        if (!debugParkourGizmos) return;

        var cc = controller != null ? controller : GetComponent<CharacterController>();
        if (cc == null) return;

        Gizmos.color = dbg_wallHit ? Color.green : Color.red;
        Gizmos.DrawWireSphere(_dbgProbeOrigin, 0.03f);
        Gizmos.DrawLine(_dbgProbeOrigin, _dbgProbeOrigin + _dbgProbeDir * ledgeGrabDistance);

        if (dbg_wallHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_dbgWallHit.point, 0.05f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_dbgWallHit.point, _dbgWallHit.point + _dbgWallHit.normal * 0.4f);

            Gizmos.color = dbg_topHit ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_dbgAbovePoint, 0.03f);
            Gizmos.DrawLine(_dbgAbovePoint, _dbgAbovePoint + Vector3.down * (1.75f));
        }

        if (dbg_topHit)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(_dbgTopHit.point, 0.05f);

            Gizmos.color = dbg_standRoom ? Color.green : Color.red;
            Gizmos.DrawSphere(_dbgStandCandidate, 0.06f);

            Gizmos.color = dbg_hangRoom ? new Color(0.3f, 0.9f, 1f, 1f) : Color.red;
            Gizmos.DrawSphere(_dbgHangCandidate, 0.06f);
        }

        if (_mode == ParkourMode.Hang)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_hangPos, 0.07f);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_standPos, 0.07f);
        }
    }

    //-------------------- Abilities --------------------
    [Server]
    public void ServerApplyImpulse(Vector3 impulse)
    {
        // impulse is treated like an "instant velocity add" for CC movement
        externalVelocity += impulse;

        // Clamp magnitude so it doesn't explode
        float mag = externalVelocity.magnitude;
        if (mag > maxExternalSpeed)
            externalVelocity = externalVelocity / mag * maxExternalSpeed;
    }
}