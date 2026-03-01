using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class MicrophoneManager : MonoBehaviour
{
    public static MicrophoneManager Instance { get; private set; }

    [Header("Dropdown Auto-Bind")]
    [Tooltip("Name of the TMP_Dropdown GameObject in your settings menus (bootstrap + arena).")]
    [SerializeField] private string dropdownObjectName = "MicDropdown";

    [Tooltip("If true, we will search the scene periodically until we find the dropdown.")]
    [SerializeField] private bool keepSearchingForDropdown = true;

    [Tooltip("How often to search for the dropdown (seconds).")]
    [SerializeField] private float searchInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logSelection = false;

    private TMP_Dropdown _dropdown;
    private readonly List<string> _devices = new();

    private const string PrefKey = "SelectedMic";

    private float _nextSearchTime;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        RefreshDeviceList();
        ApplySavedDeviceIfValid();
        TryBindDropdownNow();
    }

    private void Update()
    {
        if (!keepSearchingForDropdown) return;

        // If dropdown is missing or got destroyed (scene change), rebind.
        if (_dropdown == null && Time.unscaledTime >= _nextSearchTime)
        {
            _nextSearchTime = Time.unscaledTime + searchInterval;
            TryBindDropdownNow();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Scene changed => dropdown reference likely invalid.
        _dropdown = null;

        RefreshDeviceList();
        ApplySavedDeviceIfValid();

        // Try bind immediately (then Update keeps trying if needed).
        TryBindDropdownNow();
    }

    private void RefreshDeviceList()
    {
        _devices.Clear();

        // Unity fills this automatically.
        foreach (var d in Microphone.devices)
            _devices.Add(d);
    }

    private void ApplySavedDeviceIfValid()
    {
        if (_devices.Count == 0) return;

        // If nothing saved, default to index 0.
        if (!PlayerPrefs.HasKey(PrefKey))
            PlayerPrefs.SetString(PrefKey, _devices[0]);

        string saved = PlayerPrefs.GetString(PrefKey, _devices[0]);

        // If saved device no longer exists (different PC), fallback safely.
        if (!_devices.Contains(saved))
        {
            saved = _devices[0];
            PlayerPrefs.SetString(PrefKey, saved);
            PlayerPrefs.Save();
        }
    }

    private void TryBindDropdownNow()
    {
        // Find by name anywhere in scene (including inactive objects).
        _dropdown = FindDropdownByName(dropdownObjectName);
        if (_dropdown == null)
            return;

        // Build options
        _dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        _dropdown.ClearOptions();

        if (_devices.Count == 0)
        {
            _dropdown.interactable = false;
            _dropdown.RefreshShownValue();
            return;
        }

        _dropdown.interactable = true;
        _dropdown.AddOptions(_devices);

        // Set current selection to saved device
        string saved = PlayerPrefs.GetString(PrefKey, _devices[0]);
        int index = Mathf.Max(0, _devices.IndexOf(saved));

        _dropdown.SetValueWithoutNotify(index);
        _dropdown.RefreshShownValue();

        _dropdown.onValueChanged.AddListener(OnDropdownChanged);

        if (logSelection)
            Debug.Log($"[MicManager] Bound dropdown '{dropdownObjectName}', selected '{GetCurrentDeviceName()}'");
    }

    private static TMP_Dropdown FindDropdownByName(string goName)
    {
        // FindObjectsOfType includes inactive if we pass true (Unity 2020+)
        var all = Object.FindObjectsOfType<TMP_Dropdown>(true);
        foreach (var dd in all)
        {
            if (dd != null && dd.gameObject.name == goName)
                return dd;
        }
        return null;
    }

    private void OnDropdownChanged(int index)
    {
        if (_devices.Count == 0) return;
        index = Mathf.Clamp(index, 0, _devices.Count - 1);

        string selected = _devices[index];

        PlayerPrefs.SetString(PrefKey, selected);
        PlayerPrefs.Save();

        // Make sure the dropdown label updates visually (fixes your "blank" case)
        if (_dropdown != null)
            _dropdown.RefreshShownValue();

        if (logSelection)
            Debug.Log($"[MicManager] Selected mic: {selected}");
    }

    public string GetCurrentDeviceName()
    {
        if (_devices.Count == 0) return string.Empty;

        string saved = PlayerPrefs.GetString(PrefKey, _devices[0]);
        return saved;
    }

    // Optional helper if you ever want to set from code (buttons, etc.)
    public void SetDeviceByName(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return;
        if (_devices.Count == 0) RefreshDeviceList();
        if (_devices.Count == 0) return;

        if (!_devices.Contains(deviceName))
            deviceName = _devices[0];

        PlayerPrefs.SetString(PrefKey, deviceName);
        PlayerPrefs.Save();

        if (_dropdown != null)
        {
            int idx = Mathf.Max(0, _devices.IndexOf(deviceName));
            _dropdown.SetValueWithoutNotify(idx);
            _dropdown.RefreshShownValue();
        }
    }
}