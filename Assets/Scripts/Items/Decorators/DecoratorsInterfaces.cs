public interface IFireDamage
{
    float FireDamage { get; }
}

public interface ILavaImmunity
{
    bool IsImmuneToLava { get; }
}

public interface ISpeedModifier
{
    float SpeedMultiplier { get; }
}

public interface IHealOverTime
{
    float HealPerSecond { get; }
}

public interface IStatBuff
{
    float BuffValue { get; }
}