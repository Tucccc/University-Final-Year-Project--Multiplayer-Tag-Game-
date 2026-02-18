using UnityEngine;
using UnityEngine.UI;
using FishNet;

public class LobbyStartButtonGate : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private bool _last;

    void Update()
    {
        var nm = InstanceFinder.NetworkManager;
        bool isServer = nm != null && nm.IsServerStarted;
        bool isClient = nm != null && nm.IsClientStarted;

        if (_last != isServer)
        {
            Debug.Log($"[Gate] IsServerStarted={isServer} IsClientStarted={isClient} Transport={nm?.TransportManager?.Transport}");
            _last = isServer;
        }

        startButton.interactable = isServer;
    }
}
