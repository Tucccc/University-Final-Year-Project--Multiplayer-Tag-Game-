// Scripts/Player/Weapon.cs
using UnityEngine;
using FishNet.Object;

public class Weapon : NetworkBehaviour
{
    [Header("Aim Source")]
    [SerializeField] private Camera aimCamera;                 // assign the player’s camera
    [SerializeField] private float muzzleForwardOffset = 0.5f; // push origin forward to avoid self-hit

    [Header("Raycast")]
    [SerializeField] private float maxDistance = 200f;         // arena range
    [SerializeField] private LayerMask hitMask = ~0;           // set to Player layer in Inspector

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            aimCamera = null; // never aim from non-owner camera
    }

    private void Update()
    {
        if (!IsOwner || aimCamera == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 dir = aimCamera.transform.forward;
            Vector3 origin = aimCamera.transform.position + dir * muzzleForwardOffset;

            Debug.DrawRay(origin, dir * maxDistance, Color.red, 0.2f);
            ShootServerRpc(origin, dir);
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        var hits = Physics.RaycastAll(origin, direction, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform myRoot = transform.root;
        var attacker = myRoot.GetComponent<TagStatus>();
        if (attacker == null) return;

        foreach (var h in hits)
        {
            // Skip our own colliders
            if (h.collider.transform.root == myRoot)
                continue;

            var target = h.collider.GetComponentInParent<TagStatus>();
            if (target != null)
            {
                var rm = RoundManager.Instance;

                if (RoundManager.IsRoundRunning && attacker.IsIt.Value && target != attacker)
                {
                    // handoff "It"
                    attacker.SetIt(false);
                    target.SetIt(true);

                    // notify RoundManager who is It now (Survivor mode needs this)
                    var newItConn = target.NetworkObject?.Owner;
                    rm.NotifyHandoff(newItConn);
                }

                PlayHitEffectObserversRpc(h.point, h.normal);
            }
            else
            {
                PlayHitEffectObserversRpc(h.point, h.normal);
            }

            break; // first valid non-self hit only
        }
    }

    [ObserversRpc]
    private void PlayHitEffectObserversRpc(Vector3 point, Vector3 normal)
    {
        // optional: spawn little spark / decal
    }
}
