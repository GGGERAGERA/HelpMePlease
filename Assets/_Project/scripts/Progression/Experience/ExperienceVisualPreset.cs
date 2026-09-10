using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/XP Visual Preset")]
public sealed class ExperienceVisualPreset : ScriptableObject
{
    public Sprite sprite;
    [ColorUsage(false)] public Color sparkColor = Color.cyan;
    [Min(0)] public int productionWeight = 1;
}
