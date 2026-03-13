using UnityEngine;

public sealed class AttackSystem : MonoBehaviour
{
    [SerializeField] private AttackEventChannelSO attackEvent;
    [SerializeField] private AttackStrategySO strategy;
    [SerializeField] private AttackStrategySO playerStrategy;
    [SerializeField] private AttackStrategySO enemyStrategy;
    [SerializeField] private ProjectileFactory projectileFactory;

    private void Awake()
    {
        if (projectileFactory == null)
            projectileFactory = GetComponent<ProjectileFactory>();
    }

    private void OnEnable()
    {
        if (attackEvent != null)
            attackEvent.OnRaised += OnAttackRaised;
    }

    private void OnDisable()
    {
        if (attackEvent != null)
            attackEvent.OnRaised -= OnAttackRaised;
    }

    private void OnAttackRaised(AttackRequest request)
    {
        AttackStrategySO activeStrategy = ResolveStrategy(request.Attacker);
        if (activeStrategy == null || projectileFactory == null)
            return;

        float damageOverride = ResolveDamage(request.Attacker);
        activeStrategy.Execute(request, new AttackExecutionContext(projectileFactory, damageOverride));
    }

    private float ResolveDamage(GameObject attacker)
    {
        if (attacker == null)
            return 0f;

        if (attacker.TryGetComponent(out AttackComponent attackComponent))
            return attackComponent.GetDamage();

        if (attacker.TryGetComponent(out CharacterStatResolver statResolver))
            return statResolver.Get(StatType.AttackDamage, 0f);

        return 0f;
    }

    private AttackStrategySO ResolveStrategy(GameObject attacker)
    {
        if (attacker != null)
        {
            if (attacker.TryGetComponent(out AttackStrategyOverride strategyOverride) &&
                strategyOverride.Strategy != null)
            {
                return strategyOverride.Strategy;
            }

            if (attacker.CompareTag("Player") && playerStrategy != null)
                return playerStrategy;

            if (attacker.CompareTag("Enemy") && enemyStrategy != null)
                return enemyStrategy;
        }

        return strategy;
    }
}
