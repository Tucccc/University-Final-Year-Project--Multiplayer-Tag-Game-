using UnityEngine;
using FishNet.Object;

public class OwnerView : NetworkBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private AudioListener listener;

    private new void Reset()
    {
        if (!cam) cam = GetComponentInChildren<Camera>(true);
        if (!listener) listener = GetComponentInChildren<AudioListener>(true);
    }

    public override void OnStartClient() 
    {
        base.OnStartClient();

        bool enable = IsOwner;

        if (cam) cam.enabled = enable;
        if (listener) listener.enabled = enable;

        if (cam) cam.tag = enable ? "MainCamera" : "Untagged";

        // extra safety: only one listener active
        if (enable)
        {
            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (other != listener && other.enabled) other.enabled = false;
        }
    }
}
