// Scripts/Player/Weapon.cs
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
    [SerializeField] private LayerMask hitMask = ~0; // set this to Player layer in Inspector for player-only

    private Coroutine _assignRoutine;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
        {
            aimCamera = null;
            return;
        }

        // Start assigning after spawn, not in Awake.
        if (_assignRoutine != null)
            StopCoroutine(_assignRoutine);

        _assignRoutine = StartCoroutine(AssignCrosshairUntilFound());
    }

    private IEnumerator AssignCrosshairUntilFound()
    {
        // wait a frame so UI has a chance to instantiate
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
        // 1) Best: search under this player's camera hierarchy (common if UI is attached to camera/player)
        if (aimCamera != null)
        {
            var t = aimCamera.transform;

            // look for exact name
            var child = t.Find(crosshairObjectName);
            if (child != null)
            {
                var ri = child.GetComponent<RawImage>();
                if (ri != null) return ri;
            }

            // or anywhere under the camera
            var allUnderCam = t.GetComponentsInChildren<RawImage>(true);
            foreach (var ri in allUnderCam)
                if (ri.name == crosshairObjectName) return ri;
        }

        // 2) Fallback: find any RawImage named Crosshair in the scene (including inactive)
        // NOTE: This is safe now because we avoid static; each client process will find its own UI.
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
            return; // still not found; coroutine will keep trying

        UpdateCrosshairVisibility();

        if (!crosshair.enabled)
            return;

        UpdateCrosshair();

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 dir = aimCamera.transform.forward;
            Vector3 origin = aimCamera.transform.position + dir * muzzleForwardOffset;

            Debug.DrawRay(origin, dir * maxDistance, Color.red, 0.2f);
            ShootServerRpc(origin, dir);
        }
    }

    private void UpdateCrosshair()
    {
        Vector3 dir = aimCamera.transform.forward;
        Vector3 origin = aimCamera.transform.position + dir * muzzleForwardOffset;

        bool hitPlayer = Physics.Raycast(origin, dir, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
        crosshair.color = hitPlayer ? crosshairHitColor : crosshairDefaultColor;
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
        var hits = Physics.RaycastAll(origin, direction, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform myRoot = transform.root;
        var attacker = myRoot.GetComponent<TagStatus>();
        if (attacker == null) return;

        foreach (var h in hits)
        {
            if (h.collider.transform.root == myRoot)
                continue;

            var target = h.collider.GetComponentInParent<TagStatus>();
            if (target != null)
            {
                var rm = RoundManager.Instance;

                if (RoundManager.IsRoundRunning && attacker.IsIt.Value && target != attacker)
                {
                    attacker.SetIt(false);
                    target.SetIt(true);

                    var newItConn = target.NetworkObject?.Owner;
                    rm.NotifyHandoff(newItConn);
                }

                PlayHitEffectObserversRpc(h.point, h.normal);
            }
            else
            {
                PlayHitEffectObserversRpc(h.point, h.normal);
            }

            break;
        }
    }

    [ObserversRpc]
    private void PlayHitEffectObserversRpc(Vector3 point, Vector3 normal)
    {
        // optional: spawn little spark / decal
    }
}