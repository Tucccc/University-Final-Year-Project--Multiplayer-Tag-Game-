using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;


public class LobbyPlayerState : NetworkBehaviour
{
    public readonly SyncVar<bool> IsReady = new SyncVar<bool>(false);

    public void RequestSetReady(bool ready)
    {
        if (!IsOwner)
            return;

        SetReadyServerRpc(ready);
    }

    [ServerRpc]
    private void SetReadyServerRpc(bool ready)
    {
        IsReady.Value = ready;

        if (LobbyManager2.Instance != null)
            LobbyManager2.Instance.RecalculateAllReady();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        LobbyManager2.Instance?.Register(this);
        Debug.Log($"[LobbyPlayerState] OnStartServer. Owner={OwnerId}"); // or conn id if you store it

    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        LobbyManager2.Instance?.Unregister(this);
    }
}
