using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class StatusController : NetworkBehaviour
{
    private readonly SyncVar<bool> _isFrozen = new();
    private readonly SyncVar<bool> _isTagImmune = new();

    public bool IsFrozen => _isFrozen.Value;
    public bool IsTagImmune => _isTagImmune.Value;

    private Coroutine _freezeRoutine;
    private Coroutine _tagImmuneRoutine;

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

    [Server]
    public void ServerApplyTagImmunity(float duration)
    {
        _isTagImmune.Value = true;

        if (_tagImmuneRoutine != null)
            StopCoroutine(_tagImmuneRoutine);

        _tagImmuneRoutine = StartCoroutine(RemoveTagImmunityAfter(duration));
    }

    private IEnumerator RemoveTagImmunityAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        _isTagImmune.Value = false;
        _tagImmuneRoutine = null;
    }
}