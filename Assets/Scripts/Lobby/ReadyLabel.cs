using UnityEngine;
#if TMP_PRESENT
using TMPro;
#endif

public class ReadyLabel : MonoBehaviour
{
#if TMP_PRESENT
    [SerializeField] private TMP_Text label;
#endif

    private void Awake()
    {
#if TMP_PRESENT
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
#endif
        SetText(0, 0);
    }

    private void OnEnable() => ReadyLabel_OnCountsChanged += OnCountsChanged;
    private void OnDisable() => ReadyLabel_OnCountsChanged -= OnCountsChanged;

    private void OnCountsChanged(int ready, int total) => SetText(ready, total);

    private void SetText(int ready, int total)
    {
#if TMP_PRESENT
        if (label) label.text = $"Ready: {ready} / {total}";
#endif
    }

    public static void NotifyAll(int ready, int total)
        => ReadyLabel_OnCountsChanged?.Invoke(ready, total);

    private static event System.Action<int, int> ReadyLabel_OnCountsChanged;
}
