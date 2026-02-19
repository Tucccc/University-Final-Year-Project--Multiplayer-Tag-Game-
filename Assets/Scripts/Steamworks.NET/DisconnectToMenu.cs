using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;

public class DisconnectToMenu : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "Menu";
    private bool _wasConnected;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm == null) return;

        nm.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDisable()
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm == null) return;

        nm.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _wasConnected = true;
            return;
        }

        // If we were connected and now we are not -> host probably left / connection dropped.
        if (_wasConnected && args.ConnectionState == LocalConnectionState.Stopped)
        {
            _wasConnected = false;

            // Optional: also clean up Steam lobby state if you want.
            SteamSessionManager.Instance?.LeaveAndReset();

        }
    }
}
