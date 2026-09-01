using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public enum OrbitalMountType { Gun, Blade, Pusher }

    [System.Serializable]
    public sealed class OrbitalRingSettings
    {
        public float Radius = 1.5f;
        public float RotationSpeed = 95f;
        public bool Clockwise;
        [Range(1, 8)] public int MaxMounts = 4;
        public float ContactPush = 1.5f;
        public float ContactDamage = 2f;
        public Color Color = new(0.2f, 0.9f, 1f, 0.42f);
        public float LineWidth = 0.045f;
        public bool Visible = true;
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

    public sealed class OrbitalLabStats
    {
        public int Kills;
        public int Shots;
        public int BladeHits;
        public int PushHits;
        public int ActiveEnemies;
        public float SmoothedFps;

        public void Reset()
        {
            Kills = Shots = BladeHits = PushHits = ActiveEnemies = 0;
        }
    }
}
