using UnityEngine;
using UnityEngine.AI;

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
        PlayerController playerController = null;
        if (TryGetComponent(out playerController))
            playerController.ResetMotionForTeleport();

        if (RoomWorldSpaceSettings.Current != null)
            spawnPosition = RoomWorldSpaceSettings.Current.ClampToWalkPlane(spawnPosition);

        if (playerController != null)
            spawnPosition = playerController.GetClampedTeleportPosition(spawnPosition);

        if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
        {
            if (!TryWarpToSpawn(agent, spawnPosition))
                transform.position = spawnPosition;
        }
        else
        {
            transform.position = spawnPosition;
        }

        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.position = new Vector2(spawnPosition.x, spawnPosition.y);
            rb.linearVelocity = Vector2.zero;
        }

        if (TryGetComponent(out Rigidbody rb3D))
        {
            rb3D.position = spawnPosition;
            rb3D.linearVelocity = Vector3.zero;
            rb3D.angularVelocity = Vector3.zero;
        }

        transform.position = spawnPosition;
    }

    private static bool TryWarpToSpawn(NavMeshAgent agent, Vector3 spawnPosition)
    {
        if (agent == null || !agent.enabled)
            return false;

        Vector3 destination = spawnPosition;
        if (!agent.isOnNavMesh)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            if (!NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 32f, filter))
                return false;

            destination = hit.position;
        }

        if (!agent.Warp(destination))
        {
            agent.enabled = false;
            agent.transform.position = destination;
            agent.enabled = true;

            if (!agent.isOnNavMesh && !agent.Warp(destination))
                return false;
        }

        agent.nextPosition = destination;
        agent.ResetPath();
        return true;
    }
}
