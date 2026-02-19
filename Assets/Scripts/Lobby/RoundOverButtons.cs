using FishNet;
using UnityEngine;
using UnityEngine.UI;

public class RoundOverButtons : MonoBehaviour
{
    [Header("Buttons on the RoundOver panel")]
    [SerializeField] private Button startNextRoundButton;
    [SerializeField] private Button backToLobbyButton;
    [SerializeField] private Button leaveButton;

    private void Awake()
    {
        // Optional auto-find if not assigned in Inspector
        if (!startNextRoundButton) startNextRoundButton = transform.Find("HostStartButton")?.GetComponent<Button>();
        if (!backToLobbyButton) backToLobbyButton = transform.Find("HostLobbyButton")?.GetComponent<Button>();
        if (!leaveButton) leaveButton = transform.Find("LeaveButton")?.GetComponent<Button>();
    }

    private void OnEnable()
    {
        // Host-only interaction (host = server + client)
        bool isHost = InstanceFinder.IsHost;

        if (startNextRoundButton) startNextRoundButton.interactable = isHost;
        if (backToLobbyButton) backToLobbyButton.interactable = isHost;
        if (leaveButton) leaveButton.interactable = true;

        // Bind once per enable
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
        if (!InstanceFinder.IsHost) return; // safety
        var rm = RoundManager.Instance;
        if (rm == null) return;

        rm.StartNewRoundServerRpc();
    }

    private void OnBackToLobbyClicked()
    {
        if (!InstanceFinder.IsHost) return; // safety
        var rm = RoundManager.Instance;
        if (rm == null) return;

        rm.ReturnToLobbyServerRpc();
    }

    private void OnLeaveClicked()
    {
        SteamSessionManager.Instance?.LeaveAndReset();
    }
}
