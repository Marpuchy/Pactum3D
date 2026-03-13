using UnityEngine;

public sealed class LavaDamageTile : DamageOverTimeTile
{
    protected override bool ShouldApplyDamage(GameObject target)
    {
        var provider = target.GetComponentInParent<IItemCapabilityProvider>();
        if (provider == null)
        {
            return true; // aplica daño si no hay provider
        }

        return !provider.HasLavaImmunity();
    }


}

