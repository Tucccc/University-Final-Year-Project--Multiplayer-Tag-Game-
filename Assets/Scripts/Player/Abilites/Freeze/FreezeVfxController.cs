using UnityEngine;

public class FreezeVfxController : MonoBehaviour
{
    [SerializeField] private StatusController status;
    [SerializeField] private GameObject freezeFxObject;

    private bool _lastFrozen;

    private void Awake()
    {
        if (status == null)
            status = GetComponentInParent<StatusController>();

        if (freezeFxObject != null)
            freezeFxObject.SetActive(false);
    }

    private void Update()
    {
        bool frozen = status.IsFrozen;
        if (frozen != _lastFrozen)
        {
            Debug.Log($"[FreezeVFX] {gameObject.name} frozen={frozen} -> FX {(frozen ? "ON" : "OFF")}");
            _lastFrozen = frozen;
            freezeFxObject.SetActive(frozen);
        }

    }
}