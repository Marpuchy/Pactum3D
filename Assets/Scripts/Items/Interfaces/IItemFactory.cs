public interface IItemFactory
{
    IItem Create(ItemDataSO data, int amount = 1);
}
