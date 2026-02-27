using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using TMPro;

public class SettingsBridge : MonoBehaviour
{
    [SerializeField] private Slider sensitivitySlider;

    [Header("Rebind UI (TextMeshPro)")]
    [SerializeField] private TMP_Text jumpText;
    [SerializeField] private TMP_Text sprintText;
    [SerializeField] private TMP_Text crouchText;
    [SerializeField] private TMP_Text freeLookText;

    private bool isRebinding = false;

    private void Start()
    {
        UpdateVisuals();
    }

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
    }

    public void UpdateSensitivity(float newValue)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", newValue);
        PlayerPrefs.Save();
    }
}