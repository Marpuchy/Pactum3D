using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class RoomNavMeshController : MonoBehaviour
{
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private List<NavMeshSurface> navMeshSurfaces = new List<NavMeshSurface>();
    private bool rebuildScheduled;

    private void OnEnable()
    {
        WorldChangedEvent.OnWorldChanged += OnWorldChanged;
    }

    private void OnDisable()
    {
        WorldChangedEvent.OnWorldChanged -= OnWorldChanged;
    }

    private void OnWorldChanged()
    {
        if (rebuildScheduled)
            return;

        StartCoroutine(RebuildNextFrame());
    }

    private IEnumerator RebuildNextFrame()
    {
        rebuildScheduled = true;
        yield return null;

        Physics.SyncTransforms();
        Physics2D.SyncTransforms();

        foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
        {
            if (surface == null)
                continue;

            if (surface.navMeshData == null)
                surface.BuildNavMesh();
            else
                surface.UpdateNavMesh(surface.navMeshData);
        }

        rebuildScheduled = false;
    }

    private IEnumerable<NavMeshSurface> ResolveNavMeshSurfaces()
    {
        HashSet<NavMeshSurface> unique = new HashSet<NavMeshSurface>();

        if (navMeshSurface != null)
            unique.Add(navMeshSurface);

        if (navMeshSurfaces != null)
        {
            for (int i = 0; i < navMeshSurfaces.Count; i++)
            {
                NavMeshSurface surface = navMeshSurfaces[i];
                if (surface != null)
                    unique.Add(surface);
            }
        }

        NavMeshSurface[] discovered = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        for (int i = 0; i < discovered.Length; i++)
        {
            if (discovered[i] != null)
                unique.Add(discovered[i]);
        }

        return unique;
    }
}
