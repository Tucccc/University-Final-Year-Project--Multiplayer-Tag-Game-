using UnityEngine;

public class LocalPauseMenu : MonoBehaviour
{
    public static bool IsPauseOpen { get; private set; }
    private Canvas menuPanel;

    void Awake()
    {
        IsPauseOpen = false;
        var go = GameObject.Find("PauseMenu");
        menuPanel = go ? go.GetComponent<Canvas>() : null;
        if (menuPanel) menuPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        // don’t allow pause while round over UI has disabled gameplay
        if (!RoundOverUI.ControlsEnabled)
        {
            if (IsPauseOpen) CloseMenu();
            return;
        }

        if (menuPanel == null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!IsPauseOpen) OpenMenu();
            else CloseMenu();
        }
    }

    void OpenMenu()
    {
        IsPauseOpen = true;
        menuPanel.gameObject.SetActive(true);
    }

    void CloseMenu()
    {
        IsPauseOpen = false;
        menuPanel.gameObject.SetActive(false);
    }
}