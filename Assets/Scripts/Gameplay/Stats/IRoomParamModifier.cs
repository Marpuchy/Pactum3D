public interface IRoomParamModifier
{
    int Priority { get; }
    void Apply(RoomParamQuery query);
}
