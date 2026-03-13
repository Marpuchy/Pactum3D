using UnityEngine;

public static class SpecialTilesContainer
{
    public const string Name = "SpecialTiles";

    public static void ClearChildren()
    {
        GameObject container = GameObject.Find(Name);
        if (container == null)
            return;

        foreach (Transform child in container.transform)
        {
            Object.Destroy(child.gameObject);
        }
    }

}
