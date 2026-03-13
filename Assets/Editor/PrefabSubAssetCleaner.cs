using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SafePrefabSubAssetCleaner
{
    [MenuItem("Tools/Cleaning/Delete ONLY Non-Component Sub-Assets Of Selected Prefab (Safe)")]
    private static void DeleteOnlyNonComponentSubAssets()
    {
        var selected = Selection.activeObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Safe Prefab Sub-Assets", "Selecciona un prefab en el Project.", "OK");
            return;
        }

        var path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Safe Prefab Sub-Assets", "El objeto seleccionado no es un .prefab.", "OK");
            return;
        }

        var main = AssetDatabase.LoadMainAssetAtPath(path);
        var all = AssetDatabase.LoadAllAssetsAtPath(path);

        // IMPORTANT:
        // En un prefab, GameObjects y Components son parte del contenido normal del prefab.
        // Si los borras, destruyes el prefab. Por eso filtramos TODO eso fuera.
        bool IsPrefabContentObject(UnityEngine.Object o)
            => o is GameObject || o is Component;

        var candidates = all
            .Where(o => o != null && o != main)
            .Where(o => !IsPrefabContentObject(o))
            .ToArray();

        if (candidates.Length == 0)
        {
            EditorUtility.DisplayDialog("Safe Prefab Sub-Assets", "No hay sub-assets 'reales' (no-component) para borrar. Perfecto.", "OK");
            return;
        }

        var preview = string.Join("\n", candidates.Select(a => $"{a.name} ({a.GetType().Name})"));
        if (!EditorUtility.DisplayDialog(
                "Safe Prefab Sub-Assets",
                $"Se van a borrar {candidates.Length} sub-assets NO componentes:\n\n{preview}\n\n¿Continuar?",
                "Borrar",
                "Cancelar"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Safe Delete Prefab Sub-Assets");
        var group = Undo.GetCurrentGroup();

        foreach (var sub in candidates)
        {
            Undo.DestroyObjectImmediate(sub);
        }

        Undo.CollapseUndoOperations(group);

        AssetDatabase.ImportAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Safe Prefab Sub-Assets", "Limpieza completada.", "OK");
    }
}
