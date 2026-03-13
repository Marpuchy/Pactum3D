using UnityEngine;

public enum ModifierType
{
    FireDamage,
    Speed,
    HealOverTime,
    BuffStat,
    LavaImmunity
}

[CreateAssetMenu(menuName = "Items/Modifier Data")]
public class ModifierDataSO : ScriptableObject
{
    public ModifierType ModifierType;
    public float Value;
}
