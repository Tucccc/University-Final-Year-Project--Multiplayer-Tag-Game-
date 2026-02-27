using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class StatusController : NetworkBehaviour
{
    // FishNet v4 SyncVar<T> (no attribute)
    private readonly SyncVar<bool> _isFrozen = new();
    public bool IsFrozen => _isFrozen.Value;

    private Coroutine _freezeRoutine;

    [Server]
    public void ServerApplyFreeze(float duration)
    {
        _isFrozen.Value = true;

        if (_freezeRoutine != null)
            StopCoroutine(_freezeRoutine);

        _freezeRoutine = StartCoroutine(UnfreezeAfter(duration));
    }

    private IEnumerator UnfreezeAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        _isFrozen.Value = false;
        _freezeRoutine = null;
    }
}