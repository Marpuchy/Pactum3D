using UnityEngine;

public interface IConsumable : IItem
{
    int Charges { get; }
    float Cooldown { get; }
    
    //Posibles efectos.
    float HealAmount { get; }
    float RegenAmountPerTick { get; }
    float RegenTickInterval { get; }
    float RegenDuration { get; }
}
