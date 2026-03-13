using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerAttackSelection",
    menuName = "Game/Player Attack Selection"
)]
public class PlayerAttackSelection : ScriptableObject
{
    public AttackType SelectedAttack { get; private set; }

    public void SelectMelee()
    {
        SelectedAttack = AttackType.Melee;
    }

    public void SelectRanged()
    {
        SelectedAttack = AttackType.Ranged;
    }
}