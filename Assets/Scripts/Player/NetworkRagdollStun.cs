using FishNet.Object;
using UnityEngine;
using System.Collections;
using FishNet.Component.Transforming;
using FishNet.Component.Animating;

public class NetworkRagdollStun : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private MonoBehaviour movementScript; // PlayerMovement
    [SerializeField] private Animator animator;

    [Header("Networking (IMPORTANT)")]
    [Tooltip("All NetworkTransforms in this player hierarchy will be disabled while ragdolled.")]
    [SerializeField] private NetworkTransform[] netTransforms;
    [Tooltip("NetworkAnimator will be disabled while ragdolled.")]
    [SerializeField] private NetworkAnimator netAnimator;

    [Header("Ragdoll Bones")]
    [SerializeField] private Rigidbody hipsRigidbody;
    [SerializeField] private Rigidbody[] ragdollBodies;
    [SerializeField] private Collider[] ragdollColliders;

    [Header("Recovery Grounding")]
    [Tooltip("If true: recovery timer only 'starts' once the ragdoll is grounded & stable.")]
    [SerializeField] private bool requireGroundedToRecover = true;

    [Tooltip("How long the ragdoll must stay grounded & stable before we recover.")]
    [SerializeField] private float groundedStableSeconds = 0.60f;

    [Tooltip("Safety: maximum extra time to wait for grounding before forcing recovery.")]
    [SerializeField] private float maxWaitForGroundSeconds = 2.00f;

    [Tooltip("Extra lift applied when standing up from ragdoll (your knob).")]
    [SerializeField] private float recoverExtraUp = 0.15f;

    [Tooltip("If the stand-up position overlaps something, we step upward by this much until clear.")]
    [SerializeField] private float recoverOverlapLiftStep = 0.10f;

    [Tooltip("Maximum total upward lift we will try to resolve overlap.")]
    [SerializeField] private float recoverMaxLift = 1.50f;

    [Tooltip("Ragdoll is considered stable when hips velocity is below this.")]
    [SerializeField] private float stableHipsSpeed = 0.45f;

    [Header("Recovery (Ground Raycast)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastUp = 1.0f;
    [SerializeField] private float groundExtra = 0.05f;
    [SerializeField] private float inputDelayAfterRecover = 0.08f;

    [Header("Safety")]
    [SerializeField] private float maxImpulse = 12f;
    [SerializeField] private float taggedRagdollSeconds = 1.6f;
    public float TaggedRagdollSeconds => taggedRagdollSeconds;

    [Header("Camera Follow While Ragdolled")]
    [Tooltip("IMPORTANT: this must NOT be a parent of ragdoll bones (not the player root). Use a separate camera rig/holder.")]
    [SerializeField] private Transform followRoot;
    [SerializeField] private float followRootLerp = 25f;

    [Header("Momentum Preserve (CharacterController-friendly)")]
    [Tooltip("Estimate root velocity from position delta and transfer it into ragdoll rigidbodies.")]
    [SerializeField] private bool inheritEstimatedRootVelocity = true;

    [Tooltip("Multiplier for estimated velocity transferred to ragdoll.")]
    [SerializeField] private float estimatedVelocityMultiplier = 1f;

    [Tooltip("Clamp for estimated velocity magnitude (prevents insane speeds).")]
    [SerializeField] private float maxEstimatedSpeed = 60f;

    [Tooltip("Smoothing for estimated velocity (higher = more responsive).")]
    [SerializeField] private float velocitySmoothing = 25f;

    [Tooltip("If PlayerMovement provides GetRagdollInheritVelocity(), blend it with estimate.")]
    [SerializeField] private bool alsoUseMovementProvidedVelocity = true;

    [Range(0f, 1f)]
    [Tooltip("0 = only estimate, 1 = only movement-provided. 0.5 = blend.")]
    [SerializeField] private float movementVelocityBlend = 0.5f;

    [Header("Self Ragdoll Test")]
    [SerializeField] private bool enableSelfRagdollTest = true;
    [SerializeField] private KeyCode selfRagdollKey = KeyCode.K;
    [SerializeField] private float selfRagdollSeconds = 1.2f;
    [SerializeField] private float selfImpulseUp = 2.5f;

    [SerializeField] private Behaviour rootNetworkTransform; // drag your FishNet NetworkTransform here

    public bool IsRagdolled { get; private set; }

    private Coroutine _routine;

    private Collider[] _allColliders;
    private bool[] _colliderWasEnabled;

    private Vector3 _hipsToRootOffset;

    // Estimated velocity tracking (for CC momentum preserve)
    private Vector3 _lastRootPos;
    private Vector3 _estimatedRootVel;

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>(true);
        netAnimator = GetComponent<NetworkAnimator>();
        netTransforms = GetComponentsInChildren<NetworkTransform>(true);
    }

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        if (netAnimator == null) netAnimator = GetComponent<NetworkAnimator>();
        if (netTransforms == null || netTransforms.Length == 0)
            netTransforms = GetComponentsInChildren<NetworkTransform>(true);

        _allColliders = GetComponentsInChildren<Collider>(true);
        _colliderWasEnabled = new bool[_allColliders.Length];

        _lastRootPos = transform.position;
        _estimatedRootVel = Vector3.zero;

        SetRagdoll(false);
        SetControlEnabled(true);
        IsRagdolled = false;
    }

    private void Update()
    {
        if (!IsRagdolled && inheritEstimatedRootVelocity)
        {
            float dt = Time.deltaTime;
            if (dt > 0.0001f)
            {
                Vector3 raw = (transform.position - _lastRootPos) / dt;
                _estimatedRootVel = Vector3.Lerp(_estimatedRootVel, raw, velocitySmoothing * dt);
                _lastRootPos = transform.position;
            }
        }

        if (!enableSelfRagdollTest) return;
        if (!IsOwner) return;

        if (Input.GetKeyDown(selfRagdollKey))
            SelfRagdollServerRpc();
    }

    private void LateUpdate()
    {
        if (!IsRagdolled) return;
        if (hipsRigidbody == null) return;

        // ✅ DO NOT move the player root while ragdolled (that teleports ragdoll and causes spazz).
        // Only move followRoot if it is NOT an ancestor of hips/ragdoll (camera rig object).
        if (followRoot != null && !IsAncestorOfHips(followRoot))
        {
            Vector3 target = SnapToGround(hipsRigidbody.position);
            followRoot.position = Vector3.Lerp(followRoot.position, target, followRootLerp * Time.deltaTime);
        }
    }

    private bool IsAncestorOfHips(Transform t)
    {
        if (hipsRigidbody == null) return false;
        Transform hipsT = hipsRigidbody.transform;
        return hipsT == t || hipsT.IsChildOf(t);
    }

    [ServerRpc]
    private void SelfRagdollServerRpc()
    {
        Vector3 impulse = Vector3.up * selfImpulseUp;
        ServerStunAndLaunch(impulse, selfRagdollSeconds);
    }

    [Server]
    public void ServerStunAndLaunch(Vector3 impulseWorld, float durationSeconds)
    {
        if (IsRagdolled)
            return;

        StartRagdollObserversRpc(impulseWorld, durationSeconds);
    }

    [Server]
    public void ServerStunAndLaunch(Vector3 impulseWorld)
    {
        ServerStunAndLaunch(impulseWorld, taggedRagdollSeconds);
    }

    [ObserversRpc(BufferLast = false)]
    private void StartRagdollObserversRpc(Vector3 impulseWorld, float durationSeconds)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RagdollRoutine(impulseWorld, durationSeconds));
    }

    private IEnumerator RagdollRoutine(Vector3 impulseWorld, float durationSeconds)
    {
        IsRagdolled = true;

        // Cache offset so recovery places the root where the hips ended up.
        if (hipsRigidbody != null)
            _hipsToRootOffset = transform.position - hipsRigidbody.position;

        // Stop network drivers fighting physics
        if (netTransforms != null)
            foreach (var nt in netTransforms)
                if (nt != null) nt.enabled = false;

        if (netAnimator != null)
            netAnimator.enabled = false;

        // ✅ Capture current momentum BEFORE disabling CC/movement
        Vector3 inheritV = Vector3.zero;

        if (inheritEstimatedRootVelocity)
        {
            inheritV = _estimatedRootVel * estimatedVelocityMultiplier;
            inheritV = Vector3.ClampMagnitude(inheritV, maxEstimatedSpeed);
        }

        if (alsoUseMovementProvidedVelocity && movementScript != null)
        {
            Vector3 mv = TryGetMovementProvidedVelocity();
            // Blend: 0 = only estimate, 1 = only movement-provided
            inheritV = Vector3.Lerp(inheritV, mv, movementVelocityBlend);
            inheritV = Vector3.ClampMagnitude(inheritV, maxEstimatedSpeed);
        }

        // Enter ragdoll
        SetControlEnabled(false);
        DisableNonRagdollColliders();
        SetRagdoll(true);

        // ✅ Seed ragdoll rigidbodies with inherited velocity so we DON'T lose momentum.
        ApplyVelocityToRagdoll(inheritV);

        // Apply impulse (blast) on top
        if (maxImpulse > 0f)
            impulseWorld = Vector3.ClampMagnitude(impulseWorld, maxImpulse);

        if (hipsRigidbody != null)
            hipsRigidbody.AddForce(impulseWorld, ForceMode.Impulse);

        // Base stun time
        yield return new WaitForSeconds(durationSeconds);

        // Wait until grounded & stable for a short time (optional)
        if (requireGroundedToRecover && hipsRigidbody != null)
        {
            float stableTimer = 0f;
            float waitTimer = 0f;

            while (waitTimer < maxWaitForGroundSeconds && stableTimer < groundedStableSeconds)
            {
                waitTimer += Time.deltaTime;

                bool grounded = IsHipsGrounded(hipsRigidbody.position);
                float speed = hipsRigidbody.linearVelocity.magnitude;

                if (grounded && speed <= stableHipsSpeed)
                    stableTimer += Time.deltaTime;
                else
                    stableTimer = 0f;

                yield return null;
            }
        }

        // Capture hips position (last moment before disabling ragdoll)
        Vector3 hipsPos = hipsRigidbody != null ? hipsRigidbody.position : transform.position;

        // Turn ragdoll off (bodies kinematic), but DO NOT enable CC yet until we place safely
        SetRagdoll(false);
        yield return null;

        // Compute safe recovery root position (hips + cached offset)
        Vector3 target = SnapToGround(hipsPos + _hipsToRootOffset);
        target += Vector3.up * Mathf.Max(0f, recoverExtraUp);

        if (characterController != null)
        {
            bool ccWasEnabled = characterController.enabled;
            characterController.enabled = false;

            target = ResolveRecoveryOverlap(target);

            // Move REAL root ONCE at recovery
            transform.position = target;
            Physics.SyncTransforms();

            characterController.enabled = ccWasEnabled;
        }
        else
        {
            transform.position = target;
            Physics.SyncTransforms();
        }

        // Keep camera rig in sync after recovery too (safe)
        if (followRoot != null && !IsAncestorOfHips(followRoot))
            followRoot.position = transform.position;

        // Restore colliders & control
        RestoreNonRagdollColliders();

        if (characterController != null) characterController.enabled = true;
        if (animator != null) animator.enabled = true;

        yield return new WaitForSeconds(inputDelayAfterRecover);

        if (movementScript != null)
        {
            movementScript.enabled = true;
            // Clears stored velocity / slide / buffers (your bounce fix)
            movementScript.SendMessage("OnRagdollRecovered", SendMessageOptions.DontRequireReceiver);
        }

        // Resume networking after stable
        if (netAnimator != null)
            netAnimator.enabled = true;

        if (netTransforms != null)
            foreach (var nt in netTransforms)
                if (nt != null) nt.enabled = true;

        _routine = null;
        IsRagdolled = false;

        // reset estimator baseline
        _lastRootPos = transform.position;
        _estimatedRootVel = Vector3.zero;
    }

    private Vector3 TryGetMovementProvidedVelocity()
    {
        // If your movementScript is PlayerMovement, call directly (best)
        if (movementScript is PlayerMovement pm)
            return pm.GetRagdollInheritVelocity();

        // Otherwise reflect for method "GetRagdollInheritVelocity"
        var mi = movementScript.GetType().GetMethod(
            "GetRagdollInheritVelocity",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (mi != null && mi.ReturnType == typeof(Vector3))
        {
            object result = mi.Invoke(movementScript, null);
            if (result is Vector3 v) return v;
        }

        return Vector3.zero;
    }

    private void ApplyVelocityToRagdoll(Vector3 v)
    {
        if (hipsRigidbody != null)
            hipsRigidbody.linearVelocity = v;

        if (ragdollBodies != null)
            foreach (var rb in ragdollBodies)
                if (rb != null) rb.linearVelocity = v;
    }

    private bool IsHipsGrounded(Vector3 hipsWorldPos)
    {
        Vector3 start = hipsWorldPos + Vector3.up * raycastUp;
        float dist = raycastUp + 2.5f;

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, dist, groundMask, QueryTriggerInteraction.Ignore))
        {
            float d = hipsWorldPos.y - hit.point.y;
            return d <= (0.35f + groundExtra);
        }

        return false;
    }

    private Vector3 ResolveRecoveryOverlap(Vector3 desiredRootPos)
    {
        if (characterController == null) return desiredRootPos;

        float radius = Mathf.Max(0.05f, characterController.radius);
        float height = Mathf.Max(radius * 2f, characterController.height);

        Vector3 center = desiredRootPos + characterController.center;

        Vector3 bottom = center + Vector3.down * (height * 0.5f - radius);
        Vector3 top = center + Vector3.up * (height * 0.5f - radius);

        float lifted = 0f;
        while (lifted <= recoverMaxLift)
        {
            bool blocked = Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            if (!blocked)
                return desiredRootPos;

            float step = Mathf.Max(0.01f, recoverOverlapLiftStep);
            desiredRootPos += Vector3.up * step;
            lifted += step;

            center = desiredRootPos + characterController.center;
            bottom = center + Vector3.down * (height * 0.5f - radius);
            top = center + Vector3.up * (height * 0.5f - radius);
        }

        return desiredRootPos;
    }

    private void SetControlEnabled(bool enabled)
    {
        if (movementScript != null) movementScript.enabled = enabled;
        if (characterController != null) characterController.enabled = enabled;
        if (animator != null) animator.enabled = enabled;
    }

    private void DisableNonRagdollColliders()
    {
        if (_allColliders == null) return;

        for (int i = 0; i < _allColliders.Length; i++)
        {
            var c = _allColliders[i];
            if (c == null) continue;

            _colliderWasEnabled[i] = c.enabled;

            bool isRagdollCol = false;
            if (ragdollColliders != null)
            {
                for (int r = 0; r < ragdollColliders.Length; r++)
                {
                    if (ragdollColliders[r] == c)
                    {
                        isRagdollCol = true;
                        break;
                    }
                }
            }

            if (!isRagdollCol)
                c.enabled = false;
        }
    }

    private void RestoreNonRagdollColliders()
    {
        if (_allColliders == null) return;

        for (int i = 0; i < _allColliders.Length; i++)
        {
            var c = _allColliders[i];
            if (c == null) continue;

            c.enabled = _colliderWasEnabled[i];
        }
    }

    private void SetRagdoll(bool enabled)
    {
        if (enabled)
        {
            if (rootNetworkTransform != null)
                rootNetworkTransform.enabled = false;

            if (characterController != null)
                characterController.enabled = false;
        }
        else
        {
            if (characterController != null)
                characterController.enabled = false; // keep off until recovery places us

            if (rootNetworkTransform != null)
                rootNetworkTransform.enabled = true;
        }

        if (ragdollColliders != null)
            foreach (var c in ragdollColliders)
                if (c) c.enabled = enabled;

        if (ragdollBodies != null)
            foreach (var rb in ragdollBodies)
                SetupBody(rb, enabled);

        SetupBody(hipsRigidbody, enabled);
    }

    private void SetupBody(Rigidbody rb, bool enabled)
    {
        if (!rb) return;

        if (!enabled)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        rb.isKinematic = !enabled;
        rb.useGravity = enabled;
    }

    private Vector3 SnapToGround(Vector3 nearPos)
    {
        Vector3 start = nearPos + Vector3.up * raycastUp;

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, raycastUp + 5f, groundMask, QueryTriggerInteraction.Ignore))
            return new Vector3(nearPos.x, hit.point.y + groundExtra, nearPos.z);

        return nearPos;
    }
}