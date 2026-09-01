using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public enum OrbitalMountType { Gun, Blade, Pusher, LinkNode }
    public enum OrbitalLinkMode { Pairs, Chain, AllNearby }
    public enum OrbitalResonanceMode { RadialVolley, Beam, Shockwave, Cycle }
    public enum OrbitalMovementPreset { Default, Gear, Flower, Wave, Sync, Chaos, Freeze }
    public enum OrbitalTrailMode { Off, Short, Medium, Hypnotic }
    public enum OrbitalShape { Circle, Ellipse, Breathing, Wobble }
    public enum OrbitalRingFieldMode { Ghost, Slow, Pulse, Cut, Conductor }
    public enum OrbitalVisualProfile { Clean, Combat, Hypnotic, Maximum }
    public enum OrbitalWeaponVisualMode { Primitives, MiniWeapons }
    public enum OrbitalBladeOrientation { Tangential, Radial }

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
        public string LastResonance = "—";
        public int ActiveEnemies;
        public float SmoothedFps;

        public void Reset()
        {
            Kills = Shots = BladeHits = PushHits = LinkHits = RingFieldHits =
                Resonances = ActiveLinks = ActiveEnemies = 0;
            LastResonance = "—";
        }
    }
}
