public sealed class DefaultItemFactory : IItemFactory
{
    public IItem Create(ItemDataSO data, int amount = 1)
    {
        return ItemFactory.CreateItem(data, amount);
    }
}
