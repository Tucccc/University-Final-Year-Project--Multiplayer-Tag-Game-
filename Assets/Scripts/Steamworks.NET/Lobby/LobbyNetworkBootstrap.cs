using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Managing;
using Steamworks;

public class LobbyNetworkBootstrap : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string menuSceneName = "Menu";

    [Header("FishNet (optional)")]
    [SerializeField] private NetworkManager networkManager; // drag your NetworkRoot's NetworkManager here if you want

    private bool _started;

    private void Awake()
    {
        if (networkManager == null)
            networkManager = InstanceFinder.NetworkManager;
    }


    private void Start()
    {
        // Run once
        if (_started) return;
        _started = true;

        TryStartFishNetFromSteamLobby();
    }

    private void TryStartFishNetFromSteamLobby()
    {
        // 1) Validate Steam + Lobby
        var steamSession = SteamSessionManager.Instance;
        if (steamSession == null)
        {
            Debug.LogWarning("[LobbyNetBootstrap] No SteamSessionManager. Returning to Bootstrap.");
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        if (!steamSession.IsInLobby || steamSession.CurrentLobbyId == CSteamID.Nil)
        {
            Debug.LogWarning("[LobbyNetBootstrap] Not in a Steam lobby. Returning to Bootstrap.");
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        if (networkManager == null)
        {
            Debug.LogError("[LobbyNetBootstrap] NetworkManager not found (InstanceFinder.NetworkManager is null).");
            return;
        }

        // Decide who hosts (Steam lobby owner)
        CSteamID lobbyId = steamSession.CurrentLobbyId;
        CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
        CSteamID me = SteamUser.GetSteamID();
        bool amOwner = (owner == me);

        Debug.Log($"[LobbyNetBootstrap] Lobby={lobbyId} Owner={owner} Me={me} AmOwner={amOwner}");

        // Don’t double-start
        bool serverStarted = networkManager.ServerManager.Started;
        bool clientStarted = networkManager.ClientManager.Started;

        if (amOwner)
        {
            // Start SERVER first
            if (!networkManager.ServerManager.Started)
            {
                Debug.Log("[LobbyNetBootstrap] Starting FishNet SERVER (Host).");
                networkManager.ServerManager.StartConnection();
            }

            // Wait a moment, then decide whether to start CLIENT
            StartCoroutine(StartHostClientAfterServerUp());
        }
        else
        {
            if (!clientStarted)
            {
                Debug.Log($"[LobbyNetBootstrap] Starting FishNet CLIENT -> ownerSteamId={owner}");

                // Try overload that accepts an address (SteamID as string).
                // If this line doesn't compile in your version, use Option B below.
                networkManager.ClientManager.StartConnection(owner.ToString());
                LogClientState();
            }
        }


    }

    void LogClientState()
    {
        Debug.Log($"[LobbyNetBootstrap] ClientStarted={networkManager.ClientManager.Started}");
    }

    private System.Collections.IEnumerator StartHostClientAfterServerUp()
    {
        yield return new WaitForSeconds(0.5f);

        bool serverStarted = networkManager != null && networkManager.ServerManager.Started;
        Debug.Log($"[LobbyNetBootstrap] 0.5s later: ServerStarted={serverStarted} ClientStarted={networkManager?.ClientManager.Started}");

        if (!serverStarted)
        {
            Debug.LogError("[LobbyNetBootstrap] Server failed to start (FishySteamworks stopped). Not starting host client.");
            yield break;
        }

        if (!networkManager.ClientManager.Started)
        {
            Debug.Log("[LobbyNetBootstrap] Starting FishNet CLIENT (Host).");
            networkManager.ClientManager.StartConnection();
        }
    }

}
