using UnityEngine;

public class MenuUIController : MonoBehaviour
{
    public void OnHostSteamClicked()
    {
        SteamSessionManager.Instance?.HostLobby();
    }

    public void OnQuitClicked()
    {

    }
}
