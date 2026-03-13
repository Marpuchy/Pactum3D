using UnityEngine;

[CreateAssetMenu(fileName = "SO_RoomSpawnEvent", menuName = "Events/RoomSpawnEvent")]
public class RoomSpawnEvent : ScriptableObject
{
    public System.Action<Vector3> OnRoomSpawn;

    public void Raise(Vector3 spawnPosition)
    {
        OnRoomSpawn?.Invoke(spawnPosition);
    }
}