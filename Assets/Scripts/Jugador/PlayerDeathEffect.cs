using UnityEngine;
using System.Collections;

public class PlayerDeathEffect : MonoBehaviour
{
    [Header("Settings")]
    public float animationDuration = 0.5f;
    public Color deadColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark/Red Color

    [Header("Dependencies")]
    public GameEventSO deathEvent;        // Drag 'Evento_Muerte' here
    public SpriteRenderer playerSprite;   // Drag SpriteRenderer here
    public MonoBehaviour movementScript;  // Drag Movement Script here
    public Rigidbody2D rb;                // Drag Rigidbody2D here

    private void OnEnable()
    {
        if (deathEvent != null)
            deathEvent.RegisterListener(OnPlayerDied);
    }

    private void OnDisable()
    {
        if (deathEvent != null)
            deathEvent.UnregisterListener(OnPlayerDied);
    }

    private void OnPlayerDied()
    {
        // Disable controls and physics
        if (movementScript != null) movementScript.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Start animation
        StartCoroutine(PerformDeathAnimation());
    }

    private IEnumerator PerformDeathAnimation()
    {
        float timer = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees
        Color startColor = playerSprite.color;

        while (timer < animationDuration)
        {
            // Use unscaledDeltaTime because Game Over freezes time
            timer += Time.unscaledDeltaTime;
            float progress = timer / animationDuration;

            transform.rotation = Quaternion.Lerp(startRotation, endRotation, progress);
            playerSprite.color = Color.Lerp(startColor, deadColor, progress);

            yield return null;
        }

        transform.rotation = endRotation;
        playerSprite.color = deadColor;
    }
}