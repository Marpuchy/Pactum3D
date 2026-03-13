using UnityEngine;

public interface IWeapon
{
    float Damage { get; }
    float AttackSpeed { get; }
}

public interface IArmor
{
    float Defense { get; }
    float HealthBonus { get; }
}

public interface IPassive
{
    float Multiplier { get; }
}