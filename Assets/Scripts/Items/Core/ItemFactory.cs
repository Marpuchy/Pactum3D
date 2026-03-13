using UnityEngine;
using Zenject;

public static class ItemFactory
{
    private static IInstantiator instantiator;

    public static void SetInstantiator(IInstantiator newInstantiator)
    {
        instantiator = newInstantiator;
    }

    public static IItem CreateItem(ItemDataSO data, int amount = 1)
    {
        if (data == null)
        {
            Debug.LogError("ItemDataSO is Null");
            return null;
        }

        int clampedAmount = Mathf.Max(1, amount);
        IItem item = InstantiateOrFallback(
            () => new BasicItemRuntime(data),
            data);

        if (data.Modifiers != null)
        {
            foreach (var mod in data.Modifiers)
            {
                item = ApplyModifier(item, mod);
            }
        }

        if (data.Stackable || data.IsCurrency)
        {
            return InstantiateOrFallback(
                () => new StackableItemRuntime(item, data, clampedAmount),
                item,
                data,
                clampedAmount);
        }

        return item;
    }

    private static IItem ApplyModifier(IItem item, ModifierDataSO mod)
    {
        return mod.ModifierType switch
        {
            ModifierType.FireDamage =>
                InstantiateOrFallback(
                    () => new FireDamageDecorator(item, mod.Value),
                    item,
                    mod.Value),
            ModifierType.LavaImmunity =>
                InstantiateOrFallback(
                    () => new LavaImmunityDecorator(item),
                    item),
            _ => item
        };
    }

    private static T InstantiateOrFallback<T>(System.Func<T> fallback, params object[] extraArgs)
    {
        if (instantiator != null)
            return instantiator.Instantiate<T>(extraArgs);

        return fallback();
    }


}
