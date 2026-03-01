using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using TMPro;

public class SettingsBridge : MonoBehaviour
{
    [Header("Sensitivity")]
    [SerializeField] private Slider sensitivitySlider;

    [Header("Voice")]
    [SerializeField] private Toggle voicePTTToggle;         // Push-to-talk on/off
    [SerializeField] private Slider voiceVolumeSlider;      // 0..1 volume
    [SerializeField] private TMP_Text voicePTTKeyText;      // shows current PTT key

    [Header("Rebind UI (TextMeshPro)")]
    [SerializeField] private TMP_Text jumpText;
    [SerializeField] private TMP_Text sprintText;
    [SerializeField] private TMP_Text crouchText;
    [SerializeField] private TMP_Text freeLookText;

    private bool isRebinding = false;

    private void Start()
    {
        // (Optional) Set UI controls to saved values on open
        if (sensitivitySlider)
            sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSensitivity", 3f));

        if (voicePTTToggle)
            voicePTTToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Voice_PTTEnabled", 1) == 1);

        if (voiceVolumeSlider)
            voiceVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Volume", 1f));

        UpdateVisuals();
    }

    // Called by your rebind buttons, passing "JumpKey", "SprintKey", etc.
    public void StartRebind(string actionName)
    {
        if (!isRebinding)
            StartCoroutine(WaitForKeyPress(actionName));
    }

    private IEnumerator WaitForKeyPress(string actionName)
    {
        isRebinding = true;

        // ✅ Pick the correct TMP text to update
        TMP_Text targetText =
            actionName == "JumpKey" ? jumpText :
            actionName == "SprintKey" ? sprintText :
            actionName == "CrouchKey" ? crouchText :
            actionName == "FreeLook" ? freeLookText :
            actionName == "Voice_PTTKey" ? voicePTTKeyText :
            null;

        if (targetText != null)
            targetText.text = "Press Any Key...";

        yield return null;

        bool keyFound = false;
        while (!keyFound)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
                {
                    // ignore Mouse0 like you already do
                    if (Input.GetKeyDown(kcode) && kcode != KeyCode.Mouse0)
                    {
                        PlayerPrefs.SetInt(actionName, (int)kcode);
                        PlayerPrefs.Save();
                        keyFound = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        UpdateVisuals();
        isRebinding = false;
    }

    private void UpdateVisuals()
    {
        if (jumpText) jumpText.text = ((KeyCode)PlayerPrefs.GetInt("JumpKey", (int)KeyCode.Space)).ToString();
        if (sprintText) sprintText.text = ((KeyCode)PlayerPrefs.GetInt("SprintKey", (int)KeyCode.LeftShift)).ToString();
        if (crouchText) crouchText.text = ((KeyCode)PlayerPrefs.GetInt("CrouchKey", (int)KeyCode.LeftControl)).ToString();
        if (freeLookText) freeLookText.text = ((KeyCode)PlayerPrefs.GetInt("FreeLook", (int)KeyCode.LeftAlt)).ToString();

        // Voice PTT key label
        if (voicePTTKeyText)
            voicePTTKeyText.text = ((KeyCode)PlayerPrefs.GetInt("Voice_PTTKey", (int)KeyCode.V)).ToString();
    }

    // Hook your sensitivity slider OnValueChanged(float) to this
    public void UpdateSensitivity(float newValue)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", newValue);
        PlayerPrefs.Save();
    }

    // Hook your voice volume slider OnValueChanged(float) to this
    public void UpdateVoiceVolume(float newValue01)
    {
        PlayerPrefs.SetFloat("Voice_Volume", Mathf.Clamp01(newValue01));
        PlayerPrefs.Save();
    }

    // Hook your PTT toggle OnValueChanged(bool) to this
    public void SetVoicePTTEnabled(bool enabled)
    {
        PlayerPrefs.SetInt("Voice_PTTEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}