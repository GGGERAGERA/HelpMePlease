#if UNITY_EDITOR
using System;
using System.Linq;
using Subject42.Visuals;
using UnityEditor;
using UnityEngine;

public static class LiveVisualStudioAuthoring
{
    [MenuItem("Tools/Subject42/Visual Studio/Prepare production catalog %&v")]
    public static void Prepare()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Подготовьте каталог вне Play Mode.");
        AssetDatabase.Refresh();
        var preset = AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(VisualTuningPresetStorage.AssetPath);
        var baseline = AssetDatabase.LoadAssetAtPath<TextAsset>(LiveVisualBaselineAuthoring.OutputPath);
        if (preset == null || baseline == null) throw new InvalidOperationException("Нет production preset или Git baseline.");
        var original = JsonUtility.FromJson<VisualProfile>(baseline.text);
        original.Validate();
        Undo.RecordObject(preset, "Prepare production visual catalog");
        preset.LiveOriginalBaseline = baseline;
        preset.LiveSpriteShader = Shader.Find("Subject42/Environment Tile Lit");
        string[] sourceFolders = { "Enemies", "fx", "Orbital", "Environment/Props", "Weapons", "miniWeapons", "projectiles", "Characters", "Bootstrap" };
        preset.LiveSources = AssetDatabase.FindAssets("t:Prefab", sourceFolders.Select(p => "Assets/_Project/prefabs/" + p).Where(AssetDatabase.IsValidFolder).ToArray())
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => new LiveVisualSource { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path), AssetPath = path }).ToArray();
        preset.LiveHistoricalMaterials = original.MaterialSources.Select(m => m.AssetPath).Where(p => !string.IsNullOrEmpty(p)).Distinct()
            .OrderBy(p => p, StringComparer.Ordinal).Select(path => new LiveVisualMaterial { AssetPath = path,
                Material = AssetDatabase.LoadAssetAtPath<Material>(path) }).ToArray();
        EditorUtility.SetDirty(preset); AssetDatabase.SaveAssetIfDirty(preset);
        Debug.Log($"[LiveVisualRuntime] Catalog prepared: {preset.LiveSources.Length} prefab sources; " +
            $"{original.Values.Count} historical values; Git {original.BaselineRevision}. No production overrides selected.");
    }
}
#endif
