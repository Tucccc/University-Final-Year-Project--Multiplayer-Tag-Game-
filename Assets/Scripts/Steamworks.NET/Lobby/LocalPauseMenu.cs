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
        // If RoundManager isn't ready on this client yet, allow pause anyway.
        bool roundRunning = (RoundManager.Instance == null) ? true : RoundManager.RoundRunningClient;

        // don’t allow pause while round is over / not running
        if (!roundRunning)
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

        // ✅ Force-hide roundover UI while pause is open (prevents overlap/wrong state).
        ScoreboardUI.HideRoundOver();
        RoundOverUI.SetRoundActiveClient(true); // "running" == true => should hide roundover UI
    }

    void CloseMenu()
    {
        IsPauseOpen = false;
        menuPanel.gameObject.SetActive(false);

        // ✅ When unpausing, re-apply correct state based on round flag.
        bool running = (RoundManager.Instance == null) ? true : RoundManager.RoundRunningClient;
        RoundOverUI.SetRoundActiveClient(running);

        if (running)
            ScoreboardUI.HideRoundOver();
        else
            ScoreboardUI.ShowRoundOver();
    }
}