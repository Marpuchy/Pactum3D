using UnityEngine;

public interface IPlayerTransformRegistry
{
    void Register(Transform playerTransform);
    void Unregister(Transform playerTransform);
}
