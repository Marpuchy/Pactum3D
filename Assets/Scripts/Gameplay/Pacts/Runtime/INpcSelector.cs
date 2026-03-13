using System.Collections.Generic;

public interface INpcSelector
{
    bool IsNpcEligible(string npcId, string lineId);
    bool IsEntryEligible(NpcSpawnEntry entry);
    NpcSpawnEntry SelectNpc(IReadOnlyList<NpcSpawnEntry> candidates);
}
