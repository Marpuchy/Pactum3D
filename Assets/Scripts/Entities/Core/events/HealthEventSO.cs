using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Events/Health Event")]
public class HealthEventSO : ScriptableObject
{
    private event Action<float, GameObject, GameObject> onHit;
    private event Action<GameObject> onDeath;

    // -------------------------
    // EMITIR EVENTOS
    // -------------------------
    public void RaiseHit(float damage, GameObject target, GameObject attacker)
    {
        onHit?.Invoke(damage, target, attacker);
    }

    public void RaiseDeath(GameObject dead)
    {
        onDeath?.Invoke(dead);
    }

    // -------------------------
    // LISTENERS
    // -------------------------
    public void RegisterHit(Action<float, GameObject, GameObject> listener)
    {
        onHit += listener;
    }

    public void UnregisterHit(Action<float, GameObject, GameObject> listener)
    {
        onHit -= listener;
    }

    public void RegisterDeath(Action<GameObject> listener)
    {
        onDeath += listener;
    }

    public void UnregisterDeath(Action<GameObject> listener)
    {
        onDeath -= listener;
    }
}