using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public enum OrbitalMountType { Gun, Blade, Pusher, LinkNode, MineLayer, ArcEmitter }
    public enum OrbitalLinkMode { Pairs, Chain, AllNearby }
    public enum OrbitalResonanceMode { RadialVolley, Beam, Shockwave, Cycle }
    public enum OrbitalMovementPreset { Default, Gear, Flower, Wave, Sync, Chaos, Freeze }
    public enum OrbitalTrailMode { Off, Short, Medium, Hypnotic }
    public enum OrbitalShape { Circle, Ellipse, Breathing, Wobble }
    public enum OrbitalRingFieldMode { Ghost, Slow, Pulse, Cut, Conductor }
    public enum OrbitalVisualProfile { Clean, Combat, Hypnotic, Maximum }
    public enum OrbitalWeaponVisualMode { Primitives, MiniWeapons }
    public enum OrbitalBladeOrientation { Tangential, Radial }
    public enum OrbitalRingSpacingMode { ConstantGap, GrowingGap, Compressed }
    public enum OrbitalRingSpeedMode { Alternating, OuterSlower, Constant, GoldenRatio, ControlledChaos }
    public enum OrbitalCameraMode { FullStation, CombatFocus }
    public enum OrbitalCorePulseMode { Visual, Volley, Resonance, Cascade }
    public enum OrbitalUpgradeLayer { Core, Ring, Weapon }
    public enum OrbitalRingUpgradeType { Overdrive, Amplifier, SystemsAcceleration, ExtraMount, EffectField, ResonantRing, Stabilizer }
    public enum OrbitalCoreUpgradeType { NewRing, CorePower, PulseFrequency, FieldScale, LinkMatrix, Stabilization }

    [System.Serializable]
    public sealed class OrbitalRingGenerationSettings
    {
        public OrbitalRingSpacingMode SpacingMode = OrbitalRingSpacingMode.Compressed;
        public OrbitalRingSpeedMode SpeedMode = OrbitalRingSpeedMode.GoldenRatio;
        public float FirstRingRadius = 1.5f;
        public float BaseRingGap = 1.05f;
        public float GapGrowth = .075f;
        public float MinimumGap = .52f;
        public int CompressionStartRing = 10;
        public float BaseSpeed = 105f;
        public int ChaosSeed = 4242;
    }

    [System.Serializable]
    public sealed class OrbitalRingUpgradeState
    {
        public int Level;
        public float DamageMultiplier = 1f;
        public float CooldownMultiplier = 1f;
        public float EffectSizeMultiplier = 1f;
        public float RotationSpeedMultiplier = 1f;
        public float PushMultiplier = 1f;
        public float LinkPowerMultiplier = 1f;
        public int MountCapacityBonus;
        public float ResonancePower = 1f;

        public void Reset()
        {
            Level = MountCapacityBonus = 0;
            DamageMultiplier = CooldownMultiplier = EffectSizeMultiplier =
                RotationSpeedMultiplier = PushMultiplier = LinkPowerMultiplier = ResonancePower = 1f;
        }
    }

    [System.Serializable]
    public sealed class OrbitalCoreSettings
    {
        public int Level;
        public float GlobalDamageMultiplier = 1f;
        public float GlobalEffectSizeMultiplier = 1f;
        public float PulseInterval = 4.8f;
        public float PulseTravelSpeed = 8f;
        public float PulseWidth = .55f;
        public float PulseBrightness = 1f;
        public bool PulseGameplayEffect = true;
        public OrbitalCorePulseMode PulseMode = OrbitalCorePulseMode.Cascade;
        public int LinkCapacityBonus;
        public float LinkRangeMultiplier = 1f;
        public float ResonancePowerMultiplier = 1f;

        public void Reset()
        {
            Level = LinkCapacityBonus = 0;
            GlobalDamageMultiplier = GlobalEffectSizeMultiplier = LinkRangeMultiplier = ResonancePowerMultiplier = 1f;
            PulseInterval = 4.8f;
        }
    }

    [System.Serializable]
    public sealed class MineSettings
    {
        public float Damage = 24f;
        public float DropInterval = 1.25f;
        public float TriggerRadius = .72f;
        public float ExplosionRadius = 1.55f;
        public float Lifetime = 10f;
        public int MaximumActivePerLayer = 6;
        public float PushForce = 5f;
    }

    [System.Serializable]
    public sealed class ArcSettings
    {
        public float Damage = 13f;
        public float Cooldown = .9f;
        public float Range = 5.5f;
        public int ChainCount = 3;
        public float ChainRange = 2.4f;
        public bool LinkConduction = true;
        public float PulseBonus = 1.75f;
    }

    [System.Serializable]
    public sealed class OrbitalRingSettings
    {
        public float Radius = 1.5f;
        public float RotationSpeed = 95f;
        public bool Clockwise;
        public bool Paused;
        [Range(1, 8)] public int MaxMounts = 4;
        public float ContactPush = 1.5f;
        public float ContactDamage = 2f;
        public Color Color = new(0.2f, 0.9f, 1f, 0.42f);
        public float LineWidth = 0.045f;
        public bool Visible = true;
        public OrbitalShape Shape = OrbitalShape.Circle;
        public float AspectRatio = 1.45f;
        public float ShapeRotation;
        public float BreathingAmplitude = .35f;
        public float BreathingFrequency = .45f;
        public float BreathingPhase;
        public int WobbleLobes = 5;
        public float WobbleAmplitude = .22f;
        public float WobbleSpeed = 2f;
        public OrbitalRingFieldMode FieldMode = OrbitalRingFieldMode.Ghost;
        public float FieldWidth = .22f;
        public float FieldDamage = 5f;
        public float SlowMultiplier = .55f;
        public float FieldPushForce = 7f;
        public float PulseInterval = 1.8f;
        public float FieldTargetCooldown = .35f;
        public float GeneratedLineAlpha = 1f;
    }

    [System.Serializable]
    public sealed class GunSettings
    {
        public float Damage = 10f;
        public float FireRate = 2.5f;
        public float Range = 8f;
        public float ProjectileSpeed = 16f;
    }

    [System.Serializable]
    public sealed class BladeSettings
    {
        public float Damage = 20f;
        public float HitCooldown = 0.3f;
        public float Size = 1.15f;
    }

    [System.Serializable]
    public sealed class PusherSettings
    {
        public float PushForce = 12f;
        public float PushRadius = 1.45f;
        public float Cooldown = 0.7f;
    }

    [System.Serializable]
    public sealed class LinkSettings
    {
        public OrbitalLinkMode Mode = OrbitalLinkMode.Pairs;
        public float Damage = 8f;
        public float HitCooldown = .35f;
        public float LineWidth = .055f;
        public float MaxDistance = 9f;
        public float PulseSpeed = 3f;
        public Color LineColor = new(1f, .06f, .84f, 1f);
        public bool DealDamage = true;
        public bool ShowLinks = true;
    }

    [System.Serializable]
    public sealed class ResonanceSettings
    {
        public bool Enabled = true;
        public float AlignmentTolerance = 10f;
        public int MinimumObjects = 2;
        public float Cooldown = 1.15f;
        public float Damage = 16f;
        public float Range = 9f;
        public OrbitalResonanceMode Mode = OrbitalResonanceMode.Cycle;
        public bool VisualOnly;
    }

    [System.Serializable]
    public sealed class TrailSettings
    {
        public OrbitalTrailMode Mode = OrbitalTrailMode.Off;
        public float Length = .75f;
        public float Width = .08f;
        public float Alpha = .38f;
        public bool FollowVisualProfile = true;
    }

    [System.Serializable]
    public sealed class WeaponVisualSettings
    {
        public OrbitalWeaponVisualMode Mode = OrbitalWeaponVisualMode.MiniWeapons;
        public OrbitalBladeOrientation BladeOrientation = OrbitalBladeOrientation.Tangential;
        public float PistolScale = 2.15f;
        public float LaserSwardScale = 1.85f;
        public float ImpulsGunScale = 2.15f;
        public float LinkNodeScale = 1f;
        public float PistolRotationOffset;
        public float LaserSwardRotationOffset;
        public float ImpulsGunRotationOffset;
        public int SortingOffset = 12;
        public bool EffectsEnabled = true;
        public float EffectIntensity = .55f;
        public bool ShowPrototypeColliders;
        public bool ShowMuzzlePoints;
        public bool ShowVisualForward;
        public bool ShowMountRoots;
    }

    public sealed class OrbitalLabStats
    {
        public int Kills;
        public int Shots;
        public int BladeHits;
        public int PushHits;
        public int LinkHits;
        public int RingFieldHits;
        public int Resonances;
        public int ActiveLinks;
        public int ActiveMines;
        public int ArcChecks;
        public int ArcDischarges;
        public int ArcHits;
        public int CorePulses;
        public string LastResonance = "—";
        public int ActiveEnemies;
        public float SmoothedFps;

        public void Reset()
        {
            Kills = Shots = BladeHits = PushHits = LinkHits = RingFieldHits =
                Resonances = ActiveLinks = ActiveMines = ArcChecks = ArcDischarges = ArcHits =
                CorePulses = ActiveEnemies = 0;
            LastResonance = "—";
        }
    }
}
