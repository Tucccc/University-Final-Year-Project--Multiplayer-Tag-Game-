using UnityEngine;

public class VoiceSettingsRuntime : MonoBehaviour
{
    [SerializeField] private VoiceChat voiceChat;

    private const string PREF_PTT_ENABLED = "Voice_PTTEnabled";
    private const string PREF_VOICE_VOL = "Voice_Volume";
    private const string PREF_CHAT_TYPE = "Voice_ChatType"; // 0=Global, 1=Proximity
    private const string PREF_PROX_RANGE = "Voice_ProxRange";
    private const string PREF_VA_THRESH = "Voice_ActivationThreshold";

    private void Awake()
    {
        if (voiceChat == null)
            voiceChat = GetComponent<VoiceChat>();
    }

    private void Start()
    {
        ApplySaved();
    }

    public void SetVoiceVolume01(float v)
    {
        PlayerPrefs.SetFloat(PREF_VOICE_VOL, Mathf.Clamp01(v));
        PlayerPrefs.Save();
        ApplySaved();
    }

    public void SetPushToTalkEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(PREF_PTT_ENABLED, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplySaved();
    }

    public void SetChatTypeProximity(bool proximity)
    {
        PlayerPrefs.SetInt(PREF_CHAT_TYPE, proximity ? 1 : 0);
        PlayerPrefs.Save();
        ApplySaved();
    }

    public void SetProximityRange(float meters)
    {
        PlayerPrefs.SetFloat(PREF_PROX_RANGE, Mathf.Max(1f, meters));
        PlayerPrefs.Save();
        ApplySaved();
    }

    public void SetVoiceActivationThreshold(float t)
    {
        PlayerPrefs.SetFloat(PREF_VA_THRESH, Mathf.Max(0.0001f, t));
        PlayerPrefs.Save();
        ApplySaved();
    }

    private void ApplySaved()
    {
        if (voiceChat == null) return;

        // Volume affects playback
        if (voiceChat.source != null)
            voiceChat.source.volume = PlayerPrefs.GetFloat(PREF_VOICE_VOL, 1f);

        // Proximity/global options
        voiceChat.VoiceChatType =
            (VoiceChat.ChatType)PlayerPrefs.GetInt(PREF_CHAT_TYPE, (int)voiceChat.VoiceChatType);

        voiceChat.proximityRange =
            PlayerPrefs.GetFloat(PREF_PROX_RANGE, voiceChat.proximityRange);

        voiceChat.voiceActivationThreshold =
            PlayerPrefs.GetFloat(PREF_VA_THRESH, voiceChat.voiceActivationThreshold);

        // Push-to-talk vs voice activation
        bool pttEnabled = PlayerPrefs.GetInt(PREF_PTT_ENABLED, 1) == 1;
        voiceChat.VoiceDetectionType = pttEnabled
            ? VoiceChat.DetectionType.PushToTalk
            : VoiceChat.DetectionType.VoiceActivation;
    }
}