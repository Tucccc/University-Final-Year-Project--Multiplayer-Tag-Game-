using System.Collections;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;

public class LobbyManager2 : NetworkBehaviour
{
    public static LobbyManager2 Instance { get; private set; }

    [SerializeField] private NetworkObject lobbyPlayerStatePrefab;
    private readonly Dictionary<int, LobbyPlayerState> _statesByClientId = new();

    public readonly SyncVar<bool> AllReady = new SyncVar<bool>(false);
    private readonly List<LobbyPlayerState> _players = new List<LobbyPlayerState>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

        // host/local connection might already exist
        StartCoroutine(SpawnForExistingConnectionsDelayed());
        RecalculateAllReady();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
    }

    public void RequestStartGame()
    {
        //if host / server start
        if (IsServerInitialized)
        {
            TryStartGame();
        }
        else
        {
            RequestStartGameServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartGameServerRpc()
    {
               TryStartGame();
    }


    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // Delay so we don't spawn "too early"
            StartCoroutine(SpawnLobbyStateDelayed(conn));
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            DespawnLobbyStateFor(conn);
        }
    }

    private IEnumerator SpawnLobbyStateDelayed(NetworkConnection conn)
    {
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.25f);

        if (conn == null || !conn.IsValid)
            yield break;

        SpawnLobbyStateFor(conn);
    }

    private IEnumerator SpawnForExistingConnectionsDelayed()
    {
        yield return null;
        yield return null;

        foreach (var kvp in InstanceFinder.ServerManager.Clients)
            StartCoroutine(SpawnLobbyStateDelayed(kvp.Value));
    }

    [Server]
    private void SpawnLobbyStateFor(NetworkConnection conn)
    {
        if (conn == null) return;
        if (_statesByClientId.ContainsKey(conn.ClientId)) return;

        if (lobbyPlayerStatePrefab == null)
        {
            Debug.LogError("[LobbyManager2] lobbyPlayerStatePrefab not assigned.");
            return;
        }

        var nob = Instantiate(lobbyPlayerStatePrefab);
        InstanceFinder.ServerManager.Spawn(nob, conn);

        var state = nob.GetComponent<LobbyPlayerState>();
        if (state == null)
        {
            Debug.LogError("[LobbyManager2] Prefab is missing LobbyPlayerState.");
            return;
        }

        _statesByClientId[conn.ClientId] = state;
    }

    [Server]
    private void DespawnLobbyStateFor(NetworkConnection conn)
    {
        if (conn == null) return;

        if (_statesByClientId.TryGetValue(conn.ClientId, out var state) && state != null)
            InstanceFinder.ServerManager.Despawn(state.NetworkObject);

        _statesByClientId.Remove(conn.ClientId);
    }


    [Server]
    private void TryStartGame()
    {
        // Must be all ready
        if (!AllReady.Value)
            return;

        // (Optional) you can also enforce "only steam owner" here,
        // but since the button is only interactable for the owner, this is usually enough.

        LoadArenaForEveryone();
    }

    [Server]
    private void LoadArenaForEveryone()
    {
        var sld = new FishNet.Managing.Scened.SceneLoadData("Arena");
        sld.ReplaceScenes = FishNet.Managing.Scened.ReplaceOption.All;
        FishNet.InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }




    [Server]
    public void Register(LobbyPlayerState p)
    {
        if (!_players.Contains(p))
            _players.Add(p);

        RecalculateAllReady();
    }

    [Server]
    public void Unregister(LobbyPlayerState p)
    {
        _players.Remove(p);
        RecalculateAllReady();
    }

    [Server]
    public void RecalculateAllReady()
    {
        if (_players.Count == 0) { AllReady.Value = false; return; }

        foreach (var p in _players)
        {
            if (p == null || !p.IsReady.Value)
            {
                AllReady.Value = false;
                return;
            }
        }

        AllReady.Value = true;
    }
}
