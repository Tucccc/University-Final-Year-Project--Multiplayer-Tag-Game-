using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Managing;
using System.Linq;

public class LobbyUIBridge : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] private Toggle readyToggle;
    [SerializeField] private Button startButton;

    private LobbyManager _lobby;
    private NetworkManager _nm;

    // Buffers if user clicks before manager is found
    private bool? _pendingReady;
    private bool _pendingStart;

    private void OnEnable()
    {
        _nm = InstanceFinder.NetworkManager;
        RefreshStartGate();
        StartCoroutine(FindLobbyRoutine());


    }

    private void Awake()
    {
        readyToggle = FindAnyObjectByType<Toggle>();
        startButton = FindAnyObjectByType<Button>();

        if (readyToggle != null)
        {
            readyToggle.onValueChanged.AddListener(OnReadyToggled);
        }
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
    }

    private System.Collections.IEnumerator FindLobbyRoutine()
    {
        for (int i = 0; i < 240; i++) // ~4s worst case
        {
            _lobby = FindFirstObjectByType<LobbyManager>();
            if (_lobby != null)
            {
                // Flush any buffered input
                if (_pendingReady.HasValue)
                {
                    _lobby.SetReadyServerRpc(_pendingReady.Value);
                    _pendingReady = null;
                }
                if (_pendingStart)
                {
                    if (_nm != null && _nm.IsServerStarted) _lobby.OnStartButtonPressed();
                    else _lobby.RequestStartMatchServerRpc();
                    _pendingStart = false;
                }

                yield break;
            }
            yield return null;
        }
        Debug.LogWarning("[LobbyUIBridge] Could not find LobbyManager. Is LobbyBootstrapSpawner set up?");
    }

    public void OnReadyToggled(bool isReady)
    {
        Debug.Log("toggle clicked");
        if (_lobby != null) _lobby.SetReadyServerRpc(isReady);
        else _pendingReady = isReady; // buffer until found
        _lobby.PushReadyLabel();
    }

    public void OnStartClicked()
    {
        if (_lobby != null)
        {
            if (_nm != null && _nm.IsServerStarted) _lobby.OnStartButtonPressed();   // host
            else _lobby.RequestStartMatchServerRpc();                                // client asks host
        }
        else
        {
            _pendingStart = true;
            Debug.Log("[LobbyUIBridge] Start buffered until LobbyManager spawns.");
        }
    }

    private void RefreshStartGate()
    {
        if (startButton == null) return;
        bool isHost = _nm != null && _nm.IsServerStarted;
        startButton.interactable = isHost; // only host can start
    }
}
