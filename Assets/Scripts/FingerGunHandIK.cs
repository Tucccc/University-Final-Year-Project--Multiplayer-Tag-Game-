using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FingerGunHandIK : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform rightElbowHint;

    [Header("Enable")]
    public bool enableIK = true;

    [Header("Blend (driven by FingerGunPose)")]
    [Range(0f, 1f)] public float blend = 0f;  // NEW: 0 = no IK, 1 = full IK
    [Range(0f, 1f)] public float elbowHintBlend = 0.6f;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!IsOwner) return;
        if (!enableIK) return;
        if (!animator || !rightHandAnchor) return;

        float w = Mathf.Clamp01(blend);

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);

        animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandAnchor.position);
        animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandAnchor.rotation);

        if (rightElbowHint)
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w * elbowHintBlend);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);
        }
        else
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        }
    }
}