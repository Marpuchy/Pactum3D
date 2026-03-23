public sealed class MultiplicativeStatModifier : IStatModifier
{
    private readonly StatType type;
    private readonly float factor;
    private readonly int priority;

    public MultiplicativeStatModifier(StatType type, float factor, int priority = 100)
    {
        this.type = type;
        this.factor = factor;
        this.priority = priority;
    }

    public int Priority => priority;

    public void Apply(StatQuery query)
    {
        if (query == null || query.Type != type)
            return;

        query.Value *= factor;
    }
}
