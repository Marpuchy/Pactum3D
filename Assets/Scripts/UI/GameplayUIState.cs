using System.Collections.Generic;
using UnityEngine;

public static class GameplayUIState
{
    public const int GameplayCanvasSortOrder = 50;
    public const int PauseCanvasSortOrder = 100;
    public const int GameOverCanvasSortOrder = 100;

    private static readonly HashSet<UnityEngine.Object> openOwners = new();
    private static readonly List<UnityEngine.Object> cleanupBuffer = new();

    public static bool IsGameplayInputBlocked
    {
        get
        {
            CleanupDestroyedOwners();
            return openOwners.Count > 0;
        }
    }

    public static void Register(UnityEngine.Object owner)
    {
        if (owner == null)
            return;

        CleanupDestroyedOwners();
        openOwners.Add(owner);
    }

    public static void Unregister(UnityEngine.Object owner)
    {
        if (owner == null)
            return;

        CleanupDestroyedOwners();
        openOwners.Remove(owner);
    }

    public static void Reset()
    {
        openOwners.Clear();
        cleanupBuffer.Clear();
    }

    public static void ConfigureCanvas(Canvas canvas, int sortingOrder)
    {
        if (canvas == null)
            return;

        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    private static void CleanupDestroyedOwners()
    {
        if (openOwners.Count == 0)
            return;

        cleanupBuffer.Clear();
        foreach (var owner in openOwners)
        {
            if (owner == null)
                cleanupBuffer.Add(owner);
        }

        for (int i = 0; i < cleanupBuffer.Count; i++)
            openOwners.Remove(cleanupBuffer[i]);
    }
}
