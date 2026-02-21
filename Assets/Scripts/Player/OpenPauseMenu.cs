using FishNet.Object;
using UnityEngine;
using UnityEngine.UIElements;

public class OpenPauseMenu : NetworkBehaviour
{
    public Canvas menuPanel;
    [SerializeField] private Camera mainCamera;


    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only local player uses this UI
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Find the local pause menu on THIS client
        GameObject canvasObj = GameObject.Find("PauseMenu");
        if (canvasObj != null)
        {
            menuPanel = canvasObj.GetComponent<Canvas>();
            menuPanel.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("PauseMenu canvas not found on client " + OwnerId);
        }
    }

    private void Update()
    {
        if (!IsOwner || menuPanel == null)
            return;

        // Close when round-over UI is active
        if (!RoundOverUI.ControlsEnabled)
        {
            if (menuPanel.gameObject.activeInHierarchy)
                CloseMenu();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!menuPanel.gameObject.activeInHierarchy)
            {
                OpenMenu();
            }
            else
            {
                CloseMenu();
            }
        }
    }

    private void OpenMenu() => menuPanel.gameObject.SetActive(true);
    private void CloseMenu() => menuPanel.gameObject.SetActive(false);
}
