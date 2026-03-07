using UnityEngine;

public class ApplySavedMicOnStart : MonoBehaviour
{
    private const string PrefKey = "SelectedMic";

    private void Start()
    {
        var mm = FindObjectOfType<MicrophoneManager>(true);
        if (mm == null) return;

        if (!PlayerPrefs.HasKey(PrefKey)) return;

        string saved = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        // Re-use the binder’s reflection setter logic by calling SendMessage,
        // or just keep it simple: open your settings once and it applies.
        // If you want, I can wire this to your exact MicrophoneManager API once known.
    }
}