using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Object;

public class RoundPauseMenu : MonoBehaviour
{
    [Header("Buttons on the RoundOver panel")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button leaveButton;


    private void Awake()
    {
        // (Optional) auto-find if you forget to assign in inspector.
        // Comment these out if you prefer manual assignment.
        if (!settingsButton) settingsButton = transform.Find("SettingsButton")?.GetComponent<Button>();
        if (!leaveButton) leaveButton = transform.Find("LeaveButton")?.GetComponent<Button>();
    }

    private void OnEnable()
    {
        // Clear then re-add so we never double bind when scene reloads / object re-enables
        if (settingsButton)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnStartNextRoundClicked);
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
        rm.StartNewRoundServerRpc();
    }

    private void OnLeaveClicked()
    {
        SteamSessionManager.Instance?.LeaveAndReset(); // leaves steam lobby + stops fishnet + loads Menu
    }


}
