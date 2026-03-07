using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;

public class TagStatus : NetworkBehaviour
{
    public readonly SyncVar<bool> IsIt = new SyncVar<bool>(false);
    public readonly SyncVar<bool> canBeTagged = new SyncVar<bool>(true);

    [SerializeField] private Renderer[] colorTargets;


    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        IsIt.OnChange += OnTagChanged;
        canBeTagged.OnChange += OnCanBeTaggedChanged;
        //ApplyColor(IsIt.Value);
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        IsIt.OnChange -= OnTagChanged;
        canBeTagged.OnChange -= OnCanBeTaggedChanged;

    }

    private void OnCanBeTaggedChanged(bool prev, bool next, bool asServer)
    {
        Debug.Log($"{gameObject.name} CanBeTagged = {next}");
    }

    private void OnTagChanged(bool prev, bool next, bool asServer)
    {
        //ApplyColor(next);
    }

    //private void ApplyColor(bool isIt)
    //{
    //    if (colorTargets == null || colorTargets.Length == 0)
    //        colorTargets = GetComponentsInChildren<Renderer>();

    //    var c = isIt ? Color.red : Color.blue;
    //    foreach (var r in colorTargets)
    //        if (r != null && r.material != null)
    //            r.material.color = c;
    //}

    [Server]
    public void SetIt(bool value)
    {
        // ✅ don't re-apply same state
        if (IsIt.Value == value)
            return;

        IsIt.Value = value;
        Debug.Log($"[TagStatus][Server] SetIt({value}) on {gameObject.name}");
    }

    [Server]
    public void SetCanBeTagged(bool value)
    {
        if (canBeTagged.Value == value)
            return;

        canBeTagged.Value = value;
    }

    [Server]
    public void MakeUntaggableForSeconds(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(UntaggableRoutine(duration));
    }

    [Server]
    private IEnumerator UntaggableRoutine(float duration)
    {
        SetCanBeTagged(false);
        yield return new WaitForSeconds(duration);
        SetCanBeTagged(true);
    }
}