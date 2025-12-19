using UnityEngine;
using UnityEngine.UI;
using FishNet.Managing;
using FishNet.Managing.Scened; // SceneLoadData, ReplaceOption

public class ConnectUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkManager manager;     // drag NetworkRoot here
    [SerializeField] private InputField ipLegacy;        // optional, informational only
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Awake()
    {
        if (!manager) manager = FindFirstObjectByType<NetworkManager>();
    }

    // HOST: start server+client, then load Lobby on the server (auto-syncs to all)
    public void OnHostClicked()
    {
        if (manager == null) return;

        manager.ServerManager.StartConnection();
        manager.ClientManager.StartConnection();

        LoadLobbyOnServer();
        Debug.Log("[ConnectUI] Host started ? loading Lobby on server.");
    }

    private void LoadLobbyOnServer()
    {
        if (!manager.IsServerStarted)
        {
            StartCoroutine(LoadLobbyNextFrame());
            return;
        }

        var sld = new SceneLoadData(lobbySceneName);
        sld.ReplaceScenes = ReplaceOption.All;   // replace current scenes for everyone
        manager.SceneManager.LoadGlobalScenes(sld);
    }

    private System.Collections.IEnumerator LoadLobbyNextFrame()
    {
        yield return null;
        LoadLobbyOnServer();
    }

    // JOIN: just start client; server’s current scene (Lobby) will sync automatically
    public void OnJoinClicked()
    {
        if (manager == null) return;

        // NOTE: We do NOT set Tugboat address here.
        // Configure the Tugboat component's Client Address in the Inspector per build if needed.
        // For localhost testing, set it to "localhost" in the component.
        manager.ClientManager.StartConnection();

        Debug.Log("[ConnectUI] Client joining… waiting for server to sync Lobby scene.");
    }

    // Optional manual button if you still want it in Bootstrap
    public void GoToLobby()
    {
        var sld = new SceneLoadData(lobbySceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        manager.SceneManager.LoadGlobalScenes(sld);
    }
}
