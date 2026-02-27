using UnityEngine;

public class UpperBodyPitchFollowCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform; // the look camera

    [Header("How much to follow camera pitch")]
    [Range(0f, 1f)]
    [SerializeField] private float followWeight = 0.7f;

    [Tooltip("Clamp applied pitch (degrees). Keeps it from looking broken.")]
    [SerializeField] private float maxUp = 35f;
    [SerializeField] private float maxDown = 25f;

    [Header("Distribution (0..1)")]
    [Tooltip("How much of the pitch goes into chest/spine vs shoulder.")]
    [Range(0f, 1f)]
    [SerializeField] private float chestShare = 0.8f;

    [Tooltip("Optional extra: small shoulder pitch to keep arm in view.")]
    [Range(0f, 1f)]
    [SerializeField] private float shoulderShare = 0.2f;

    Transform spine, chest, upperChest, rightShoulder;

    Quaternion spineBase, chestBase, upperChestBase, shoulderBase;
    bool cachedBases = false;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!cameraTransform)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam) cameraTransform = cam.transform;
        }

        CacheBones();
    }

    void CacheBones()
    {
        if (!animator) return;

        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
    }

    void LateUpdate()
    {
        if (!animator || !cameraTransform) return;
        if (!spine && !chest && !upperChest) return;

        // Capture base rotations AFTER animator has posed this frame
        // (so we add pitch on top of the current animation)
        spineBase = spine ? spine.localRotation : Quaternion.identity;
        chestBase = chest ? chest.localRotation : Quaternion.identity;
        upperChestBase = upperChest ? upperChest.localRotation : Quaternion.identity;
        shoulderBase = rightShoulder ? rightShoulder.localRotation : Quaternion.identity;

        // Convert camera pitch to [-180, 180]
        float pitch = cameraTransform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        // In Unity, looking up is often negative pitch. We'll invert so up = +.
        float upPositivePitch = -pitch;

        float clamped = Mathf.Clamp(upPositivePitch, -maxDown, maxUp) * followWeight;

        // Apply distributed pitch (about local X axis)
        float chestPitch = clamped * chestShare;
        float shoulderPitch = clamped * shoulderShare;

        // Apply to chest/spine chain (prefer UpperChest->Chest->Spine if available)
        if (upperChest)
            upperChest.localRotation = upperChestBase * Quaternion.Euler(chestPitch, 0f, 0f);

        if (chest)
            chest.localRotation = chestBase * Quaternion.Euler(chestPitch, 0f, 0f);

        if (spine)
            spine.localRotation = spineBase * Quaternion.Euler(chestPitch * 0.6f, 0f, 0f);

        // Small amount to shoulder (optional)
        if (rightShoulder)
            rightShoulder.localRotation = shoulderBase * Quaternion.Euler(shoulderPitch, 0f, 0f);
    }
}