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
}
