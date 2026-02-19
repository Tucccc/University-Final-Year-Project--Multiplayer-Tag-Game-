using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class RoundOverPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    private void Reset() => canvasGroup = GetComponent<CanvasGroup>();

    private void Awake()
    {
        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();

        // Hide by default first.
        SetVisible(false);

        // Then apply buffered desired state.
        RoundOverUI.NotifyPanelReady(this);
    }

    public void SetVisible(bool show)
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = show ? 1f : 0f;
        canvasGroup.blocksRaycasts = show;
        canvasGroup.interactable = show;
    }
}
