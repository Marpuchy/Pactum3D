using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(RoomTemplate))]
public sealed class RoomTemplateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var rt = (RoomTemplate)target;

        EditorGUILayout.Space();

        DrawNormalizedSection(
            "Special Tiles Percentages (Normalized)",
            rt.specialTiles,
            e => e.spawnPercentage,
            (e, v) => e.spawnPercentage = v,
            e => e.type.ToString()
        );

        DrawNormalizedSection(
            "Enemy Spawn Weights (Normalized)",
            rt.enemySpawns,
            e => e.spawnWeight,
            (e, v) => e.spawnWeight = v,
            e => e.DisplayName
        );

        DrawNormalizedSection(
            "Item Rarity Weights (Normalized)",
            rt.itemRarities,
            e => e.spawnWeight,
            (e, v) => e.spawnWeight = v,
            e => e.rarity != null ? e.rarity.DisplayName : "None"
        );

        DrawNormalizedSection(
            "Chest Rarity Weights (Normalized)",
            rt.chestRarities,
            e => e.spawnWeight,
            (e, v) => e.spawnWeight = v,
            e => e.rarity != null ? e.rarity.DisplayName : "None"
        );

        DrawNormalizedSection(
            "Pact NPC Spawn Rates (Normalized)",
            rt.pactNpcSpawns,
            e => e.spawnRate,
            (e, v) => e.spawnRate = v,
            e => e.DisplayName
        );

        DrawNormalizedSection(
            "Other NPC Spawn Rates (Normalized)",
            rt.otherNpcSpawns,
            e => e.spawnRate,
            (e, v) => e.spawnRate = v,
            e => e.DisplayName
        );

        if (GUI.changed)
            EditorUtility.SetDirty(rt);
    }

    #region Normalized Drawer

    private void DrawNormalizedSection<T>(
        string title,
        List<T> list,
        System.Func<T, float> getValue,
        System.Action<T, float> setValue,
        System.Func<T, string> getLabel)
    {
        if (list == null || list.Count == 0)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(getLabel(entry), GUILayout.Width(140));

            float current = getValue(entry);
            float newValue = EditorGUILayout.Slider(current, 0f, 1f);

            if (!Mathf.Approximately(newValue, current))
            {
                ApplyNormalizedChange(list, i, newValue, getValue, setValue);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void ApplyNormalizedChange<T>(
        List<T> list,
        int changedIndex,
        float newValue,
        System.Func<T, float> getValue,
        System.Action<T, float> setValue)
    {
        setValue(list[changedIndex], newValue);

        float othersSum = 0f;
        for (int i = 0; i < list.Count; i++)
            if (i != changedIndex)
                othersSum += getValue(list[i]);

        if (othersSum <= 0f)
            return;

        float scale = (1f - newValue) / othersSum;

        for (int i = 0; i < list.Count; i++)
        {
            if (i == changedIndex)
                continue;

            setValue(list[i], getValue(list[i]) * scale);
        }
    }

    #endregion
}
