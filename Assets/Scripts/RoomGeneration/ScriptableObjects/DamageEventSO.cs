using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Events/Damage Event")]
public class DamageEventSO : ScriptableObject
{
    public UnityAction<GameObject, int> OnEventRaised;

    public void Raise(GameObject target, int amount)
    {
        OnEventRaised?.Invoke(target, amount);
    }
}