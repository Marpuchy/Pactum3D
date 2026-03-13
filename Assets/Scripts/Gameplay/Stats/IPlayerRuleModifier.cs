public interface IPlayerRuleModifier
{
    int Priority { get; }
    void Apply(PlayerRuleQuery query);
}
