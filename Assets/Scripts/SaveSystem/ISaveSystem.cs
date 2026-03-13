namespace SaveSystem
{
    public interface ISaveSystem
    {
        void Save<TState>(string slot, TState state) where TState : struct;
        bool TryLoad<TState>(string slot, out TState state) where TState : struct;
        bool Exists(string slot);
        bool Delete(string slot);
    }
}
