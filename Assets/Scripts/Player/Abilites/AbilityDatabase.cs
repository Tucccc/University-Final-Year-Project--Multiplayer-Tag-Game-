using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Ability Database")]
public class AbilityDatabase : ScriptableObject
{
    public List<AbilityDefinition> abilities = new();

    public AbilityDefinition GetById(string id)
    {
        for (int i = 0; i < abilities.Count; i++)
            if (abilities[i] != null && abilities[i].id == id)
                return abilities[i];
        return null;
    }
}