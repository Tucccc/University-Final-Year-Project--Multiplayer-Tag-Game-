using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

public class VoiceChat : NetworkBehaviour
{
    public enum ChatType { Global, Proximity }
    public ChatType VoiceChatType = ChatType.Global;

    public enum DetectionType { PushToTalk, VoiceActivation }
    public DetectionType VoiceDetectionType = DetectionType.PushToTalk;

    public bool Activated = true;
    public KeyCode PushToTalkKey = KeyCode.V;

    public AudioSource source;
    public float proximityRange = 10f;
    public float voiceActivationThreshold = 0.002f;

    private bool canTalk = true;
    private bool previousCanTalk = false;

    private string deviceName;
    private const int sampleRate = 48000;
    private const int bufferSize = 16384;

    private float[] audioBuffer;
    private int position;

    private AudioClip microphoneClip;

    private float[] sampleData;
    private float[] micDataBuffer;

    private Coroutine transmitRoutine;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (source == null)
            Debug.LogError("[VOICE] AudioSource not assigned!");

        // Allocate buffers for owner (mic) + for safety
        audioBuffer = new float[bufferSize];
        sampleData = new float[bufferSize];
        micDataBuffer = new float[bufferSize];

        if (source != null)
            source.playOnAwake = false;

        if (!IsOwner)
            return;

        deviceName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (string.IsNullOrEmpty(deviceName))
            Debug.LogError("[VOICE] No microphone device found!");
    }

    private void Update()
    {
        // Apply volume for everyone (playback)
        if (source != null)
            source.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Voice_Volume", 1f));

        if (!Activated || !IsOwner)
            return;

        // Read PTT settings from your central settings system (same style as JumpKey etc.)
        bool pttEnabled = PlayerPrefs.GetInt("Voice_PTTEnabled", 1) == 1;
        VoiceDetectionType = pttEnabled ? DetectionType.PushToTalk : DetectionType.VoiceActivation;

        PushToTalkKey = (KeyCode)PlayerPrefs.GetInt("Voice_PTTKey", (int)KeyCode.V);

        // Mic device from your MicrophoneManager (if present)
        if (MicrophoneManager.Instance != null)
        {
            string selectedDevice = MicrophoneManager.Instance.GetCurrentDeviceName();
            if (!string.IsNullOrEmpty(selectedDevice) && selectedDevice != deviceName)
                UpdateMicrophone(selectedDevice);
        }

        switch (VoiceDetectionType)
        {
            case DetectionType.PushToTalk:
                canTalk = Input.GetKey(PushToTalkKey);

                if (canTalk && microphoneClip == null)
                {
                    StartMicrophone();
                    StartTalking();
                }
                else if (!canTalk && microphoneClip != null)
                {
                    StopTalking();
                    StopMicrophone();
                }
                break;

            case DetectionType.VoiceActivation:
                if (microphoneClip == null)
                    StartMicrophone();

                canTalk = IsVoiceActivated();
                break;
        }

        if (!previousCanTalk && canTalk)
            StartTalking();

        if (previousCanTalk && !canTalk)
            StopTalking();

        previousCanTalk = canTalk;
    }

    private void StartMicrophone()
    {
        if (string.IsNullOrEmpty(deviceName))
            return;

        position = 0;
        microphoneClip = Microphone.Start(deviceName, true, 10, sampleRate);
    }

    private void StopMicrophone()
    {
        if (string.IsNullOrEmpty(deviceName))
            return;

        Microphone.End(deviceName);
        microphoneClip = null;
        position = 0;
    }

    private void UpdateMicrophone(string newDeviceName)
    {
        if (string.IsNullOrEmpty(newDeviceName))
            return;

        Debug.Log($"[VOICE] Switching microphone from '{deviceName}' to '{newDeviceName}'");

        // Stop safely
        StopTalking();
        StopMicrophone();

        deviceName = newDeviceName;

        if (canTalk)
        {
            StartMicrophone();
            StartTalking();
        }
    }

    private void StartTalking()
    {
        if (string.IsNullOrEmpty(deviceName))
            return;

        if (microphoneClip == null)
            return;

        if (transmitRoutine != null)
            return;

        transmitRoutine = StartCoroutine(TransmitVoice());
    }

    private void StopTalking()
    {
        if (transmitRoutine == null)
            return;

        StopCoroutine(transmitRoutine);
        transmitRoutine = null;
    }

    private IEnumerator TransmitVoice()
    {
        while (canTalk)
        {
            if (microphoneClip == null)
                yield break;

            int micPosition = Microphone.GetPosition(deviceName);
            if (micPosition < 0)
            {
                yield return null;
                continue;
            }

            if (micPosition < position)
                position = micPosition;

            if (position + bufferSize > micPosition)
            {
                yield return null;
                continue;
            }

            microphoneClip.GetData(audioBuffer, position);
            position = (position + bufferSize) % microphoneClip.samples;

            TransmitAudioServerRpc(audioBuffer);

            yield return new WaitForSeconds(bufferSize / (float)sampleRate);
        }
    }

    private bool IsVoiceActivated()
    {
        if (microphoneClip == null)
            return false;

        int micPosition = Microphone.GetPosition(deviceName);
        int sampleStartPosition = micPosition - bufferSize;
        if (sampleStartPosition < 0)
            return false;

        microphoneClip.GetData(sampleData, sampleStartPosition);

        float sum = 0f;
        for (int i = 0; i < sampleData.Length; i++)
            sum += Mathf.Abs(sampleData[i]);

        float average = sum / sampleData.Length;
        return average > voiceActivationThreshold;
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransmitAudioServerRpc(float[] audioData, NetworkConnection sender = null)
    {
        if (sender == null) return;
        TransmitAudioObserversRpc(audioData, sender.ClientId);
    }

    [ObserversRpc]
    private void TransmitAudioObserversRpc(float[] audioData, int senderClientId)
    {
        // Ensure we do not play our own voice
        if (NetworkManager.ClientManager != null &&
            NetworkManager.ClientManager.Connection != null &&
            senderClientId == NetworkManager.ClientManager.Connection.ClientId)
            return;

        PlayReceivedAudio(audioData, senderClientId);
    }

    private void PlayReceivedAudio(float[] audioData, int senderClientId)
    {
        if (source == null)
            return;

        // Set spatial blend based on chat type
        if (VoiceChatType == ChatType.Proximity)
        {
            source.spatialBlend = 1.0f;
            source.maxDistance = proximityRange;

            Transform senderTransform = GetPlayerTransform(senderClientId);
            if (senderTransform != null)
            {
                float distance = Vector3.Distance(transform.position, senderTransform.position);
                if (distance > proximityRange)
                    return;
            }
        }
        else
        {
            source.spatialBlend = 0.0f;
        }

        AudioClip clip = AudioClip.Create("ReceivedVoice", audioData.Length, 1, sampleRate, false);
        clip.SetData(audioData, 0);

        source.clip = clip;
        source.Play();
    }

    private Transform GetPlayerTransform(int clientId)
    {
        foreach (var obj in FindObjectsOfType<NetworkObject>())
        {
            if (obj != null && obj.Owner.IsValid && obj.Owner.ClientId == clientId)
                return obj.transform;
        }
        return null;
    }

    private float GetMicInputVolume()
    {
        if (microphoneClip == null || string.IsNullOrEmpty(deviceName))
            return 0f;

        int micPosition = Microphone.GetPosition(deviceName);
        int sampleStartPosition = micPosition - bufferSize;
        if (sampleStartPosition < 0)
            return 0f;

        microphoneClip.GetData(micDataBuffer, sampleStartPosition);

        float sum = 0f;
        for (int i = 0; i < micDataBuffer.Length; i++)
            sum += micDataBuffer[i] * micDataBuffer[i];

        float rmsValue = Mathf.Sqrt(sum / micDataBuffer.Length);
        return Mathf.Clamp(rmsValue * 50f, 0f, 1f);
    }
}