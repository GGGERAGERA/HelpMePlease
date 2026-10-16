using System;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

[Serializable]
public sealed class OrbitalWeaponSettings
{
    [Serializable]
    public sealed class ProjectileStyle
    {
        public Color Color = new(.1f, .95f, 1f);
        [Tooltip("Full body size in metres, along/across flight. Also defines the swept hit shape.")]
        public Vector2 BodySize = new(.24f, .16f);
        [Min(0f)] public float TrailSeconds = .045f;
        [Min(0f)] public float TrailWidth = .035f;
        [Range(0f, 1f)] public float TrailOpacity = .3f;
    }
    // Supplied by the presentation asset; legacy/lab profiles retain their authored visuals.
    public ProjectileStyle Presentation;
    public OrbitalUpgradeCategory Category;
    public OrbitalModuleKind Kind;
    [Tooltip("Visual-only wrapper containing the authored artist prefab.")] public GameObject WeaponVisual;
    public GameObject ProjectileVisual;
    [Min(.1f)] public float ProjectileVisualScale = 1f;
    [Min(-1f), Tooltip("Trail lifetime on this pooled shot only. Negative keeps the authored preset.")]
    public float ProjectileTrailSeconds = -1f;
    [Tooltip("Explicit sniper-only shield bypass.")] public bool BypassShields;
    [Min(.1f)] public float Damage = 4f;
    [Min(.05f), Tooltip("Seconds between bursts/volleys, after their last shot.")] public float FireInterval = .6f;
    [Min(.1f)] public float Range = 8f;
    [Min(1f)] public float ProjectileSpeed = 22f;
    [Min(.01f)] public float ProjectileRadius = .04f;
    [Min(1)] public int BurstShots = 3;
    [Min(.02f)] public float BurstInterval = .09f;
    [Min(1)] public int Pellets = 1;
    [Range(0f, 90f)] public float SpreadDegrees = 3f;
    [Min(1), Tooltip("Maximum distinct bodies damaged by one projectile.")] public int BodyPierces = 1;
    [Min(0f)] public float ChargeSeconds;
    [Range(0f, 5f)] public float Knockback;
    [Range(0f, .4f)] public float Recoil = .06f;
    public Color Color = new(.35f, 1f, 1f);
    public AudioCueId ShotSound = AudioCueId.PistolShot;
    [Min(.05f)] public float ContactRadius = .65f;
    [Min(.05f)] public float ContactHitInterval = .32f;
    public float SpinDegreesPerSecond = 720f;
    public static OrbitalWeaponSettings Pistol() => new()
    {
        Damage = OrbitalPistolModule.BaseDamage, FireInterval = .55f, Range = 8f, ProjectileSpeed = 13f,
        ProjectileRadius = .14f, BurstShots = 1, Pellets = 1, SpreadDegrees = 0f,
        Color = Color.white
    };
    public static OrbitalWeaponSettings Shotgun() => new()
    {
        Kind = OrbitalModuleKind.Shotgun, Damage = 3.5f, FireInterval = 1.05f, Range = 4.5f, ProjectileSpeed = 10f,
        ProjectileRadius = .14f, BurstShots = 1, Pellets = 7, SpreadDegrees = 42f,
        Knockback = 4f, Recoil = .23f, Color = new(1f, .65f, .25f), ShotSound = AudioCueId.OrbitalImpulseFire
    };
    public static OrbitalWeaponSettings Spear() => new()
    {
        Damage = 22f, FireInterval = 1.5f, Range = 10f, ProjectileSpeed = 18f,
        ProjectileRadius = .13f, BurstShots = 1, SpreadDegrees = 0f, BodyPierces = 3,
        ChargeSeconds = .28f, Recoil = .16f, Color = new(.75f, .4f, 1f), ShotSound = AudioCueId.OrbitalArcFire
    };
    public static OrbitalWeaponSettings Sniper() => new()
    {
        Kind = OrbitalModuleKind.Sniper, Damage = 32f, FireInterval = 1.8f, Range = 12f, ProjectileSpeed = 120f, ProjectileTrailSeconds = .02f,
        ProjectileRadius = .14f, BurstShots = 1, SpreadDegrees = 0f, BodyPierces = 1,
        BypassShields = true, Recoil = .16f, Color = new(1f, .25f, .25f), ShotSound = AudioCueId.OrbitalArcFire
    };
    public static OrbitalWeaponSettings Saw() => new()
    {
        Kind = OrbitalModuleKind.Saw, Category = OrbitalUpgradeCategory.Contact, Damage = 13f, ContactRadius = .65f,
        ContactHitInterval = .32f, Color = new(.6f, .4f, 1f), ShotSound = AudioCueId.OrbitalSwordHit
    };
}

