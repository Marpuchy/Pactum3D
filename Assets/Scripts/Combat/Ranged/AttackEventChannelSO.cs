using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Attack Event Channel", fileName = "AttackEventChannel")]
public sealed class AttackEventChannelSO : ScriptableObject
{
    public event Action<AttackRequest> OnRaised;

    public void Raise(AttackRequest request)
    {
        OnRaised?.Invoke(request);
    }
}
