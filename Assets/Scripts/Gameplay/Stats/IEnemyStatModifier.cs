public interface IEnemyStatModifier
{
    int Priority { get; }
    void Apply(EnemyStatQuery query);
}
