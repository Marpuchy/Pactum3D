namespace SaveSystem
{
    public interface IGameSaveService
    {
        void SaveCurrentState(string slot, string sceneName);
        bool TryLoadState(string slot, out GameSaveData data);
        bool TryApplyPendingState();
        bool Exists(string slot);
        bool Delete(string slot);
    }
}
