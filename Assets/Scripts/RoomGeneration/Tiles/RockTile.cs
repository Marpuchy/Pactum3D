using System.Collections.Generic;
using UnityEngine;

public class RockTile : BreakableBase
{
    [Header("Sprites (+ > -)")]
    [SerializeField] private List<Sprite> rockSprites;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [SerializeField] private AudioClip rockBreakingAudio;
    [SerializeField] private RockHitEventSO rockHitEvent;

    protected override void OnInitialized()
    {
        StabilizePhysics();
        UpdateSprite();
    }

    protected override void OnHealthChanged()
    {
        UpdateSprite();
    }

    protected override void OnBreak()
    {
        DisableColliders();
        WorldChangedEvent.Raise();
        base.OnBreak();
    }

    private void UpdateSprite()
    {
        if (spriteRenderer == null || rockSprites.Count == 0) return;

        int index = Mathf.Clamp(
            rockSprites.Count - Mathf.CeilToInt(HealthPercentage * rockSprites.Count),
            0,
            rockSprites.Count - 1
        );

        if(index != 0) rockHitEvent?.Raise(rockBreakingAudio);
        spriteRenderer.sprite = rockSprites[index];
    }

    private void DisableColliders()
    {
        Collider2D[] colliders2D = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders2D.Length; i++)
            colliders2D[i].enabled = false;

        Collider[] colliders3D = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders3D.Length; i++)
            colliders3D[i].enabled = false;
    }

    private void StabilizePhysics()
    {
        Rigidbody rigidbody3D = GetComponent<Rigidbody>();
        if (rigidbody3D != null)
        {
            rigidbody3D.useGravity = false;
            rigidbody3D.isKinematic = true;
            rigidbody3D.constraints = RigidbodyConstraints.FreezeAll;
            rigidbody3D.linearVelocity = Vector3.zero;
            rigidbody3D.angularVelocity = Vector3.zero;
        }

        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.bodyType = RigidbodyType2D.Static;
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }
    }
}
