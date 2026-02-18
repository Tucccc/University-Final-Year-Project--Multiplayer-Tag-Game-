using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Object;

public class RoundOverButtons : MonoBehaviour
{
    [Header("Buttons on the RoundOver panel")]
    [SerializeField] private Button startNextRoundButton;
    [SerializeField] private Button backToLobbyButton;
    [SerializeField] private Button leaveButton;

    private void Awake()
    {
        // (Optional) auto-find if you forget to assign in inspector.
        // Comment these out if you prefer manual assignment.
        if (!startNextRoundButton) startNextRoundButton = transform.Find("HostStartButton")?.GetComponent<Button>();
        if (!backToLobbyButton) backToLobbyButton = transform.Find("HostLobbyButton")?.GetComponent<Button>();
        if (!leaveButton) leaveButton = transform.Find("LeaveButton")?.GetComponent<Button>();
    }

    private void OnEnable()
    {
        // Clear then re-add so we never double bind when scene reloads / object re-enables
        if (startNextRoundButton)
        {
            startNextRoundButton.onClick.RemoveAllListeners();
            startNextRoundButton.onClick.AddListener(OnStartNextRoundClicked);
        }

        if (backToLobbyButton)
        {
            backToLobbyButton.onClick.RemoveAllListeners();
            backToLobbyButton.onClick.AddListener(OnBackToLobbyClicked);
        }

        if (leaveButton)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }
    }

    private void OnStartNextRoundClicked()
    {
        var rm = RoundManager.Instance;
        if (rm == null) return;

        // Only host/server can start rounds (method is [Server])
        if (rm.IsServer)
            rm.StartNewRoundServer();
    }

    private void OnBackToLobbyClicked()
    {
        var rm = RoundManager.Instance;
        if (rm == null) return;

        // This RPC already blocks non-host (ClientId != 0) inside your code.
        rm.ReturnToLobbyServerRpc();
    }

    private void OnLeaveClicked()
    {
        // Host leaves -> kick everyone (stop server + client)
        // Client leaves -> disconnect client only
        if (InstanceFinder.NetworkManager == null) return;

        bool isHost = InstanceFinder.IsServerStarted; // host runs server

        if (isHost)
        {
            // Optional message before shutdown (your RoundManager supports it)
            var rm = RoundManager.Instance;
            if (rm != null)
                rm.HostShutdownAllServerRpc("Host ended the game.");

            InstanceFinder.ServerManager.StopConnection(true);
            InstanceFinder.ClientManager.StopConnection();
        }
        else
        {
            InstanceFinder.ClientManager.StopConnection();
        }
    }
}
