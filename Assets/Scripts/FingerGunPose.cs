// FingerGunPose.cs
// MP-safe owner-only IK:
// - Owner uses Hand IK (camera anchor) while pose is active (smooth via blend)
// - Non-owners NEVER enable IK and ALWAYS get arm raise from this script
// - Fingers are always posed when w > 0
//
// IMPORTANT SETUP:
// 1) Assign cameraTransform to your pitch object (CameraPivot).
// 2) Assign ownerHandIK to the FingerGunHandIK component (same player).
// 3) FingerGunHandIK should have rightHandAnchor assigned + IK Pass enabled on Animator layer.
// 4) This version assumes FingerGunHandIK has: public bool enableIK; public float blend;

using FishNet.Object;
using UnityEngine;

public class FingerGunPose : MonoBehaviour
{
    public enum PitchAxis { None, LocalX, LocalY, LocalZ }

    [Header("References")]
    [SerializeField] private Animator animator;

    [Tooltip("Assign the object that pitches up/down (often CameraPivot).")]
    [SerializeField] private Transform cameraTransform;

    [Header("Owner-only IK while posing")]
    [Tooltip("If true, toggles FingerGunHandIK on ONLY for the local owner while pose weight > threshold.")]
    [SerializeField] private bool enableOwnerIKWhilePosing = true;

    [Tooltip("Reference to the FingerGunHandIK component on this player (assign in inspector).")]
    [SerializeField] private FingerGunHandIK ownerHandIK;

    [Tooltip("Pose weight above this turns IK on. 0.01 is a safe default.")]
    [SerializeField] private float ikEnableThreshold = 0.01f;

    [Header("Timing")]
    [SerializeField] private float raiseTime = 0.08f;
    [SerializeField] private float holdTime = 0.06f;
    [SerializeField] private float returnTime = 0.10f;

    [Header("Debug / Tuning")]
    [SerializeField] private bool alwaysPose = false;
    [Range(0f, 1f)][SerializeField] private float alwaysPoseWeight = 1f;

    [Header("Arm base target local rotations (your 'perfect straight look' pose)")]
    [SerializeField] private Vector3 upperArmEuler = new Vector3(-35f, 35f, 20f);
    [SerializeField] private Vector3 lowerArmEuler = new Vector3(-70f, 10f, 5f);
    [SerializeField] private Vector3 handEuler = new Vector3(0f, 20f, 0f);

    [Header("Camera pitch -> arm (reduce + clamp)")]
    [SerializeField] private bool aimWithCameraPitch = true;

    [Range(0f, 1.5f)]
    [SerializeField] private float pitchWeight = 0.35f;

    [SerializeField] private float maxUp = 35f;
    [SerializeField] private float maxDown = 25f;

    [SerializeField] private bool invertPitch = false;

    [Header("Pitch axes (fix twisting)")]
    [SerializeField] private PitchAxis upperArmPitchAxis = PitchAxis.LocalZ;
    [SerializeField] private PitchAxis lowerArmPitchAxis = PitchAxis.LocalZ;
    [SerializeField] private PitchAxis handPitchAxis = PitchAxis.LocalZ;

    [Header("Pitch distribution down the chain (small on forearm/hand)")]
    [SerializeField] private float upperArmPitchShare = 1.0f;
    [SerializeField] private float lowerArmPitchShare = 0.15f;
    [SerializeField] private float handPitchShare = 0.05f;

    [Header("Framing compensation (keeps arm in view)")]
    [SerializeField] private float lookUpPushDown = 12f;
    [SerializeField] private float lookDownPushUp = 6f;
    [SerializeField] private PitchAxis framingAxis = PitchAxis.LocalZ;

    [Header("Optional Shoulder follow")]
    [SerializeField] private bool pitchShoulderToo = false;
    [SerializeField] private float shoulderPitchScale = 0.2f;
    [SerializeField] private PitchAxis shoulderPitchAxis = PitchAxis.None;

    [Header("Fingers")]
    [SerializeField] private bool poseFingers = true;

    // Index straight
    [SerializeField] private Vector3 index1Euler = Vector3.zero;
    [SerializeField] private Vector3 index2Euler = Vector3.zero;
    [SerializeField] private Vector3 index3Euler = Vector3.zero;

    // Thumb up/out
    [SerializeField] private Vector3 thumb1Euler = new Vector3(0f, 20f, 20f);
    [SerializeField] private Vector3 thumb2Euler = new Vector3(0f, 10f, 10f);
    [SerializeField] private Vector3 thumb3Euler = Vector3.zero;

    // Other fingers curled
    [SerializeField] private Vector3 middle1Euler = new Vector3(40f, 0f, 0f);
    [SerializeField] private Vector3 middle2Euler = new Vector3(60f, 0f, 0f);
    [SerializeField] private Vector3 middle3Euler = new Vector3(40f, 0f, 0f);

    [SerializeField] private Vector3 ring1Euler = new Vector3(45f, 0f, 0f);
    [SerializeField] private Vector3 ring2Euler = new Vector3(65f, 0f, 0f);
    [SerializeField] private Vector3 ring3Euler = new Vector3(45f, 0f, 0f);

    [SerializeField] private Vector3 little1Euler = new Vector3(50f, 0f, 0f);
    [SerializeField] private Vector3 little2Euler = new Vector3(70f, 0f, 0f);
    [SerializeField] private Vector3 little3Euler = new Vector3(50f, 0f, 0f);

    // Bones
    private Transform rightShoulder;
    private Transform upperArm, lowerArm, hand;
    private Transform th1, th2, th3, i1, i2, i3, m1, m2, m3, r1, r2, r3, l1, l2, l3;

    // Weight animation
    private float weight = 0f;
    private float timer = 0f;
    private bool playing = false;

    // NEW: external lock (scramble/climb/etc)
    private bool _forcedDisabled = false;

    /// <summary>
    /// Called by PlayerMovement (SendMessage) to force the pose off during traversal.
    /// </summary>
    public void ForceDisable(bool disabled)
    {
        _forcedDisabled = disabled;

        if (disabled)
        {
            // stop any active pose immediately
            playing = false;
            timer = 0f;
            weight = 0f;

            // hard kill IK so it can't fight scramble animations
            ForceOwnerIKOff();
        }
    }

    private void ForceOwnerIKOff()
    {
        if (ownerHandIK == null) return;

        // Only the local owner should be able to change IK settings,
        // but turning it OFF is safe either way.
        ownerHandIK.enableIK = false;
        ownerHandIK.blend = 0f;
    }

    private bool OwnerIKIsActuallyRunning()
    {
        if (!enableOwnerIKWhilePosing) return false;
        if (ownerHandIK == null) return false;

        var nb = ownerHandIK.GetComponent<NetworkBehaviour>();
        if (nb != null && !nb.IsOwner) return false;

        return ownerHandIK.enableIK && ownerHandIK.blend > ikEnableThreshold;
    }

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        if (!cameraTransform)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam) cameraTransform = cam.transform;
        }

        CacheBones();
    }

    private void CacheBones()
    {
        if (!animator) return;

        rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (poseFingers)
        {
            th1 = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            th2 = animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
            th3 = animator.GetBoneTransform(HumanBodyBones.RightThumbDistal);

            i1 = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            i2 = animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
            i3 = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);

            m1 = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            m2 = animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
            m3 = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);

            r1 = animator.GetBoneTransform(HumanBodyBones.RightRingProximal);
            r2 = animator.GetBoneTransform(HumanBodyBones.RightRingIntermediate);
            r3 = animator.GetBoneTransform(HumanBodyBones.RightRingDistal);

            l1 = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            l2 = animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate);
            l3 = animator.GetBoneTransform(HumanBodyBones.RightLittleDistal);
        }
    }

    public void PlayFingerGunPose()
    {
        // NEW: ignore pose requests while forced off (scrambling/climbing)
        if (_forcedDisabled) return;

        if (!upperArm || !lowerArm || !hand)
            CacheBones();

        if (!upperArm || !lowerArm || !hand)
        {
            Debug.LogError("[FingerGunPose] Missing arm bones or animator not humanoid.", this);
            return;
        }

        weight = 0f;
        timer = 0f;
        playing = true;
    }

    private void Update()
    {
        // NEW: if traversal forced disabled, keep everything off
        if (_forcedDisabled)
        {
            playing = false;
            timer = 0f;
            weight = 0f;
            return;
        }

        if (alwaysPose)
        {
            playing = false;
            weight = 0f;
            return;
        }

        if (!playing) return;

        timer += Time.deltaTime;

        float tRaiseEnd = raiseTime;
        float tHoldEnd = raiseTime + holdTime;
        float tEnd = raiseTime + holdTime + returnTime;

        if (timer <= tRaiseEnd)
            weight = (raiseTime <= 0f) ? 1f : Mathf.Clamp01(timer / raiseTime);
        else if (timer <= tHoldEnd)
            weight = 1f;
        else if (timer <= tEnd)
        {
            float tt = timer - tHoldEnd;
            weight = (returnTime <= 0f) ? 0f : 1f - Mathf.Clamp01(tt / returnTime);
        }
        else
        {
            weight = 0f;
            playing = false;
        }
    }

    private void LateUpdate()
    {
        // NEW: forced disabled = force IK off and do nothing
        if (_forcedDisabled)
        {
            ForceOwnerIKOff();
            return;
        }

        float w = alwaysPose ? Mathf.Clamp01(alwaysPoseWeight) : weight;

        // MP-safe: ONLY owner toggles IK. Remotes force IK off so they always see arm raise from this script.
        if (enableOwnerIKWhilePosing && ownerHandIK != null)
        {
            var nb = ownerHandIK.GetComponent<NetworkBehaviour>();
            bool isOwner = (nb == null) ? true : nb.IsOwner;

            if (isOwner)
            {
                ownerHandIK.enableIK = (w > ikEnableThreshold);
                ownerHandIK.blend = (w > ikEnableThreshold) ? w : 0f;
            }
            else
            {
                ownerHandIK.enableIK = false;
                ownerHandIK.blend = 0f;
            }
        }

        if (w <= 0f) return;
        ApplyPose(w);
    }

    private float GetUpPositiveCameraPitch()
    {
        if (!cameraTransform) return 0f;

        float pitch = cameraTransform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        return -pitch; // up = positive
    }

    private void ApplyPose(float w)
    {
        float pitchAdd = 0f;

        if (aimWithCameraPitch && cameraTransform)
        {
            float upPitch = GetUpPositiveCameraPitch();
            float clamped = Mathf.Clamp(upPitch, -maxDown, maxUp);
            pitchAdd = clamped * pitchWeight;
            if (invertPitch) pitchAdd = -pitchAdd;
        }

        float comp = 0f;
        if (pitchAdd > 0.001f)
            comp = -Mathf.InverseLerp(0f, maxUp * pitchWeight, pitchAdd) * lookUpPushDown;
        else if (pitchAdd < -0.001f)
            comp = Mathf.InverseLerp(0f, maxDown * pitchWeight, -pitchAdd) * lookDownPushUp;

        Quaternion uaBase = Quaternion.Euler(upperArmEuler);
        Quaternion laBase = Quaternion.Euler(lowerArmEuler);
        Quaternion hBase = Quaternion.Euler(handEuler);

        Quaternion uaPitch = PitchRot(pitchAdd * upperArmPitchShare, upperArmPitchAxis);
        Quaternion laPitch = PitchRot(pitchAdd * lowerArmPitchShare, lowerArmPitchAxis);
        Quaternion hPitch = PitchRot(pitchAdd * handPitchShare, handPitchAxis);

        Quaternion uaFrame = PitchRot(comp, framingAxis);

        Quaternion uaTarget = uaBase * uaPitch * uaFrame;
        Quaternion laTarget = laBase * laPitch;
        Quaternion hTarget = hBase * hPitch;

        bool ikActive = OwnerIKIsActuallyRunning();
        if (!ikActive)
        {
            upperArm.localRotation = Quaternion.Slerp(upperArm.localRotation, uaTarget, w);
            lowerArm.localRotation = Quaternion.Slerp(lowerArm.localRotation, laTarget, w);
            hand.localRotation = Quaternion.Slerp(hand.localRotation, hTarget, w);

            if (pitchShoulderToo && rightShoulder && shoulderPitchAxis != PitchAxis.None)
            {
                Quaternion sPitch = PitchRot(pitchAdd * shoulderPitchScale, shoulderPitchAxis);
                rightShoulder.localRotation =
                    rightShoulder.localRotation * Quaternion.Slerp(Quaternion.identity, sPitch, w);
            }
        }

        if (!poseFingers) return;

        ApplyIf(th1, thumb1Euler, w); ApplyIf(th2, thumb2Euler, w); ApplyIf(th3, thumb3Euler, w);
        ApplyIf(i1, index1Euler, w); ApplyIf(i2, index2Euler, w); ApplyIf(i3, index3Euler, w);
        ApplyIf(m1, middle1Euler, w); ApplyIf(m2, middle2Euler, w); ApplyIf(m3, middle3Euler, w);
        ApplyIf(r1, ring1Euler, w); ApplyIf(r2, ring2Euler, w); ApplyIf(r3, ring3Euler, w);
        ApplyIf(l1, little1Euler, w); ApplyIf(l2, little2Euler, w); ApplyIf(l3, little3Euler, w);
    }

    private static Quaternion PitchRot(float degrees, PitchAxis axis)
    {
        if (axis == PitchAxis.None) return Quaternion.identity;
        return Quaternion.AngleAxis(degrees, AxisVector(axis));
    }

    private static Vector3 AxisVector(PitchAxis axis)
    {
        return axis switch
        {
            PitchAxis.LocalX => Vector3.right,
            PitchAxis.LocalY => Vector3.up,
            PitchAxis.LocalZ => Vector3.forward,
            _ => Vector3.forward
        };
    }

    private void ApplyIf(Transform bone, Vector3 euler, float w)
    {
        if (!bone) return;
        bone.localRotation = Quaternion.Slerp(bone.localRotation, Quaternion.Euler(euler), w);
    }
}