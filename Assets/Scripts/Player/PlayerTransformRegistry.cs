using UnityEngine;

public sealed class PlayerTransformRegistry : IPlayerTransformProvider, IPlayerTransformRegistry
{
    public Transform Current { get; private set; }

    public void Register(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        Current = playerTransform;
    }

    public void Unregister(Transform playerTransform)
    {
        if (Current == playerTransform)
            Current = null;
    }
}
