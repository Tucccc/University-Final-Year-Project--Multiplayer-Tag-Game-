using UnityEngine;
using System.Linq;

public static class RoundOverUI
{
    public static bool ControlsEnabled { get; private set; } = true;

    // Buffer last requested state so scene timing doesn't matter.
    private static bool _wantOverlayVisible;
    private static bool _haveOverlayState;

    public static void SetRoundActiveClient(bool running)
    {
        ControlsEnabled = running;
        SetOverlayVisible(!running);
    }

    public static void SetControlsEnabled(bool enabled) => SetRoundActiveClient(enabled);

    public static void SetOverlayVisible(bool show)
    {
        // ✅ Pause always wins: never show round-over overlay while paused.
        if (LocalPauseMenu.IsPauseOpen)
            show = false;

        _wantOverlayVisible = show;
        _haveOverlayState = true;

        var panel = FindPanelIncludeInactive();
        if (panel == null)
            return;

        // ✅ Only force GameObject active if we actually want to SHOW.
        if (show)
        {
            if (!panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            panel.SetVisible(true);
        }
        else
        {
            // Hide without forcing activation.
            panel.SetVisible(false);

            // Optional but recommended: disable the GO to prevent it intercepting input.
            if (panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);
        }
    }

    // Called by RoundOverPanel when it becomes available.
    public static void NotifyPanelReady(RoundOverPanel panel)
    {
        if (panel == null) return;

        bool show = _haveOverlayState ? _wantOverlayVisible : false;

        // ✅ Pause always wins here too.
        if (LocalPauseMenu.IsPauseOpen)
            show = false;

        if (show)
        {
            if (!panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            panel.SetVisible(true);
        }
        else
        {
            panel.SetVisible(false);

            if (panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);
        }
    }

    private static RoundOverPanel FindPanelIncludeInactive()
    {
#if UNITY_2022_2_OR_NEWER
        return Object.FindFirstObjectByType<RoundOverPanel>(FindObjectsInactive.Include);
#else
        return Resources.FindObjectsOfTypeAll<RoundOverPanel>()
            .FirstOrDefault(p => p != null && p.gameObject.scene.IsValid() && p.gameObject.scene.isLoaded);
#endif
    }
}