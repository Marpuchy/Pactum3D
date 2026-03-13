using UnityEngine;

public class HealthChangedEventListener : MonoBehaviour
{
    [SerializeField] private HealthChangedEventSO healthChangedEvent;
    [SerializeField] private ProHealthBar proHealthBar;

    private void OnEnable()
    {
        if (healthChangedEvent == null)
            return;

        healthChangedEvent.RegisterListener(OnHealthChanged);
    }

    private void OnDisable()
    {
        if (healthChangedEvent == null)
            return;

        healthChangedEvent.UnregisterListener(OnHealthChanged);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (proHealthBar == null)
            return;

        proHealthBar.OnHealthChanged(current, max);
    }
}
