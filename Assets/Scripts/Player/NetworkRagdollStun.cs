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

    [Header("Recovery")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastUp = 1.0f;
    [SerializeField] private float groundExtra = 0.05f;
    [SerializeField] private float inputDelayAfterRecover = 0.08f;

    [Header("Safety")]
    [SerializeField] private float maxImpulse = 12f;
    [SerializeField] private float taggedRagdollSeconds = 1.2f;
    public float TaggedRagdollSeconds => taggedRagdollSeconds;

    [Header("Ragdoll Root Follow (Camera Follow Fix)")]
    [Tooltip("Usually leave null to use this.transform (player root). Root will follow hips while ragdolled so camera moves too.")]
    [SerializeField] private Transform followRoot;
    [SerializeField] private float followRootLerp = 25f;

    [Header("Self Ragdoll Test")]
    [SerializeField] private bool enableSelfRagdollTest = true;
    [SerializeField] private KeyCode selfRagdollKey = KeyCode.K;
    [SerializeField] private float selfRagdollSeconds = 1.2f;
    [SerializeField] private float selfImpulseUp = 2.5f;

    public bool IsRagdolled { get; private set; }

    private Coroutine _routine;

    // Disable root/extra colliders while ragdolled (prevents self-collision explosions)
    private Collider[] _allColliders;
    private bool[] _colliderWasEnabled;

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>(true);
        netAnimator = GetComponent<NetworkAnimator>();
        netTransforms = GetComponentsInChildren<NetworkTransform>(true);

        followRoot = transform;
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

        if (followRoot == null)
            followRoot = transform;

        // Start clean.
        SetRagdoll(false);
        SetControlEnabled(true);
        IsRagdolled = false;
    }

    private void Update()
    {
        // ✅ Press K to ragdoll yourself (owner only) for testing.
        if (!enableSelfRagdollTest) return;
        if (!IsOwner) return;

        if (Input.GetKeyDown(selfRagdollKey))
            SelfRagdollServerRpc();
    }

    private void LateUpdate()
    {
        if (!IsRagdolled) return;
        if (hipsRigidbody == null) return;
        if (followRoot == null) return;

        // Follow hips, but clamp to ground so we don't go under the map.
        Vector3 grounded = SnapToGround(hipsRigidbody.position);
        followRoot.position = Vector3.Lerp(followRoot.position, grounded, followRootLerp * Time.deltaTime);
    }

    [ServerRpc]
    private void SelfRagdollServerRpc()
    {
        // Behaves like real tag: server triggers observers.
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

    [ObserversRpc(BufferLast = false)]
    private void StartRagdollObserversRpc(Vector3 impulseWorld, float durationSeconds)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RagdollRoutine(impulseWorld, durationSeconds));
    }

    private IEnumerator RagdollRoutine(Vector3 impulseWorld, float durationSeconds)
    {
        IsRagdolled = true;

        // ✅ Stop network drivers fighting physics
        if (netTransforms != null)
            foreach (var nt in netTransforms)
                if (nt != null) nt.enabled = false;

        if (netAnimator != null)
            netAnimator.enabled = false;

        // Enter ragdoll
        SetControlEnabled(false);

        // Disable any non-ragdoll colliders (prevents root/capsule/etc fighting ragdoll)
        DisableNonRagdollColliders();

        SetRagdoll(true);

        if (maxImpulse > 0f)
            impulseWorld = Vector3.ClampMagnitude(impulseWorld, maxImpulse);

        if (hipsRigidbody != null)
            hipsRigidbody.AddForce(impulseWorld, ForceMode.Impulse);

        yield return new WaitForSeconds(durationSeconds);

        Vector3 hipsPos = hipsRigidbody != null ? hipsRigidbody.position : transform.position;

        // Turn ragdoll off
        SetRagdoll(false);

        yield return null;

        // Snap root to hips, then sync physics
        if (followRoot == null) followRoot = transform;
        followRoot.position = SnapToGround(hipsPos);
        Physics.SyncTransforms();

        // Restore colliders & control
        RestoreNonRagdollColliders();

        if (characterController != null) characterController.enabled = true;
        if (animator != null) animator.enabled = true;

        yield return new WaitForSeconds(inputDelayAfterRecover);

        if (movementScript != null) movementScript.enabled = true;

        // ✅ Resume networking after stable
        if (netAnimator != null)
            netAnimator.enabled = true;

        if (netTransforms != null)
            foreach (var nt in netTransforms)
                if (nt != null) nt.enabled = true;

        _routine = null;
        IsRagdolled = false;
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
        if (ragdollColliders != null)
            foreach (var c in ragdollColliders)
                if (c) c.enabled = enabled;

        if (ragdollBodies != null)
            foreach (var rb in ragdollBodies)
                SetupBody(rb, enabled);

        // hips too
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

    [Server]
    public void ServerStunAndLaunch(Vector3 impulseWorld)
    {
        ServerStunAndLaunch(impulseWorld, taggedRagdollSeconds);
    }
}