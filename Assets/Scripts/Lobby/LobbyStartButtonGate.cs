using UnityEngine;
using FishNet.Managing;

public class LobbyStartButtonGate : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    private void Awake() { if (!manager) manager = FindFirstObjectByType<NetworkManager>(); }
    private void OnEnable() { gameObject.SetActive(manager && manager.IsServerStarted); }
}
