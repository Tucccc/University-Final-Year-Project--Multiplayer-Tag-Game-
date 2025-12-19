using UnityEngine;

/// <summary>
/// Put this on your Round-Over overlay GameObject (which has a CanvasGroup).
/// Controls visibility and input blocking for the overlay.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class RoundOverPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();

        // Hidden by default on scene load.
        SetVisible(false);
    }

    public void SetVisible(bool show)
    {
        if (!canvasGroup) return;

        canvasGroup.alpha = show ? 1f : 0f;
        canvasGroup.blocksRaycasts = show;
        canvasGroup.interactable = show;
    }
}
