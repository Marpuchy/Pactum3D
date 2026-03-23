using UnityEngine;

public class BaseItem : IItem, IWeapon, IArmor, IConsumable, IItemDataProvider
{
    protected readonly ItemDataSO data;
    
    public BaseItem(ItemDataSO itemData)
    {
        data = itemData;
    }

    public string Name => data.DisplayName;
    public Sprite Icon => data.Icon;
    public string Description => data.Description;
    public ItemRaritySO Rarity => data.Rarity;
    public int SellValue => data.SellValue;
    public AudioClip UseSound { get; }
    public float VolumeUse { get; }
    public ItemDataSO Data => data;

    public float Damage =>
        data.WeaponStats != null ? data.WeaponStats.Damage : 0f;

    public float AttackSpeed =>
        data.WeaponStats != null ? data.WeaponStats.AttackSpeed : 0f;

    public float Defense =>
        data.ArmorStats != null ? data.ArmorStats.Defense : 0f;

    public float HealthBonus =>
        data.ArmorStats != null ? data.ArmorStats.HealthBonus : 0f;

    public int Charges { get; }
    public float Cooldown { get; }

    public float HealAmount =>
        data.ConsumableStats != null ? data.ConsumableStats.healAmount : 0f;

    public float RegenAmountPerTick =>
        data.ConsumableStats != null ? data.ConsumableStats.regenAmountPerTick : 0f;

    public float RegenTickInterval =>
        data.ConsumableStats != null ? data.ConsumableStats.regenTickInterval : 0f;

    public float RegenDuration =>
        data.ConsumableStats != null ? data.ConsumableStats.regenDuration : 0f;

    public virtual void Use()
    {
        Debug.Log($"Using item: {Name}");
        if (data.UseSound != null)
        {
            AudioSource.PlayClipAtPoint(data.UseSound, Vector3.zero);
        }
    }
}
