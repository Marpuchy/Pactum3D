using System.Numerics;

public interface IRoomGenerator
{
    Room Generate(RoomTemplate template);
}