namespace SaveSystem
{
    public static class PendingGameSaveState
    {
        private static bool hasPendingState;
        private static GameSaveData pendingState;

        public static void Set(GameSaveData data)
        {
            pendingState = data;
            hasPendingState = true;
        }

        public static bool TryGet(out GameSaveData data)
        {
            data = pendingState;
            return hasPendingState;
        }

        public static void Clear()
        {
            pendingState = default;
            hasPendingState = false;
        }
    }
}
