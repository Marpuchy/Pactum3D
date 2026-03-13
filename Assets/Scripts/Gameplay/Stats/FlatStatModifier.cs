public sealed class FlatStatModifier : IStatModifier
{
    private readonly StatType type;
    private readonly float amount;
    private readonly int priority;

    public FlatStatModifier(StatType type, float amount, int priority = 0)
    {
        this.type = type;
        this.amount = amount;
        this.priority = priority;
    }

    public int Priority => priority;

    public void Apply(StatQuery query)
    {
        if (query == null || query.Type != type)
            return;

        query.Value += amount;
    }
}
