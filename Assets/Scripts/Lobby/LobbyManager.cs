using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FishNet.Object;
using FishNet.Managing.Scened;
using FishNet.Connection;
using TMPro;

public class LobbyManager : NetworkBehaviour
{
    private readonly Dictionary<int, bool> _ready = new();
    public TextMeshProUGUI readyLable;

    private void Awake()
    {
        readyLable = GameObject.Find("ReadyLabel")?.GetComponent<TextMeshProUGUI>();

    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Clients is a Dictionary<int, NetworkConnection> in older FishNet
        foreach (KeyValuePair<int, NetworkConnection> kvp in NetworkManager.ServerManager.Clients)
            _ready[kvp.Key] = false;

        PushReadyLabel();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        _ready.Clear();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready, NetworkConnection caller = null)
    {
        if (caller == null) return;
        _ready[caller.ClientId] = ready;
        PushReadyLabel();
    }

    public void OnStartButtonPressed()
    {
        if (NetworkManager.IsServerStarted)
            StartGameServer();
    }

    [Server]
    private void StartGameServer()
    {
        if (!NetworkManager.IsServerStarted)
            return;

        if (_ready.Count == 0 || _ready.Values.Any(v => v == false))
        {
            Debug.Log("[Lobby] Not all players are ready.");
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkManager.ServerManager.Despawn(NetworkObject);

        var sld = new SceneLoadData("Arena")
        {
            ReplaceScenes = ReplaceOption.All
        };
        NetworkManager.SceneManager.LoadGlobalScenes(sld);
        Debug.Log("[Lobby] Loading Arena via FishNet SceneManager.");
    }

    public void PushReadyLabel()
    {
        Debug.Log("[Lobby] Pushing ready label update.");
        int total = Mathf.Max(_ready.Count, NetworkManager?.ServerManager?.Clients?.Count ?? 0);
        int ready = _ready.Values.Count(v => v);
        UpdateReadyLabelObserversRpc(ready, total);
        readyLable.text = $"Ready: {ready}/{total}";
    }

    [ObserversRpc]
    private void UpdateReadyLabelObserversRpc(int ready, int total)
    {
        Debug.Log("[Lobby] Updating ready label on clients.");
        ReadyLabel.NotifyAll(ready, total);
    }

    // Let a non-host client ask the host to start the match.
    [ServerRpc(RequireOwnership = false)]
    public void RequestStartMatchServerRpc(FishNet.Connection.NetworkConnection caller = null)
    {
        // Only host should actually start the match.
        if (NetworkManager.IsServerStarted)
            StartGameServer();
    }

    public void PlayerJoin()
    {
        if (!IsServer) return;
        _ready[NetworkManager.ServerManager.Clients.Count + 1] = false;
        PushReadyLabel();
        UpdateReadyLabelObserversRpc(_ready.Values.Count(v => v), _ready.Count);
    }

    public void PlayerLeave()
    {
        if (!IsServer) return;
        _ready[NetworkManager.ServerManager.Clients.Count - 1] = true;
        PushReadyLabel();
        UpdateReadyLabelObserversRpc(_ready.Values.Count(v => v), _ready.Count);
    }

}
