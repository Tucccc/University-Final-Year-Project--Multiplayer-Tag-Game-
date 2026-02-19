using UnityEngine;

public class DisableTrace : MonoBehaviour
{
    private void OnDisable()
    {
        Debug.LogError($"[DisableTrace] {name} was DISABLED.\nStack:\n{System.Environment.StackTrace}");
    }
}
