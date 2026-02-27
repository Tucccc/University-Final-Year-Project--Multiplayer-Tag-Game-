using UnityEngine;
using UnityEngine.Animations.Rigging;

public class RightArmViewLock : MonoBehaviour
{
    [SerializeField] private TwoBoneIKConstraint rightArmIK;
    [SerializeField, Range(0f, 1f)] private float lockedWeight = 1f;
    [SerializeField] private float blendSpeed = 14f;

    float targetWeight;
    float currentWeight;

    void Awake()
    {
        if (!rightArmIK) rightArmIK = GetComponentInChildren<TwoBoneIKConstraint>(true);
        currentWeight = rightArmIK != null ? rightArmIK.weight : 0f;
        targetWeight = currentWeight;
    }

    void Update()
    {
        if (!rightArmIK) return;

        currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendSpeed * Time.deltaTime);
        rightArmIK.weight = currentWeight;
    }

    public void SetLocked(bool locked)
    {
        targetWeight = locked ? lockedWeight : 0f;
    }

    // optional: quick pulse when tagging
    public void Pulse(float seconds = 0.12f)
    {
        StopAllCoroutines();
        StartCoroutine(PulseRoutine(seconds));
    }

    System.Collections.IEnumerator PulseRoutine(float seconds)
    {
        SetLocked(true);
        yield return new WaitForSeconds(seconds);
        SetLocked(false);
    }
}