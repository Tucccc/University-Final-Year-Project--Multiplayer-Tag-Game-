using UnityEngine;

public class StatusVfxController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StatusController status;

    [Header("Effect Objects")]
    [SerializeField] private GameObject freezeFxObject;
    [SerializeField] private GameObject blockFxObject;

    [Header("Options")]
    [SerializeField] private bool autoFindStatusInParent = true;
    [SerializeField] private bool disableEffectsOnStart = true;

    private bool _lastFrozen;
    private bool _lastTagImmune;
    private bool _initialized;

    private void Awake()
    {
        if (status == null && autoFindStatusInParent)
            status = GetComponentInParent<StatusController>();

        if (disableEffectsOnStart)
        {
            if (freezeFxObject != null)
                freezeFxObject.SetActive(false);

            if (blockFxObject != null)
                blockFxObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (status == null)
        {
            Debug.LogWarning($"[StatusVFX] No StatusController found on {gameObject.name}");
            enabled = false;
            return;
        }

        _lastFrozen = !status.IsFrozen;
        _lastTagImmune = !status.IsTagImmune;

        RefreshEffects();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || status == null)
            return;

        RefreshEffects();
    }

    private void RefreshEffects()
    {
        bool frozen = status.IsFrozen;
        if (frozen != _lastFrozen)
        {
            _lastFrozen = frozen;

            if (freezeFxObject != null)
                freezeFxObject.SetActive(frozen);

            Debug.Log($"[StatusVFX] {gameObject.name} freeze FX {(frozen ? "ON" : "OFF")}");
        }

        bool tagImmune = status.IsTagImmune;
        if (tagImmune != _lastTagImmune)
        {
            _lastTagImmune = tagImmune;

            if (blockFxObject != null)
                blockFxObject.SetActive(tagImmune);

            Debug.Log($"[StatusVFX] {gameObject.name} block FX {(tagImmune ? "ON" : "OFF")}");
        }
    }
}