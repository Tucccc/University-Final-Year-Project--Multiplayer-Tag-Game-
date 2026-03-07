using UnityEngine;

public class VoiceVolumeApplier : MonoBehaviour
{
    [SerializeField] private AudioSource voiceSource;
    private const string PrefKey = "VoiceVolume";

    private void Awake()
    {
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        ApplySaved();
    }

    public void SetVoiceVolume01(float v)
    {
        v = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefKey, v);
        PlayerPrefs.Save();
        ApplySaved();
    }

    private void ApplySaved()
    {
        if (voiceSource == null) return;
        float v = PlayerPrefs.GetFloat(PrefKey, 1f);
        voiceSource.volume = v;
    }
}