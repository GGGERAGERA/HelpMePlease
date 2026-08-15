using UnityEngine;

public static class AnomalyPowerLevelProfiles
{
    public static int ClampLevel(int level) => Mathf.Clamp(level, 1, 3);

    public static float GravityDamage(int level) => ClampLevel(level) switch
    {
        1 => 1f,
        2 => 1.25f,
        _ => 1.5f
    };

    public static float GravitySize(int level) => ClampLevel(level) switch
    {
        1 => 1f,
        2 => 1.15f,
        _ => 1.3f
    };

    public static float ArcDamage(int level) => ClampLevel(level) switch
    {
        1 => 1f,
        2 => 1.2f,
        _ => 1.4f
    };

    public static int ArcTargets(int level) => 3 + ClampLevel(level);

    public static float BeamDamage(int level) => ClampLevel(level) switch
    {
        1 => 1f,
        2 => 1.2f,
        _ => 1.4f
    };

    public static float BeamWidth(int level) => ClampLevel(level) switch
    {
        1 => 1f,
        2 => 1.15f,
        _ => 1.3f
    };
}
