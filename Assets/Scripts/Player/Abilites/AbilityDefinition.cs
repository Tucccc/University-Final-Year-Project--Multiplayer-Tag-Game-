using UnityEngine;

public enum AbilityFireType { Hitscan }
public enum AbilityEffectType { Freeze, Blast }

[CreateAssetMenu(menuName = "Abilities/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "freeze_shot";
    public string displayName = "Freeze Shot";
    public Sprite icon;

    [Header("Roll Weight")]
    public int weight = 10;

    [Header("Fire")]
    public AbilityFireType fireType = AbilityFireType.Hitscan;
    public float range = 60f;

    [Header("Effect")]
    public AbilityEffectType effectType = AbilityEffectType.Freeze;
    public float freezeDuration = 1.5f;

    [Header("Blast")]
    public float blastRadius = 5f;
    public float blastForce = 10f;
    public float blastUpLift = 1f;
    public float selfForceMultiplier = 1f;
    public float ragdollDuration = 2f;
}