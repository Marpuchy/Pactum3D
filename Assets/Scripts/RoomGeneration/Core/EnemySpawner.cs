using UnityEngine;
using System.Collections.Generic;

public sealed class EnemySpawner
{
    public void Spawn(Room room)
    {
        foreach (var enemy in room.EnemySpawns)
        {
            Vector3 worldPos =
                new(enemy.Position.x + 0.5f,
                    enemy.Position.y + 0.5f,
                    0);

            GameObject instance = Object.Instantiate(
                enemy.Prefab,
                worldPos,
                Quaternion.identity);

            IReadOnlyList<GameplayTag> effectiveTags = ResolveEnemyEffectiveTags(enemy);
            EnemyMovementAgentTypeMapper.Apply(instance, effectiveTags);

            if (instance.TryGetComponent(out EnemyStatsApplier applier))
            {
                applier.ApplyTags(effectiveTags);
                applier.Apply();
            }
            else if (instance.TryGetComponent(out CharacterStatResolver statResolver))
            {
                statResolver.SetPactProvider(CharacterStatResolver.PactProviderMode.Enemy);
                statResolver.AddTags(effectiveTags);
            }
        }
    }

    private static IReadOnlyList<GameplayTag> ResolveEnemyEffectiveTags(EnemySpawnData enemy)
    {
        IReadOnlyList<GameplayTag> baseTags = enemy.Tags;
        PactManager manager = PactManager.Instance;
        if (manager == null || manager.EnemyStats == null)
            return baseTags;

        return manager.EnemyStats.GetEffectiveTags(baseTags);
    }
}
