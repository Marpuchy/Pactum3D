using SaveSystem;

namespace Injections
{
    public static class RuntimeServiceFallback
    {
        private static IPlayerDataService playerDataService;
        private static IGameSaveService gameSaveService;

        public static IPlayerDataService PlayerDataService
        {
            get
            {
                if (playerDataService == null)
                    playerDataService = new PlayerDataService();

                return playerDataService;
            }
        }

        public static IGameSaveService GameSaveService
        {
            get
            {
                if (gameSaveService == null)
                    gameSaveService = new GameSaveService(new global::SaveSystem.SaveSystem(), PlayerDataService);

                return gameSaveService;
            }
        }
    }
}
