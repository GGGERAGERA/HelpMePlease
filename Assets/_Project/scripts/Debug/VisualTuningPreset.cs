using System;
using UnityEngine;

[Serializable]
public struct VisualAnomalyTuningSnapshot
{
    public string Target;
    public string Type;
    public bool ArtHooksVisible;
    public AnomalyVisualTuningValues Values;
}

[Serializable]
public struct VisualTuningSnapshot
{
    public int WorldReadability;
    public float DecorBrightness, EnvironmentDarken;
    public int EnemyReadability, EnemyScope;
    public float EnemyBrightness, EnemySaturation, EnemyTintStrength;
    public float EnemyHueShift, EnemyRecolorStrength;
    public Color EnemyRecolorTarget;
    public bool EnemyOutlineEnabled;
    public float EnemyOutlineStrength, EnemyOutlineWidth;

    public float PlayerScale, PlayerOffsetX, PlayerOffsetY;
    public float PlayerBrightness, PlayerSaturation, PlayerOpacity;
    public Color PlayerTint;
    public float PlayerTintStrength, PlayerGlowIntensity, PlayerGlowRadius;

    public float WeaponScale, WeaponOffsetX, WeaponOffsetY;
    public float WeaponBrightness, WeaponSaturation, WeaponOpacity;
    public Color WeaponTint;
    public float WeaponTintStrength;

    public bool RingEnabled;
    public float RingRadius, RingWidth, RingOpacity, RingBrightness;
    public float RingPulseAmount, RingPulseSpeed, RingRotationSpeed;
    public float RingOffsetX, RingOffsetY;
    public Color RingTint;

    public float ProjectileScale, TrailWidth, TrailLifetime;
    public float TrailOpacity, TrailBrightness;
    public float LaserCoreWidth, LaserGlowWidth, LaserBrightness;

    public bool AnomalyFocus;
    public float OutsideDarkness, OutsideColor, FocusTransition;
    public float WindDustAmount, AnomalyAccent;
    public bool MonochromeAnomalies;
    public bool AnomalyArtHooksVisible;
    public string AnomalyTarget;
    public AnomalyVisualTuningValues AnomalyValues;
    public VisualAnomalyTuningSnapshot[] Anomalies;

    public float VignetteIntensity, CameraOrthographicSize;
}

[CreateAssetMenu(menuName = "Subject42/Debug/Visual Tuning Preset")]
public sealed class VisualTuningPreset : ScriptableObject
{
    [SerializeField] private string savedAtUtc;
    [SerializeField] private VisualTuningSnapshot values;
    [SerializeField, TextArea(12, 40)] private string readableValues;

    public string SavedAtUtc => savedAtUtc;
    public VisualTuningSnapshot Values => values;
    public string ReadableValues => readableValues;
    public bool HasValues => !string.IsNullOrWhiteSpace(savedAtUtc);

    public void Capture(VisualTuningSnapshot snapshot, string text)
    {
        values = snapshot;
        readableValues = text;
        savedAtUtc = DateTime.UtcNow.ToString("O");
    }
}

public static class VisualTuningPresetStorage
{
    public const string AssetPath =
        "Assets/_Project/Resources/VisualTuningSavedValues.asset";
    public const string LegacyAssetPath =
        "Assets/_Project/VisualTuningSavedValues.asset";
    public const string ResourcePath = "VisualTuningSavedValues";

    public static bool TryLoad(
        out VisualTuningSnapshot snapshot,
        out string source,
        out string message)
    {
        snapshot = default;
        source = AssetPath;
        VisualTuningPreset preset = null;
#if UNITY_EDITOR
        preset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
            AssetPath);
        if (preset == null)
        {
            preset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
                LegacyAssetPath);
            if (preset != null)
                source = LegacyAssetPath;
        }
#else
        preset = Resources.Load<VisualTuningPreset>(ResourcePath);
#endif
        if (preset == null || !preset.HasValues)
        {
            message = "Visual production preset ещё не создан.";
            return false;
        }

        snapshot = preset.Values;
        message = "Visual production preset загружен из " + source;
        return true;
    }

    public static bool Save(
        VisualTuningSnapshot snapshot, string text, out string message)
    {
#if UNITY_EDITOR
        try
        {
            EnsureAssetFolder();
            VisualTuningPreset preset =
                UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
                    AssetPath);
            if (preset == null)
            {
                VisualTuningPreset legacy =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
                        LegacyAssetPath);
                if (legacy != null)
                {
                    string moveError = UnityEditor.AssetDatabase.MoveAsset(
                        LegacyAssetPath, AssetPath);
                    if (!string.IsNullOrWhiteSpace(moveError))
                        throw new InvalidOperationException(moveError);
                    preset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
                        AssetPath);
                }
                else
                {
                    preset = ScriptableObject.CreateInstance<VisualTuningPreset>();
                    preset.name = "VisualTuningSavedValues";
                    UnityEditor.Undo.RegisterCreatedObjectUndo(
                        preset, "Save Visual Lab tuning preset");
                    UnityEditor.AssetDatabase.CreateAsset(preset, AssetPath);
                }
            }

            UnityEditor.Undo.RecordObject(
                preset, "Save Visual Lab tuning preset");

            VisualTuningSnapshot oldProduction = preset.Values;
            preset.Capture(snapshot, text);
            UnityEditor.EditorUtility.SetDirty(preset);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.ImportAsset(
                AssetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
            VisualTuningPreset verified =
                UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
                    AssetPath);
            if (verified == null ||
                UnityEngine.JsonUtility.ToJson(verified.Values) !=
                UnityEngine.JsonUtility.ToJson(snapshot))
            {
                throw new InvalidOperationException(
                    "asset reread differs from the runtime snapshot");
            }

            Debug.Log(BuildSaveDiagnostic(oldProduction, verified.Values));
            message = "Значения сохранены и проверены: " + AssetPath;
            return true;
        }
        catch (Exception exception)
        {
            message = "Не удалось сохранить visual preset: " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
#else
        message = "Сохранение asset доступно только в Unity Editor.";
        return false;
#endif
    }

#if UNITY_EDITOR
    private static void EnsureAssetFolder()
    {
        const string folder = "Assets/_Project/Resources";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            UnityEditor.AssetDatabase.CreateFolder(
                "Assets/_Project", "Resources");
    }

    private static string BuildSaveDiagnostic(
        VisualTuningSnapshot oldProduction,
        VisualTuningSnapshot saved)
    {
        return "[VisualLab SAVE]\n" +
            $"ProjectileVisualScale: runtime={saved.ProjectileScale:0.###}, " +
            $"old production={oldProduction.ProjectileScale:0.###}, " +
            $"saved production={saved.ProjectileScale:0.###}\n" +
            $"TrailWidth: runtime={saved.TrailWidth:0.###}, " +
            $"old production={oldProduction.TrailWidth:0.###}, " +
            $"saved production={saved.TrailWidth:0.###}\n" +
            $"TrailTime: runtime={saved.TrailLifetime:0.###}, " +
            $"old production={oldProduction.TrailLifetime:0.###}, " +
            $"saved production={saved.TrailLifetime:0.###}\n" +
            $"CameraZoom: runtime={saved.CameraOrthographicSize:0.###}, " +
            $"old production={oldProduction.CameraOrthographicSize:0.###}, " +
            $"saved production={saved.CameraOrthographicSize:0.###}\n" +
            "source=" + AssetPath;
    }
#endif
}
