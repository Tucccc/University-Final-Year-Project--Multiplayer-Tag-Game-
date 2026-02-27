using FishNet.Object;
using UnityEngine;

public class AbilityExecutor : MonoBehaviour
{
    [SerializeField] private AbilityDatabase database;
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private LayerMask affectMask;

    private RoundManager _roundManager;

    private void Update()
    {
        if (_roundManager == null)
            _roundManager = FindObjectOfType<RoundManager>();
    }

    public bool ServerTryExecute(string abilityId, Vector3 origin, Vector3 aimDir, GameObject shooter)
    {
        if (database == null)
        {
            Debug.LogError("[Executor][SERVER] AbilityDatabase is NULL.");
            return false;
        }

        var def = database.GetById(abilityId);
        if (def == null) return false;

        switch (def.effectType)
        {
            case AbilityEffectType.Freeze:
                return ServerFreezeShot(def, origin, aimDir, shooter);

            case AbilityEffectType.Blast:
                return ServerBlastShot(def, origin, aimDir, shooter);

            default:
                return false;
        }
    }

    private bool ServerFreezeShot(AbilityDefinition def, Vector3 origin, Vector3 dir, GameObject shooter)
    {
        dir = dir.normalized;

        var hits = Physics.RaycastAll(origin, dir, def.range, hitMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[Executor][SERVER] FreezeShot: no hits.");
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform shooterRoot = shooter.transform.root;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            if (h.collider.transform.root == shooterRoot)
                continue;

            var status = h.collider.GetComponentInParent<StatusController>();
            if (status == null)
            {
                Debug.Log($"[Executor][SERVER] Hit '{h.collider.name}' but no StatusController, continuing...");
                continue;
            }

            Debug.Log($"[Executor][SERVER] Freezing '{status.gameObject.name}' for {def.freezeDuration}s");
            status.ServerApplyFreeze(def.freezeDuration);
            return true;
        }

        Debug.Log("[Executor][SERVER] FreezeShot: no valid StatusController targets hit.");
        return false;
    }

    private bool ServerBlastShot(AbilityDefinition def, Vector3 origin, Vector3 dir, GameObject shooter)
    {
        dir = dir.normalized;

        // 1) Find impact point (first non-self hit).
        var hits = Physics.RaycastAll(origin, dir, def.range, hitMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[Executor][SERVER] BlastShot: no hits.");
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform shooterRoot = shooter.transform.root;

        Vector3 impactPoint = default;
        Vector3 impactNormal = Vector3.up;

        bool foundImpact = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            // Skip self colliders/hitboxes.
            if (h.collider.transform.root == shooterRoot)
                continue;

            impactPoint = h.point;
            impactNormal = h.normal;
            foundImpact = true;
            Debug.Log($"[Executor][SERVER] Blast impact hit '{h.collider.name}' at {impactPoint}");
            break;
        }

        if (!foundImpact)
        {
            Debug.Log("[Executor][SERVER] BlastShot: only hit self colliders.");
            return false;
        }


        // 2) Find affected colliders in radius (players only recommended).
        float radius = def.blastRadius;
        if (radius <= 0.001f)
        {
            Debug.LogWarning("[Executor][SERVER] BlastShot: blastRadius is 0.");
            return true; // impact happened but nothing to affect
        }

        var cols = Physics.OverlapSphere(impactPoint, radius, affectMask, QueryTriggerInteraction.Collide);
        Debug.Log($"[Executor][SERVER] Blast overlap: radius={radius} colliders={(cols == null ? 0 : cols.Length)}");

        if (cols == null || cols.Length == 0)
            return true;

        // 3) Deduplicate players by root transform.
        var uniqueRoots = new System.Collections.Generic.HashSet<Transform>();
        foreach (var c in cols)
        {
            if (c == null) continue;
            uniqueRoots.Add(c.transform.root);
        }

        // 4) Apply effects.
        foreach (var targetRoot in uniqueRoots)
        {
            if (targetRoot == null) continue;

            bool isSelf = (targetRoot == shooterRoot);

            // Direction away from blast center + optional uplift.
            Vector3 away = targetRoot.position - impactPoint;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.up;
            away = away.normalized;

            Vector3 impulseDir = (away + Vector3.up * def.blastUpLift).normalized;

            float force = def.blastForce * (isSelf ? def.selfForceMultiplier : 1f);
            Vector3 impulse = impulseDir * force;

            if (isSelf)
            {
                // CharacterController launch: use your movement script external impulse.
                var move = targetRoot.GetComponentInChildren<PlayerMovement>();
                if (move != null)
                {
                    move.AddExternalImpulse(impulse);
                    Debug.Log($"[Executor][SERVER] Blast SELF launch '{targetRoot.name}' impulse={impulse}");
                }
                else
                {
                    Debug.LogWarning($"[Executor][SERVER] Blast SELF: PlayerMovement not found on '{targetRoot.name}'.");
                }

                continue;
            }

            // Other players: ragdoll + launch (your existing system).
            var rag = targetRoot.GetComponentInChildren<NetworkRagdollStun>();
            if (rag != null && !rag.IsRagdolled)
            {
                rag.ServerStunAndLaunch(impulse, def.ragdollDuration);
                Debug.Log($"[Executor][SERVER] Blast RAGDOLL '{targetRoot.name}' impulse={impulse} dur={def.ragdollDuration}");
            }
            else
            {
                // Fallback: if no ragdoll component, try applying impulse via CC movement too.
                var move = targetRoot.GetComponentInChildren<PlayerMovement>();
                if (move != null)
                {
                    move.AddExternalImpulse(impulse);
                    Debug.Log($"[Executor][SERVER] Blast target (no ragdoll) CC impulse '{targetRoot.name}' impulse={impulse}");
                }
                else
                {
                    Debug.LogWarning($"[Executor][SERVER] Blast target '{targetRoot.name}' has no ragdoll and no PlayerMovement.");
                }
            }
        }

        _roundManager.PlayBlastImpactObserversRpc(impactPoint, impactNormal);

        return true;
    }



}