public sealed class PlayerDataService : IPlayerDataService
{
    private static AttackType selectedAttack = AttackType.Melee;

    public AttackType SelectedAttack => selectedAttack;

    public void SetSelectedAttack(AttackType attackType)
    {
        selectedAttack = attackType;
    }
}
