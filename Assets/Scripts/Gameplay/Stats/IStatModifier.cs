public interface IStatModifier
{
    int Priority { get; }
    void Apply(StatQuery query);
}
