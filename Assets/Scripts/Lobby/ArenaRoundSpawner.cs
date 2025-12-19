using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using System.Linq;

public class ArenaRoundSpawner : MonoBehaviour
{
    [SerializeField] private FishNet.Object.NetworkObject roundManagerPrefab;

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
        if (mgr == null) { Debug.LogWarning("[ArenaRoundSpawner] No NetworkManager found."); return; }
        if (!mgr.IsServerStarted) return;

        bool loadedArena = args.LoadedScenes != null && args.LoadedScenes.Any(s => s.name == "Arena");
        if (!loadedArena) return;

        if (roundManagerPrefab == null)
        {
            Debug.LogError("[ArenaRoundSpawner] RoundManager prefab not assigned.");
            return;
        }

        if (RoundManager.Instance != null)
            return; // already present

        var no = Instantiate(roundManagerPrefab);
        mgr.ServerManager.Spawn(no);
        Debug.Log("[ArenaRoundSpawner] Spawned RoundManager prefab.");
    }
}
