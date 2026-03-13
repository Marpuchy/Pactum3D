using System;

public sealed class TelemetryEvent
{
    public string EventName { get; }
    public string PlayerId { get; }
    public string SessionId { get; }
    public DateTime TimestampUtc { get; }
    public string PayloadJson { get; }

    public TelemetryEvent(
        string eventName,
        string playerId,
        string sessionId,
        DateTime timestampUtc,
        string payloadJson)
    {
        EventName = eventName;
        PlayerId = playerId;
        SessionId = sessionId;
        TimestampUtc = timestampUtc;
        PayloadJson = payloadJson;
    }
}
