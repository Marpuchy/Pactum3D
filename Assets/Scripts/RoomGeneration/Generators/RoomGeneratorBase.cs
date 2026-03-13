using UnityEngine;

public abstract class RoomGeneratorBase : IRoomGenerator
{
    public Room Generate(RoomTemplate config)
    {
        Room room = CreateLayout(config);
        PlaceDoors(room, config);
        PlaceSpawnPoint(room, config);
        PlaceSpawns(room, config);
        PlaceEnemies(room, config);
        PlaceChests(room, config);
        PlaceNpcs(room, config);

        return room;
    }
    
    protected virtual void PlaceSpawnPoint(Room room, RoomTemplate config) { }

    protected abstract Room CreateLayout(RoomTemplate config);

    protected virtual void PlaceDoors(Room room, RoomTemplate config) { }

    protected virtual void PlaceSpawns(Room room, RoomTemplate config) { }
    
    protected virtual void PlaceEnemies(Room room, RoomTemplate config) { }
    protected virtual void PlaceChests(Room room, RoomTemplate config) { }
    
    protected virtual void PlaceNpcs(Room room, RoomTemplate config) { }
    
    

}