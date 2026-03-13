using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttackComponent))]
[RequireComponent(typeof(EnemyAnimatorDriver))]
public class EnemyCombat : MonoBehaviour
{
    private AttackComponent attack;
    private EnemyAnimatorDriver animatorDriver;
    public bool IsAttacking => attack.IsAttackLocked;
    
    private void Awake()
    {
        attack = GetComponent<AttackComponent>();
        animatorDriver = GetComponent<EnemyAnimatorDriver>();
    }

    public void TryAttack()
    {
        // 🔒 si aún está atacando, NO relanzar
        if (animatorDriver.IsInAttackState())
            return;
        if (!attack.CanAttack)
            return;
        attack.ConsumeAttack();
        animatorDriver.PlayAttack(attack.AttackAnimationDuration);
    }
}   
