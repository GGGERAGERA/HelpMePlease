using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[CreateAssetMenu(menuName = "Subject42/Debug/Combat Feel Tuning Preset")]
public sealed class CombatFeelTuningPreset : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public CombatFeelParameter Parameter;
        public float Value;
    }

    [SerializeField] private string savedAtUtc;
    [SerializeField] private List<Entry> values = new();

    public string SavedAtUtc => savedAtUtc;
    public IReadOnlyList<Entry> Values => values;

    public void Capture(CombatFeelLabSettings lab)
    {
        values.Clear();
        IReadOnlyDictionary<CombatFeelParameter, float> snapshot =
            lab.ExportValues();
        IReadOnlyList<CombatFeelDescriptor> descriptors =
            CombatFeelLabSettings.Descriptors;
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor descriptor = descriptors[i];
            values.Add(new Entry
            {
                Parameter = descriptor.Parameter,
                Value = snapshot.TryGetValue(descriptor.Parameter, out float value)
                    ? value : descriptor.Neutral
            });
        }
        savedAtUtc = DateTime.UtcNow.ToString("O");
    }
}

public static class CombatFeelTuningPresetStorage
{
    public const string AssetPath =
        "Assets/_Project/CombatFeelSavedTuning.asset";

    public static bool Save(CombatFeelLabSettings lab, out string message)
    {
#if UNITY_EDITOR
        try
        {
            CombatFeelTuningPreset preset =
                UnityEditor.AssetDatabase.LoadAssetAtPath<CombatFeelTuningPreset>(
                    AssetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<CombatFeelTuningPreset>();
                preset.name = "CombatFeelSavedTuning";
                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    preset, "Save Combat Feel tuning preset");
                UnityEditor.AssetDatabase.CreateAsset(preset, AssetPath);
            }
            else
            {
                UnityEditor.Undo.RecordObject(
                    preset, "Save Combat Feel tuning preset");
            }

            preset.Capture(lab);
            UnityEditor.EditorUtility.SetDirty(preset);
            UnityEditor.AssetDatabase.SaveAssets();
            lab.MarkSaved();
            message = "Значения сохранены в CombatFeelSavedTuning.asset";
            return true;
        }
        catch (Exception exception)
        {
            message = "Не удалось сохранить preset: " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
#else
        message = "Сохранение asset доступно только в Unity Editor.";
        return false;
#endif
    }
}
#endif
