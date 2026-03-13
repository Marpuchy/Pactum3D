using UnityEngine;

public abstract class RoomPactEffect : PactModifierAsset, IRoomParamModifier
{
    [SerializeField] private int priority;

    public int Priority => priority;

    public abstract void Apply(RoomParamQuery query);
}
