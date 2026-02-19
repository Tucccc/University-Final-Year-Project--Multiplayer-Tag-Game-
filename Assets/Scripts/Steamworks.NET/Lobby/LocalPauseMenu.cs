using UnityEngine;

public class LocalPauseMenu : MonoBehaviour
{
    private Canvas menuPanel;

    void Awake()
    {
        GameObject canvasObj = GameObject.Find("PauseMenu");
        if (canvasObj != null)
        {
            menuPanel = canvasObj.GetComponent<Canvas>();
            menuPanel.gameObject.SetActive(false);
            Debug.Log("PauseMenu found and ready");
        }
        else
        {
            Debug.LogError("PauseMenu NOT found in scene!");
        }
    }

    void Update()
    {
        if (!RoundOverUI.ControlsEnabled)
        {
            if (menuPanel?.gameObject.activeInHierarchy == true)
                CloseMenu();
            return;
        }

        if (menuPanel == null || !Input.GetKeyDown(KeyCode.Escape))
            return;

        if (!menuPanel.gameObject.activeInHierarchy)
            OpenMenu();
        else
            CloseMenu();
    }

    void OpenMenu() => menuPanel.gameObject.SetActive(true);
    void CloseMenu() => menuPanel.gameObject.SetActive(false);
}
