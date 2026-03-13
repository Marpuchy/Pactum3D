using UnityEngine;
using UnityEngine.UI;

public class ProHealthBar : MonoBehaviour
{
    [Header("Visual References")]
    [SerializeField] private Image fastBarImage;
    [SerializeField] private Image slowBarImage;

    [Header("Events")]
    [SerializeField] private HealthChangedEventSO healthChangedEvent;

    [Header("Visual")]
    [SerializeField] private Gradient healthGradient;
    [SerializeField] private float trailSpeed = 2f;
    [SerializeField] private float trailDelay = 0.5f;

    private float targetHealth = 1f;
    private float trailTimer;
    private void Update()
    {
        fastBarImage.fillAmount = targetHealth;

        if (slowBarImage.fillAmount > fastBarImage.fillAmount)
        {
            trailTimer += Time.deltaTime;
            if (trailTimer >= trailDelay)
            {
                slowBarImage.fillAmount = Mathf.Lerp(
                    slowBarImage.fillAmount,
                    fastBarImage.fillAmount,
                    Time.deltaTime * trailSpeed
                );
            }
        }
        else
        {
            slowBarImage.fillAmount = fastBarImage.fillAmount;
            trailTimer = 0f;
        }

        fastBarImage.color = healthGradient.Evaluate(fastBarImage.fillAmount);
    }

    public void OnHealthChanged(float current, float max)
    {
        float percentage = current / max;
        targetHealth = percentage;
        trailTimer = 0f;
    }



}