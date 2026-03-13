using UnityEngine;

[DisallowMultipleComponent]
public sealed class AttackStrategyOverride : MonoBehaviour
{
    [SerializeField] private AttackStrategySO strategy;

    public AttackStrategySO Strategy => strategy;
}
