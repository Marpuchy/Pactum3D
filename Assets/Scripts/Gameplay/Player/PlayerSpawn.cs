using UnityEngine;

public class PlayerSpawn : MonoBehaviour
{
    [SerializeField] private RoomSpawnEvent roomSpawnEvent;

    private void OnEnable()
    {
        if (roomSpawnEvent != null)
            roomSpawnEvent.OnRoomSpawn += MoveToSpawn;
    }

    private void OnDisable()
    {
        if (roomSpawnEvent != null)
            roomSpawnEvent.OnRoomSpawn -= MoveToSpawn;
    }

    private void MoveToSpawn(Vector3 spawnPosition)
    {
        TeleportImmediate(spawnPosition);
    }

    private void TeleportImmediate(Vector3 spawnPosition)
    {
        if (TryGetComponent(out PlayerController playerController))
            playerController.ResetMotionForTeleport();

        transform.position = spawnPosition;

        if (TryGetComponent(out Rigidbody2D rb))
            rb.linearVelocity = Vector2.zero;
    }
}
