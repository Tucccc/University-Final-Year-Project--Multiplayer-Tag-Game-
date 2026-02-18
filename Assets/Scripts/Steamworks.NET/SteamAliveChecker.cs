using UnityEngine;
using Steamworks;

public class SteamAliveCheck : MonoBehaviour
{
    void Start()
    {
        // If you're using SteamManager, you can check its Initialized property instead.
        // But this direct check is fine for an "alive check" as long as SteamManager exists in the scene.
        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogWarning("[Steam] Steam client not running.");
            return;
        }

        // These calls will throw / fail if SteamAPI didn't initialize.
        try
        {
            var name = SteamFriends.GetPersonaName();
            var id = SteamUser.GetSteamID();
            Debug.Log($"[Steam] OK. Name={name}, SteamID={id}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Steam] Not initialized / failed calls: {e.Message}");
        }
    }
}
