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
        Transform impactRoot = null;
        Collider impactCollider = null;
        bool foundImpact = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            // Skip self colliders/hitboxes.
            if (h.collider.transform.root == shooterRoot)
                continue;

            impactPoint = h.point;
            impactNormal = h.normal;
            impactCollider = h.collider;
            impactRoot = h.collider.transform.root;
            foundImpact = true;

            Debug.Log($"[Executor][SERVER] Blast impact hit '{h.collider.name}' at {impactPoint}");
            break;
        }

        if (!foundImpact)
        {
            Debug.Log("[Executor][SERVER] BlastShot: only hit self colliders.");
            return false;
        }

        // 2) Find affected colliders in radius
        float radius = def.blastRadius;
        if (radius <= 0.001f)
        {
            Debug.LogWarning("[Executor][SERVER] BlastShot: blastRadius is 0.");
            return true;
        }

        var cols = Physics.OverlapSphere(impactPoint, radius, affectMask, QueryTriggerInteraction.Collide);
        Debug.Log($"[Executor][SERVER] Blast overlap: radius={radius} colliders={(cols == null ? 0 : cols.Length)}");

        if (cols == null || cols.Length == 0)
            return true;

        // 3) Deduplicate roots + pick a "best collider" per root (better target point)
        var rootToBestCol = new System.Collections.Generic.Dictionary<Transform, Collider>();
        foreach (var c in cols)
        {
            if (c == null) continue;
            Transform root = c.transform.root;

            if (!rootToBestCol.TryGetValue(root, out var best) || best == null)
            {
                rootToBestCol[root] = c;
            }
            else
            {
                // Prefer larger bounds (usually body) instead of tiny hitboxes
                if (c.bounds.size.sqrMagnitude > best.bounds.size.sqrMagnitude)
                    rootToBestCol[root] = c;
            }
        }

        // 4) Apply effects
        foreach (var kvp in rootToBestCol)
        {
            Transform targetRoot = kvp.Key;
            Collider targetCol = kvp.Value;
            if (targetRoot == null || targetCol == null) continue;

            bool isSelf = (targetRoot == shooterRoot);

            // --- Choose a good target point (CC center is best) ---
            Vector3 targetPoint = targetCol.bounds.center;
            var cc = targetRoot.GetComponentInChildren<CharacterController>();
            if (cc != null)
                targetPoint = cc.transform.TransformPoint(cc.center);

            // --- Occlusion check: don't blast through walls ---
            // Ray from blast center to target point. If something blocks and isn't part of targetRoot => blocked.
            Vector3 toTarget = targetPoint - impactPoint;
            float distToTarget = toTarget.magnitude;

            if (distToTarget > 0.05f)
            {
                Vector3 toTargetDir = toTarget / distToTarget;
                Vector3 occStart = impactPoint + toTargetDir * 0.05f;

                if (Physics.Raycast(occStart, toTargetDir, out RaycastHit blockHit, distToTarget - 0.05f, hitMask, QueryTriggerInteraction.Ignore))
                {
                    if (blockHit.collider != null && blockHit.collider.transform.root != targetRoot)
                    {
                        // Blocked by world/other object. Skip (or you can reduce force instead of skipping)
                        continue;
                    }
                }
            }

            // --- Direction logic ---
            // If the initial impact was directly on THIS target, use -impactNormal (true push away from the hit surface).
            // Otherwise use explosion center -> target center.
            bool directHitThisTarget = (impactRoot != null && impactRoot == targetRoot);

            Vector3 impulseDir;
            if (directHitThisTarget)
            {
                impulseDir = (-impactNormal).normalized;
            }
            else
            {
                Vector3 away = (targetPoint - impactPoint);
                if (away.sqrMagnitude < 0.0001f)
                    away = -impactNormal;

                impulseDir = away.normalized;
            }

            // Optional: add "uplift" only when we're not strongly slamming down.
            if (def.blastUpLift > 0f)
            {
                float verticalDot = Vector3.Dot(impulseDir, Vector3.up); // -1 = down, +1 = up
                if (verticalDot > -0.25f)
                    impulseDir = (impulseDir + Vector3.up * def.blastUpLift).normalized;
            }

            // Optional: distance falloff so edge of radius is weaker (feels nicer)
            float falloff = 1f;
            if (radius > 0.0001f)
            {
                float t = Mathf.Clamp01(distToTarget / radius);
                falloff = 1f - t; // linear falloff
            }

            float force = def.blastForce * falloff * (isSelf ? def.selfForceMultiplier : 1f);
            Vector3 impulse = impulseDir * force;

            // Debug so you can verify direction
            Debug.Log($"[Blast] target={targetRoot.name} directHit={directHitThisTarget} dir={impulseDir} force={force:F2} impulse={impulse}");

            if (isSelf)
            {
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

            var rag = targetRoot.GetComponentInChildren<NetworkRagdollStun>();
            if (rag != null && !rag.IsRagdolled)
            {
                rag.ServerStunAndLaunch(impulse, def.ragdollDuration);
                Debug.Log($"[Executor][SERVER] Blast RAGDOLL '{targetRoot.name}' impulse={impulse} dur={def.ragdollDuration}");
            }
            else
            {
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