using System;
using UnityEngine;

namespace SaveSystem
{
    public static class LoadedNpcRoomOfferState
    {
        private static int runSeed;
        private static int roomNumber;
        private static int roomSeed;
        private static string npcId = string.Empty;
        private static string[] pactOfferIds = Array.Empty<string>();

        public static void SetFromSave(GameSaveData data)
        {
            runSeed = data.State.RunSeed;
            roomNumber = Mathf.Max(1, data.State.RoomNumber);
            roomSeed = data.State.NpcRoomSeed;
            npcId = PactIdentity.Normalize(data.State.NpcRoomNpcId);
            pactOfferIds = CloneIds(data.State.NpcRoomOfferPactIds);
        }

        public static bool TryGetExpectedNpcForCurrentRoom(out string expectedNpcId)
        {
            expectedNpcId = string.Empty;
            if (!MatchesCurrentRoom() || string.IsNullOrEmpty(npcId))
                return false;

            expectedNpcId = npcId;
            return true;
        }

        public static bool TryGetOfferPactIdsForCurrentRoom(string requestNpcId, out string[] offerIds)
        {
            offerIds = Array.Empty<string>();
            if (!MatchesCurrentRoom())
                return false;

            if (string.IsNullOrEmpty(npcId) || !PactIdentity.AreEqual(npcId, requestNpcId))
                return false;

            if (pactOfferIds == null || pactOfferIds.Length == 0)
                return false;

            offerIds = CloneIds(pactOfferIds);
            return offerIds.Length > 0;
        }

        public static void Clear()
        {
            runSeed = 0;
            roomNumber = 0;
            roomSeed = 0;
            npcId = string.Empty;
            pactOfferIds = Array.Empty<string>();
        }

        private static bool MatchesCurrentRoom()
        {
            if (RoomBuilder.Current == null)
                return false;

            if (runSeed == 0 || roomNumber <= 0 || roomSeed == 0)
                return false;

            if (RoomBuilder.Current.CurrentRunSeed != runSeed)
                return false;

            if (RoomBuilder.Current.CurrentRoomNumber != roomNumber)
                return false;

            return RoomBuilder.Current.CurrentRoomSeed == roomSeed;
        }

        private static string[] CloneIds(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();

            string[] clone = new string[source.Length];
            int writeIndex = 0;
            for (int i = 0; i < source.Length; i++)
            {
                string normalized = PactIdentity.Normalize(source[i]);
                if (string.IsNullOrEmpty(normalized))
                    continue;

                clone[writeIndex++] = normalized;
            }

            if (writeIndex == 0)
                return Array.Empty<string>();

            if (writeIndex == clone.Length)
                return clone;

            string[] compact = new string[writeIndex];
            Array.Copy(clone, compact, writeIndex);
            return compact;
        }
    }
}
