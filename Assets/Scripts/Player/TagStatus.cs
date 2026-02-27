using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class TagStatus : NetworkBehaviour
{
    public readonly SyncVar<bool> IsIt = new SyncVar<bool>(false);

    [SerializeField] private Renderer[] colorTargets;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        IsIt.OnChange += OnTagChanged;
        ApplyColor(IsIt.Value);
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        IsIt.OnChange -= OnTagChanged;
    }

    private void OnTagChanged(bool prev, bool next, bool asServer)
    {
        ApplyColor(next);
    }

    private void ApplyColor(bool isIt)
    {
        if (colorTargets == null || colorTargets.Length == 0)
            colorTargets = GetComponentsInChildren<Renderer>();

        var c = isIt ? Color.red : Color.blue;
        foreach (var r in colorTargets)
            if (r != null && r.material != null)
                r.material.color = c;
    }

    [Server]
    public void SetIt(bool value)
    {
        // ✅ don't re-apply same state
        if (IsIt.Value == value)
            return;

        IsIt.Value = value;
        Debug.Log($"[TagStatus][Server] SetIt({value}) on {gameObject.name}");
    }
}