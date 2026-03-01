using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class MicrophoneDropdownBinder : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private MicrophoneManager micManager;

    private const string PrefKey = "SelectedMic";

    private void OnEnable()
    {
        micManager = FindObjectOfType<MicrophoneManager>(true);

        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null)
        {
            Debug.LogError("[MicDropdown] No TMP_Dropdown assigned/found.");
            return;
        }

        var devices = Microphone.devices?.ToList() ?? new List<string>();

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(devices);

        if (devices.Count == 0)
        {
            Debug.LogWarning("[MicDropdown] No microphone devices found.");
            dropdown.interactable = false;
            return;
        }

        dropdown.interactable = true;

        // Choose what the dropdown should show:
        // 1) Saved device (preferred)
        // 2) else first device
        int index = 0;

        if (PlayerPrefs.HasKey(PrefKey))
        {
            string saved = PlayerPrefs.GetString(PrefKey, "");
            int found = devices.IndexOf(saved);
            if (found >= 0) index = found;
        }

        dropdown.SetValueWithoutNotify(index);

        // Apply on open (ensures bootstrap selection carries into arena)
        Apply(index);

        dropdown.onValueChanged.AddListener(Apply);
    }

    private void Apply(int index)
    {
        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0) return;
        if (index < 0 || index >= devices.Length) return;

        string selected = devices[index];

        // Save so EVERY scene/menu shows the same selection later
        PlayerPrefs.SetString(PrefKey, selected);
        PlayerPrefs.Save();

        // Apply to microphone manager if present
        if (micManager == null)
            micManager = FindObjectOfType<MicrophoneManager>(true);

        if (micManager == null)
        {
            Debug.LogWarning("[MicDropdown] MicrophoneManager not found (yet). Saved selection anyway.");
            return;
        }

        // ---- APPLY TO YOUR MicrophoneManager ----
        // Since your MicrophoneManager API name varies by version, we do a safe reflection apply.
        if (!TryApplyToMicrophoneManager(micManager, selected))
        {
            Debug.LogError("[MicDropdown] Could not apply mic to MicrophoneManager. " +
                           "Tell me what fields you see on MicrophoneManager Inspector and I’ll hard-wire it.");
        }
    }

    private static bool TryApplyToMicrophoneManager(MicrophoneManager mm, string deviceName)
    {
        var t = mm.GetType();
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        // Try common method names first
        string[] methods =
        {
            "SetMicrophone", "SetDevice", "SetInputDevice", "SetMicrophoneDevice",
            "SelectMicrophone", "ChangeMicrophone", "SetMic", "SetCurrentDevice"
        };

        foreach (var name in methods)
        {
            var m = t.GetMethod(name, flags, null, new[] { typeof(string) }, null);
            if (m != null)
            {
                m.Invoke(mm, new object[] { deviceName });
                return true;
            }
        }

        // Try common property/field names
        string[] names =
        {
            "DeviceName", "deviceName",
            "MicrophoneDevice", "microphoneDevice",
            "SelectedDevice", "selectedDevice",
            "CurrentDevice", "currentDevice",
            "InputDevice", "inputDevice",
            "MicDevice", "micDevice"
        };

        foreach (var n in names)
        {
            var p = t.GetProperty(n, flags);
            if (p != null && p.CanWrite && p.PropertyType == typeof(string))
            {
                p.SetValue(mm, deviceName);
                return true;
            }
        }

        foreach (var n in names)
        {
            var f = t.GetField(n, flags);
            if (f != null && f.FieldType == typeof(string))
            {
                f.SetValue(mm, deviceName);
                return true;
            }
        }

        return false;
    }
}