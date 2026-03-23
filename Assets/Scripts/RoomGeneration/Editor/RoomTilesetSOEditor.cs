using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomTilesetSO))]
public sealed class RoomTilesetSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUI.changed)
            EditorUtility.SetDirty((RoomTilesetSO)target);
    }
}
