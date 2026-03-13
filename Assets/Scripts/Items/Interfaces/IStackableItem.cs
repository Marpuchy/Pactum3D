public interface IStackableItem : IItem, IItemDataProvider
{
    int Count { get; }
    void Add(int amount);
    void Remove(int amount);
}
