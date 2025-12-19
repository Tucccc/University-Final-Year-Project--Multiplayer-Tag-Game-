using UnityEngine;

public class ArenaUiBootstrap : MonoBehaviour
{
    private void Start()
    {
        // Give the scene a moment to settle, then request UI state
        StartCoroutine(RequestSoon());
    }

    private System.Collections.IEnumerator RequestSoon()
    {
        yield return null;                  // 1 frame
        yield return new WaitForSeconds(0.1f);

        if (RoundManager.Instance != null)
        {
            // Ask server for the current timer/scores/running flag
            RoundManager.Instance.RequestUiSnapshotServerRpc();
        }
        else
        {
            Debug.LogWarning("[ArenaUiBootstrap] RoundManager.Instance is null on client.");
        }
    }
}
