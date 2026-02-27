using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HeadLookIK : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraPivot;

    [Header("LookAt")]
    [Range(0f, 1f)] public float weight = 0.85f;
    [Range(0f, 1f)] public float bodyWeight = 0.25f;
    [Range(0f, 1f)] public float headWeight = 0.95f;
    [Range(0f, 1f)] public float eyesWeight = 1f;
    [Range(0f, 1f)] public float clampWeight = 0.25f;

    public float lookDistance = 10f;
    public bool enableLook = true;

    [Header("Smoothing")]
    [SerializeField] private float lookSmoothTime = 0.05f; // 0.03-0.08 is nice
    private Vector3 _lookPos;
    private Vector3 _lookVel;
    private bool _hasLookPos;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!IsOwner) return;
        if (!enableLook) return;
        if (!animator || !cameraPivot) return;

        Vector3 target = cameraPivot.position + cameraPivot.forward * lookDistance;

        if (!_hasLookPos)
        {
            _lookPos = target;
            _hasLookPos = true;
        }

        if (lookSmoothTime <= 0f)
            _lookPos = target;
        else
            _lookPos = Vector3.SmoothDamp(_lookPos, target, ref _lookVel, lookSmoothTime);

        animator.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(_lookPos);
    }
}