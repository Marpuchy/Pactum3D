using UnityEngine;

public sealed class RuntimeCanvasSpawner : MonoBehaviour
{
    [SerializeField] private Transform parent;
    [SerializeField] private GameObject[] canvasPrefabs;

    private void Awake()
    {
        if (canvasPrefabs == null || canvasPrefabs.Length == 0)
            return;

        for (int i = 0; i < canvasPrefabs.Length; i++)
        {
            var prefab = canvasPrefabs[i];
            if (prefab == null) continue;

            GameObject instance = parent != null
                ? Instantiate(prefab, parent)
                : Instantiate(prefab);

            if (!instance.activeSelf)
                instance.SetActive(true);
        }
    }
}
