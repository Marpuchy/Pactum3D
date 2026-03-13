using System;

public static class WorldChangedEvent
{
    public static event Action OnWorldChanged;

    public static void Raise()
    {
        OnWorldChanged?.Invoke();
    }
}