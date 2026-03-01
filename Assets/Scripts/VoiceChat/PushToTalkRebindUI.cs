using System;
using TMPro;
using UnityEngine;

public class PushToTalkRebindUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;      // shows current binding
    [SerializeField] private KeyCode defaultKey = KeyCode.V;

    private const string PrefKey = "VoicePTTKey";
    private bool _waitingForKey;

    private void Start()
    {
        RefreshLabel();
    }

    public void BeginRebind()
    {
        _waitingForKey = true;
        if (label != null) label.text = "Press a key...";
    }

    private void Update()
    {
        if (!_waitingForKey) return;

        // Detect any key down
        foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(k))
            {
                SaveKey(k);
                _waitingForKey = false;
                RefreshLabel();
                ApplyToLocalVoiceChat();
                return;
            }
        }
    }

    private void SaveKey(KeyCode key)
    {
        PlayerPrefs.SetString(PrefKey, key.ToString());
        PlayerPrefs.Save();
    }

    private KeyCode LoadKey()
    {
        string s = PlayerPrefs.GetString(PrefKey, defaultKey.ToString());
        if (Enum.TryParse(s, out KeyCode k)) return k;
        return defaultKey;
    }

    private void RefreshLabel()
    {
        if (label == null) return;
        label.text = LoadKey().ToString();
    }

    private void ApplyToLocalVoiceChat()
    {
        // Apply to the local player's VoiceChat if it exists right now
        // (You can also apply this in your player spawn script)
        var all = FindObjectsOfType<VoiceChat>(true);
        KeyCode k = LoadKey();

        foreach (var vc in all)
        {
            if (vc != null && vc.IsOwner)
            {
                vc.PushToTalkKey = k; // field name must match your VoiceChat
                break;
            }
        }
    }
}