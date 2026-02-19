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
    public TextMeshProUGUI winnerText;

    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text bigMessageText;

    // ---- Buffers so late UI still shows correct state ----
    private static string[] _bufferNames;
    private static int[] _bufferScores;
    private static bool _haveBufferedScores;

    private static int _bufferTimer = -1;
    private static bool _haveBufferedTimer;

    private static bool _bufferWinnerVisible;
    private static bool _haveBufferedWinnerVisible;

    // Round over / big message buffering (THIS was missing).
    private static bool _bufferRoundOverVisible;
    private static bool _haveBufferedRoundOverVisible;
    private static string _bufferBigMessage;
    private static bool _bufferBigVisible;
    private static bool _haveBufferedBigVisible;

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

        // Default hidden
        if (winnerText) winnerText.gameObject.SetActive(false);
        if (bigMessageText) bigMessageText.gameObject.SetActive(false);

        // Apply buffered payloads immediately.
        if (_haveBufferedScores && _bufferNames != null && _bufferScores != null)
            Refresh(_bufferNames, _bufferScores);

        if (_haveBufferedTimer)
            ApplyTimer(_bufferTimer);

        // Apply buffered round over + big message state (so late UI still shows).
        if (_haveBufferedBigVisible && bigMessageText)
        {
            bigMessageText.text = _bufferBigMessage ?? "";
            bigMessageText.gameObject.SetActive(_bufferBigVisible);
        }

        if (_haveBufferedWinnerVisible && winnerText)
            winnerText.gameObject.SetActive(_bufferWinnerVisible);

        if (_haveBufferedRoundOverVisible && _bufferRoundOverVisible)
            ApplyRoundOverNow();

        // Keep winner text content updated, but DO NOT force it visible here.
        var rm = RoundManager.Instance;
        if (rm != null && winnerText != null)
        {
            rm.WinnerName.OnChange += (_, __, ___) =>
            {
                var w = rm.WinnerName.Value;
                winnerText.text = string.IsNullOrWhiteSpace(w) ? "" : $"{w} WINS!";
            };
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

        // fallback for old input
        if (Input.GetKey(KeyCode.Tab)) wantVisible = true;

        if (wantVisible != _isVisible)
            ShowScoreboard(wantVisible);
    }

    private void ShowScoreboard(bool show)
    {
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

    /// <summary>
    /// Show Round Over UI on EVERY client (even if UI is not spawned yet).
    /// </summary>
    public static void ShowRoundOver()
    {
        _bufferRoundOverVisible = true;
        _haveBufferedRoundOverVisible = true;

        _bufferBigMessage = "ROUND OVER!";
        _bufferBigVisible = true;
        _haveBufferedBigVisible = true;

        _bufferWinnerVisible = true;
        _haveBufferedWinnerVisible = true;

        if (_instance == null) return;
        _instance.ApplyRoundOverNow();
    }

    /// <summary>
    /// Call this at the start of a new round to clear old winner/roundover UI.
    /// </summary>
    public static void HideRoundOver()
    {
        _bufferRoundOverVisible = false;
        _haveBufferedRoundOverVisible = true;

        _bufferBigVisible = false;
        _haveBufferedBigVisible = true;

        _bufferWinnerVisible = false;
        _haveBufferedWinnerVisible = true;

        if (_instance == null) return;

        if (_instance.bigMessageText)
            _instance.bigMessageText.gameObject.SetActive(false);

        if (_instance.winnerText)
            _instance.winnerText.gameObject.SetActive(false);
    }

    // Keep old API but make it reliable.
    public static void HideWinner()
    {
        _bufferWinnerVisible = false;
        _haveBufferedWinnerVisible = true;

        if (_instance?.winnerText)
            _instance.winnerText.gameObject.SetActive(false);
    }

    public static void ShowBig(string msg)
    {
        _bufferBigMessage = msg;
        _bufferBigVisible = true;
        _haveBufferedBigVisible = true;

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

    private void ApplyRoundOverNow()
    {
        if (bigMessageText)
        {
            bigMessageText.text = "ROUND OVER!";
            bigMessageText.gameObject.SetActive(true);
        }

        if (winnerText)
        {
            var winner = RoundManager.Instance != null ? RoundManager.Instance.WinnerName.Value : "";
            winnerText.text = string.IsNullOrWhiteSpace(winner) ? "" : $"{winner} WINS!";
            winnerText.gameObject.SetActive(true);
        }
    }

    private System.Collections.IEnumerator HideBigAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bigMessageText) bigMessageText.gameObject.SetActive(false);

        // Update buffer too so late-created UI doesn't re-show it.
        _bufferBigVisible = false;
        _haveBufferedBigVisible = true;
    }

    private void ApplyTimer(int secondsLeft)
    {
        if (timerText)
        {
            int safe = Mathf.Max(0, secondsLeft);
            int m = safe / 60;
            int s = safe % 60;
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
                if (t.name.Contains("Name")) t.text = names[i];
                if (t.name.Contains("Score")) t.text = scores[i].ToString();
            }
        }
    }
}
