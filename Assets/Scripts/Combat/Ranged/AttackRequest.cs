using UnityEngine;

public readonly struct AttackRequest
{
    public readonly GameObject Attacker;
    public readonly Vector2 Origin;
    public readonly Vector2 Direction;

    public AttackRequest(GameObject attacker, Vector2 origin, Vector2 direction)
    {
        Attacker = attacker;
        Origin = origin;
        Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    }
}
