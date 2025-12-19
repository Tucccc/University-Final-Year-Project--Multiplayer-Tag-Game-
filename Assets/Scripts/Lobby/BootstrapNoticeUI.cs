using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class BootstrapNoticeUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text label;           // Assign in inspector (the text to show)
    [SerializeField] private float fadeDuration = 1f;  // Seconds for fade in/out
    [SerializeField] private float displayTime = 4f;   // Seconds to stay visible

    private CanvasGroup group;

    private void Awake()
    {
        // Ensure movement is never locked when returning to bootstrap
        RoundOverUI.SetControlsEnabled(true);   // or false

        // Create a CanvasGroup if one isn't already present
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        // If a message was set, show it and fade
        if (!string.IsNullOrWhiteSpace(DisconnectNotice.Message))
        {
            label.text = DisconnectNotice.Message;
            StartCoroutine(FadeSequence());
        }
        else
        {
            // Nothing to display
            label.text = string.Empty;
            group.alpha = 0;
        }
    }

    private IEnumerator FadeSequence()
    {
        // Fade in
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }

        // Wait
        yield return new WaitForSeconds(displayTime);

        // Fade out
        t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(1, 0, t / fadeDuration);
            yield return null;
        }

        // Clear text for cleanliness
        label.text = string.Empty;
        DisconnectNotice.Message = string.Empty;
    }
}
