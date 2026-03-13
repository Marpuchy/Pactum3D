using UnityEngine;

public class FireDamageDecorator : ItemDecorator, IFireDamage, IWeapon
{
    private readonly float damage;

    public FireDamageDecorator(IItem item, float damage) : base(item)
    {
        this.damage = damage;
    }

    //Ejemplo para proximos decorators
    public float Damage
    {
        get
        {
            if (wrappedItem is IWeapon w)
            {
                return w.Damage;
            }

            return 0f;
        }
    }

    public float AttackSpeed { get; }
    public float FireDamage => damage;
    public override string Name => $"{wrappedItem.Name} (Fire)";

    public override void Use()
    {
        base.Use();
        Debug.Log($"Applying fire damage: {damage}");
    }


}
