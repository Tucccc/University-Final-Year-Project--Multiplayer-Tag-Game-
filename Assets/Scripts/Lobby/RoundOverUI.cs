using UnityEngine;

public static class RoundOverUI
{
    /// <summary>True when gameplay input is allowed.</summary>
    public static bool ControlsEnabled { get; private set; } = true;

    /// <summary>
    /// Main entry used by RoundManager / disconnect code.
    /// running = true  -> gameplay enabled, overlay hidden
    /// running = false -> gameplay frozen, overlay shown
    /// </summary>
    public static void SetRoundActiveClient(bool running)
    {
        ControlsEnabled = running;
        TrySetOverlayVisible(!running);
    }

    /// <summary>
    /// Backwards-compatible helper for old code that was setting ControlsEnabled directly.
    /// </summary>
    public static void SetControlsEnabled(bool enabled) => SetRoundActiveClient(enabled);

    private static void TrySetOverlayVisible(bool show)
    {
        // Simple, Unity-6-safe version: only look at active objects
        var panel = Object.FindFirstObjectByType<RoundOverPanel>();
        if (panel != null)
            panel.SetVisible(show);
    }
}
