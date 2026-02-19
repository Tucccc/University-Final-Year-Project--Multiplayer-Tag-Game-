using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;


public class LobbyPlayerState : NetworkBehaviour
{
    public readonly SyncVar<bool> IsReady = new SyncVar<bool>(false);

    public Toggle readyToggle;


    private void Awake()
    {
        readyToggle = GameObject.Find("ReadyToggle").GetComponent<Toggle>();

    }
    public void RequestSetReady(bool ready)
    {
        if (!IsOwner)
            return;

        //stops if not connected or if client manager is not started (e.g. still in lobby scene and not loaded game scene yet)
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.ClientManager.Started)
        {
            readyToggle.interactable = false; // disable toggle if not connected, optional but good UX
            return;
        }

        SetReadyServerRpc(ready);
        readyToggle.interactable = true; // re-enable toggle if it was disabled due to not being connected, optional but good UX
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
