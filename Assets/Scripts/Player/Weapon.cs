using UnityEngine;
using FishNet.Object;
using UnityEngine.UI;
using System.Collections;

public class Weapon : NetworkBehaviour
{
    [Header("CrossHair")]
    [SerializeField] private RawImage crosshair;
    [SerializeField] private string crosshairObjectName = "Crosshair";
    [SerializeField] private Color crosshairDefaultColor = Color.green;
    [SerializeField] private Color crosshairHitColor = Color.red;

    [Header("Aim Source")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float muzzleForwardOffset = 0.5f;

    [Header("Raycast")]
    [SerializeField] private float maxDistance = 200f;

    [Tooltip("✅ Include PlayerHitbox layer here (and whatever world layers you want to hit).")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Optional: Layers To Ignore")]
    [Tooltip("If you put ragdoll colliders on a 'Ragdoll' layer, put it here to avoid hitting them.")]
    [SerializeField] private LayerMask ignoreMask = 0;

    [Header("Owner Viewmodel (Hand IK)")]
    [SerializeField] private bool ownerUseHandIK = true;

    [Header("Tag Ragdoll")]
    [SerializeField] private bool enableTagRagdoll = true;
    [SerializeField] private float ragdollForce = 8f;
    [Range(0f, 2f)]
    [SerializeField] private float ragdollUpLift = 0.35f;
    [SerializeField] private float ragdollStunSeconds = 1.2f;

    [Header("Abilities")]
    public PlayerAbilityManager abilityManager;

    private FingerGunPose _fingerGunPose;
    private FingerGunHandIK _handIK;
    private Coroutine _assignRoutine;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>(true);

        _fingerGunPose = GetComponentInChildren<FingerGunPose>(true);
        _handIK = GetComponentInChildren<FingerGunHandIK>(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (_fingerGunPose == null)
            _fingerGunPose = GetComponentInChildren<FingerGunPose>(true);
        if (_handIK == null)
            _handIK = GetComponentInChildren<FingerGunHandIK>(true);

        if (IsOwner)
        {
            if (_handIK != null)
                _handIK.enableIK = ownerUseHandIK;
        }
        else
        {
            if (_handIK != null)
                _handIK.enableIK = false;
        }

        if (!IsOwner)
        {
            aimCamera = null;
            return;
        }

        if (_assignRoutine != null)
            StopCoroutine(_assignRoutine);

        _assignRoutine = StartCoroutine(AssignCrosshairUntilFound());
    }

    private IEnumerator AssignCrosshairUntilFound()
    {
        yield return null;

        while (crosshair == null)
        {
            crosshair = TryFindLocalCrosshair();

            if (crosshair != null)
            {
                crosshair.color = crosshairDefaultColor;
                yield break;
            }

            yield return null;
        }
    }

    private RawImage TryFindLocalCrosshair()
    {
        if (aimCamera != null)
        {
            var t = aimCamera.transform;

            var child = t.Find(crosshairObjectName);
            if (child != null)
            {
                var ri = child.GetComponent<RawImage>();
                if (ri != null) return ri;
            }

            var allUnderCam = t.GetComponentsInChildren<RawImage>(true);
            foreach (var ri in allUnderCam)
                if (ri.name == crosshairObjectName) return ri;
        }

        var all = FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ri in all)
            if (ri.name == crosshairObjectName)
                return ri;

        return null;
    }

    private void Update()
    {
        if (!IsOwner || aimCamera == null)
            return;

        if (crosshair == null)
            return;

        UpdateCrosshairVisibility();
        if (!crosshair.enabled)
            return;

        UpdateCrosshair();

        if (Input.GetMouseButtonDown(0))
        {
            PlayTagPoseLocal();

            Vector3 dir = aimCamera.transform.forward;
            Vector3 origin = aimCamera.transform.position + dir * muzzleForwardOffset;

            Debug.DrawRay(origin, dir * maxDistance, Color.red, 0.2f);

            RequestTagPoseServerRpc();
            ShootServerRpc(origin, dir);
        }



        if (Input.GetMouseButtonDown(1))
        {
            PlayTagPoseLocal();

            if (abilityManager == null)
            {
                Debug.LogWarning("[Weapon] Right click but abilityManager is NULL.");
                return;
            }

            RequestTagPoseServerRpc();

            Vector3 dir = aimCamera.transform.forward;
            Vector3 origin = aimCamera.transform.position + dir * muzzleForwardOffset;

            Debug.DrawRay(origin, dir * maxDistance, Color.cyan, 0.5f);
            Debug.Log($"[Weapon] Right click -> TryUseAbility origin={origin} dir={dir}");

            abilityManager.TryUseAbility(origin, dir);
        }
    }

    private void PlayTagPoseLocal()
    {
        if (_fingerGunPose == null)
            _fingerGunPose = GetComponentInChildren<FingerGunPose>(true);

        _fingerGunPose?.PlayFingerGunPose();
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestTagPoseServerRpc()
    {
        PlayTagPoseObserversRpc();
    }

    [ObserversRpc(BufferLast = false)]
    private void PlayTagPoseObserversRpc()
    {
        if (IsOwner) return;
        PlayTagPoseLocal();
    }

    private void UpdateCrosshair()
    {
        Vector3 dir = aimCamera.transform.forward;
        Vector3 origin = aimCamera.transform.position + dir * muzzleForwardOffset;

        int finalMask = hitMask & ~ignoreMask;

        // ✅ IMPORTANT: Collide with triggers so PlayerHitbox (IsTrigger) can be hit.
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, finalMask, QueryTriggerInteraction.Collide))
        {
            var target = hit.collider.GetComponentInParent<TagStatus>();
            crosshair.color = (target != null) ? crosshairHitColor : crosshairDefaultColor;
        }
        else
        {
            crosshair.color = crosshairDefaultColor;
        }
    }

    private void UpdateCrosshairVisibility()
    {
        bool cursorFree =
            Cursor.visible &&
            (Cursor.lockState == CursorLockMode.None || Cursor.lockState == CursorLockMode.Confined);

        crosshair.enabled = !cursorFree;

        if (cursorFree)
            crosshair.color = crosshairDefaultColor;
    }

    [ServerRpc(RequireOwnership = true)]
    private void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        int finalMask = hitMask & ~ignoreMask;

        // ✅ IMPORTANT: Collide with triggers so PlayerHitbox (IsTrigger) can be hit.
        var hits = Physics.RaycastAll(origin, direction, maxDistance, finalMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform myRoot = transform.root;
        var attacker = myRoot.GetComponent<TagStatus>();
        if (attacker == null) return;

        TagStatus target = null;
        RaycastHit targetHit = default;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            if (h.collider.transform.root == myRoot)
                continue;

            var maybeTarget = h.collider.GetComponentInParent<TagStatus>();
            if (maybeTarget != null)
            {
                target = maybeTarget;
                targetHit = h;
                break;
            }

            targetHit = h;
            break;
        }

        var rm = RoundManager.Instance;

        if (target != null)
        {
            if (RoundManager.IsRoundRunning && attacker.IsIt.Value && target != attacker)
            {
                attacker.SetIt(false);
                target.SetIt(true);

                var newItConn = target.NetworkObject?.Owner;
                rm.NotifyHandoff(newItConn);

                if (enableTagRagdoll)
                {
                    var rag = target.GetComponentInParent<NetworkRagdollStun>();
                    if (rag != null && !rag.IsRagdolled)
                    {
                        Vector3 dir = direction.normalized;
                        Vector3 impulse = (dir + Vector3.up * ragdollUpLift) * ragdollForce;
                        rag.ServerStunAndLaunch(impulse, rag.TaggedRagdollSeconds);
                    }
                }
            }

            PlayHitEffectObserversRpc(targetHit.point, targetHit.normal);
        }
        else
        {
            PlayHitEffectObserversRpc(targetHit.point, targetHit.normal);
        }
    }

    [ObserversRpc(BufferLast = false)]
    private void PlayHitEffectObserversRpc(Vector3 point, Vector3 normal)
    {
        // optional: spawn little spark / decal
    }
}