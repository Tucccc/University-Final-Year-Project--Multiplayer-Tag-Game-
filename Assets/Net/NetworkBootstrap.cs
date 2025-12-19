using UnityEngine;
using FishNet.Managing;                 // NetworkManager
using FishNet.Managing.Transporting;    // TransportManager  ?

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    private static NetworkBootstrap _instance;

    private void Awake()
    {
        // avoid duplicates if Bootstrap loads twice
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // find the NetworkManager if not assigned
        if (manager == null)
        {
            manager = FindFirstObjectByType<NetworkManager>();

        }

        if (manager == null)
        {
            Debug.LogError("NetworkBootstrap: No NetworkManager found in scene.");
            return;
        }

        // persist across scene loads
        DontDestroyOnLoad(manager.gameObject);
        DontDestroyOnLoad(gameObject);

        // optional: sanity check for transport wiring
        var tm = manager.GetComponent<TransportManager>();
        if (tm == null || tm.Transport == null)
            Debug.LogWarning("NetworkBootstrap: No Transport set. Add TransportManager + Tugboat to NetworkRoot.");
    }
}
