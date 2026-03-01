using UnityEngine;

public class VoiceSystemBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject voiceSystemPrefab; // Prefab with MicrophoneManager

    private static bool _spawned;

    private void Awake()
    {
        if (_spawned) { Destroy(gameObject); return; }
        _spawned = true;

        if (FindObjectOfType<MicrophoneManager>(true) == null && voiceSystemPrefab != null)
            Instantiate(voiceSystemPrefab);

        DontDestroyOnLoad(gameObject);

        var mm = FindObjectOfType<MicrophoneManager>(true);
        if (mm != null) DontDestroyOnLoad(mm.gameObject);
    }
}