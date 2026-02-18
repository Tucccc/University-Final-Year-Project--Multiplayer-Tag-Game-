// Scripts/Net/DisconnectWatcher.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Managing;
using FishNet.Managing.Client;   // ClientConnectionStateArgs
using FishNet.Transporting;      // LocalConnectionState

public class DisconnectWatcher : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (!manager)
            manager = FindFirstObjectByType<NetworkManager>();

        if (manager != null)
            manager.ClientManager.OnClientConnectionState += OnClientState;
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.ClientManager.OnClientConnectionState -= OnClientState;
    }

    // ? Correct signature for the event:
    private void OnClientState(ClientConnectionStateArgs args)
    {
        // When our local client stops (lost connection to host/server)...
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            if (string.IsNullOrWhiteSpace(DisconnectNotice.Message))
                DisconnectNotice.Message = "Disconnected from host.";

            // Load Bootstrap so the player can Host/Join again
            if (SceneManager.GetActiveScene().name != "Menu")
                SceneManager.LoadScene("Menu");
        }

        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            RoundOverUI.SetControlsEnabled(false);
            if (string.IsNullOrWhiteSpace(DisconnectNotice.Message))
                DisconnectNotice.Message = "Disconnected from host.";
            if (SceneManager.GetActiveScene().name != "Menu")
                SceneManager.LoadScene("Menu");
        }
    }
}
