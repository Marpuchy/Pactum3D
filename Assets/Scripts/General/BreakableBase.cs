using System;
using UnityEngine;

public class BreakableBase: MonoBehaviour, IBreakable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    protected int CurrentHealth { get; private set; }

    protected virtual void Awake()
    {
        CurrentHealth = maxHealth;
        OnInitialized();
    }

    public void ApplyDamage(int damage = 1)
    {
        if (CurrentHealth <= 0) return;

        CurrentHealth = Math.Clamp(CurrentHealth - damage, 0, maxHealth);

        OnHealthChanged();

        if (CurrentHealth == 0)
        {
            OnBreak();
        }
    }

    protected float HealthPercentage =>
        maxHealth == 0 ? 0f : (float)CurrentHealth / maxHealth;

    protected virtual void OnInitialized() { }

    protected virtual void OnHealthChanged() { }

    protected virtual void OnBreak()
    {
        Destroy(gameObject);
    }
}