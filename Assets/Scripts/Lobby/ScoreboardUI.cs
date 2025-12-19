// Scripts/UI/ScoreboardUI.cs
using UnityEngine;
using TMPro;

public class ScoreboardUI : MonoBehaviour
{
    private static ScoreboardUI _instance;

    [Header("Refs")]
    [SerializeField] private Transform scoreContainer;
    [SerializeField] private GameObject rowPrefab;

    [Header("UI Sections")]
    [SerializeField] private CanvasGroup scorePanelGroup; // assign your ScorePanel (or Scroll View parent) CanvasGroup

    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text bigMessageText;

    // ---- Buffers so late UI still shows correct state ----
    private static string[] _bufferNames;
    private static int[] _bufferScores;
    private static bool _haveBufferedScores;
    private static int _bufferTimer = -1;
    private static bool _haveBufferedTimer;
    private static bool _desiredScoreboardVisible; // for Tab hold state if you want to persist

    private bool _isVisible;

    private void Awake()
    {
        _instance = this;

        // Find scorePanelGroup if not assigned
        if (scorePanelGroup == null)
        {
            var tr = transform.Find("ScorePanel");
            if (tr) scorePanelGroup = tr.GetComponent<CanvasGroup>() ?? tr.gameObject.AddComponent<CanvasGroup>();
        }
        if (scorePanelGroup)
        {
            scorePanelGroup.alpha = 0f;
            scorePanelGroup.interactable = false;
            scorePanelGroup.blocksRaycasts = false;
        }

        if (bigMessageText) bigMessageText.gameObject.SetActive(false);

        // Apply buffered payloads immediately.
        if (_haveBufferedScores && _bufferNames != null && _bufferScores != null)
        {
            Refresh(_bufferNames, _bufferScores);
        }
        if (_haveBufferedTimer)
        {
            ApplyTimer(_bufferTimer);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        bool wantVisible = false;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null) wantVisible = kb.tabKey.isPressed;
        wantVisible = Input.GetKey(KeyCode.Tab);

        if (wantVisible != _isVisible)
            ShowScoreboard(wantVisible);
    }


    private void ShowScoreboard(bool show)
    {
        _desiredScoreboardVisible = show;
        if (scorePanelGroup == null) return;
        _isVisible = show;
        scorePanelGroup.alpha = show ? 1f : 0f;
        scorePanelGroup.interactable = show;
        scorePanelGroup.blocksRaycasts = show;
    }

    // ---------- CALLED BY SERVER VIA RPCS (RoundManager) ----------

    public static void UpdateAll(string[] names, int[] scores)
    {
        _bufferNames = names;
        _bufferScores = scores;
        _haveBufferedScores = true;

        if (_instance != null)
            _instance.Refresh(names, scores);
    }

    public static void UpdateTimer(int secondsLeft)
    {
        _bufferTimer = secondsLeft;
        _haveBufferedTimer = true;

        if (_instance != null)
            _instance.ApplyTimer(secondsLeft);
    }

    public static void ShowRoundOver()
    {
        if (_instance == null) return;
        if (_instance.bigMessageText)
        {
            _instance.bigMessageText.text = "ROUND OVER!";
            _instance.bigMessageText.gameObject.SetActive(true);
        }
    }

    public static void ShowBig(string msg)
    {
        if (_instance == null) return;
        if (_instance.bigMessageText)
        {
            _instance.bigMessageText.text = msg;
            _instance.bigMessageText.gameObject.SetActive(true);
            _instance.StopAllCoroutines();
            _instance.StartCoroutine(_instance.HideBigAfter(1.5f));
        }
    }

    // ---------- Internal apply helpers ----------

    private System.Collections.IEnumerator HideBigAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bigMessageText) bigMessageText.gameObject.SetActive(false);
    }

    private void ApplyTimer(int secondsLeft)
    {
        if (timerText)
        {
            int m = Mathf.Max(0, secondsLeft) / 60;
            int s = Mathf.Max(0, secondsLeft) % 60;
            timerText.text = $"{m:0}:{s:00}";
        }
    }

    private void Refresh(string[] names, int[] scores)
    {
        if (scoreContainer == null || rowPrefab == null) return;

        // Clear rows
        for (int i = scoreContainer.childCount - 1; i >= 0; i--)
            Destroy(scoreContainer.GetChild(i).gameObject);

        // Build rows
        for (int i = 0; i < names.Length; i++)
        {
            var row = Instantiate(rowPrefab, scoreContainer);
            foreach (var t in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name.Contains("Name"))  t.text = names[i];
                if (t.name.Contains("Score")) t.text = scores[i].ToString();
            }

        }
    }
}
