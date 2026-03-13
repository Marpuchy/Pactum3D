using System.Collections.Generic;

public interface IRunState
{
    string LockedNpcId { get; }
    string LockedLineId { get; }
    IReadOnlyCollection<string> ActivePactIds { get; }

    void RecordPact(PactDefinition pact);
    void RemovePact(PactDefinition pact);
    bool HasPact(string pactId);
    int GetLineTier(string lineId);
    void SetLineTier(string lineId, int tier);
    void LockTo(string npcId, string lineId);
    bool IsNpcEligible(string npcId, string lineId);
    void ResetRun();
}
