public interface IPlayerDataService
{
    AttackType SelectedAttack { get; }
    void SetSelectedAttack(AttackType attackType);
}
