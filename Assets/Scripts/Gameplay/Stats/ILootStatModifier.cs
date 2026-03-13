public interface ILootStatModifier
{
    int Priority { get; }
    void Apply(LootStatQuery query);
}
