using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using System.Linq;

public class LobbyBootstrapSpawner : MonoBehaviour
{
    [SerializeField] private FishNet.Object.NetworkObject lobbyManagerPrefab;

    private NetworkManager Manager => InstanceFinder.NetworkManager;

    private void OnEnable()
    {
        if (Manager != null)
            Manager.SceneManager.OnLoadEnd += OnLoadEnd;
    }

    private void OnDisable()
    {
        if (Manager != null)
            Manager.SceneManager.OnLoadEnd -= OnLoadEnd;
    }

    private void OnLoadEnd(SceneLoadEndEventArgs args)
    {
        var mgr = Manager;
        if (mgr == null) { Debug.LogWarning("[LobbyBootstrapSpawner] No NetworkManager found."); return; }
        if (!mgr.IsServerStarted) return;

        bool loadedLobby = args.LoadedScenes != null && args.LoadedScenes.Any(s => s.name == "Lobby");
        if (!loadedLobby) return;

        if (lobbyManagerPrefab == null)
        {
            Debug.LogError("[LobbyBootstrapSpawner] LobbyManager prefab not assigned.");
            return;
        }

        if (FindFirstObjectByType<LobbyManager>() != null)
            return; // already present

        var no = Instantiate(lobbyManagerPrefab);
        mgr.ServerManager.Spawn(no);
        Debug.Log("[LobbyBootstrapSpawner] Spawned LobbyManager prefab.");
    }
}
