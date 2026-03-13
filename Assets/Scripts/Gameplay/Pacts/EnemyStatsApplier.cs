using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyStatsApplier : MonoBehaviour
{
    [SerializeField] private CharacterStatResolver statResolver;
    [SerializeField] private bool useEnemyProvider = true;

    private bool applied;

    private void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        EnsureResolver();
        if (statResolver == null)
            return;

        if (useEnemyProvider)
            statResolver.SetPactProvider(CharacterStatResolver.PactProviderMode.Enemy);

        if (applied)
            return;

        applied = true;

    }

    public void ApplyTags(IReadOnlyList<GameplayTag> runtimeTags)
    {
        EnsureResolver();
        if (statResolver == null)
            return;

        if (useEnemyProvider)
            statResolver.SetPactProvider(CharacterStatResolver.PactProviderMode.Enemy);

        if (runtimeTags != null && runtimeTags.Count > 0)
            statResolver.AddTags(runtimeTags);
    }

    private void EnsureResolver()
    {
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();
    }
}
