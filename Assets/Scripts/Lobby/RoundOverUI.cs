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
        _wantOverlayVisible = show;
        _haveOverlayState = true;

        var panel = FindPanelIncludeInactive();
        if (panel == null)
            return;

        // KEY: if it's disabled on the client, enable it before setting visible.
        if (!panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        panel.SetVisible(show);
    }

    // Called by RoundOverPanel when it becomes available.
    public static void NotifyPanelReady(RoundOverPanel panel)
    {
        if (panel == null) return;

        if (_haveOverlayState)
        {
            if (_wantOverlayVisible && !panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            panel.SetVisible(_wantOverlayVisible);
        }
        else
        {
            panel.SetVisible(false);
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
