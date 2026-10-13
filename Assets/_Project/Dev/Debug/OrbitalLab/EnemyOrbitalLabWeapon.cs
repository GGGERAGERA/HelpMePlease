#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

[Serializable]
public sealed class EnemyOrbitalLabWeaponSettings
{
    public GameObject ProjectileVisual;
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
    public static EnemyOrbitalLabWeaponSettings Shotgun() => new()
    {
        Damage = 3.5f, FireInterval = 1.05f, Range = 4.5f, ProjectileSpeed = 16f,
        ProjectileRadius = .07f, BurstShots = 1, Pellets = 7, SpreadDegrees = 42f,
        Knockback = 4f, Recoil = .23f, Color = new(1f, .65f, .25f), ShotSound = AudioCueId.OrbitalImpulseFire
    };
    public static EnemyOrbitalLabWeaponSettings Spear() => new()
    {
        Damage = 22f, FireInterval = 1.5f, Range = 10f, ProjectileSpeed = 18f,
        ProjectileRadius = .13f, BurstShots = 1, SpreadDegrees = 0f, BodyPierces = 3,
        ChargeSeconds = .28f, Recoil = .16f, Color = new(.75f, .4f, 1f), ShotSound = AudioCueId.OrbitalArcFire
    };
}

namespace Subject42.Combat.OrbitalStation
{
    // Lab factory supplies these as ordinary mounted pistol slots; rewards/saves never know their profiles.
    public sealed class EnemyOrbitalLabWeapon : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.Pistol;
        public readonly EnemyOrbitalLabWeaponSettings Settings;
        private readonly ProductionOrbitalCombatAdapter adapter;
        private readonly System.Random spreadRandom;
        private readonly Action<float> shotFeedback;
        private float chargeRemaining = -1f;
        private int burstRemaining;
        private static long nextVolley;
        public int ShotsFired { get; private set; }
        public EnemyOrbitalLabWeapon(OrbitalStationRuntime station, int id, EnemyOrbitalLabWeaponSettings settings, Action<float> shotFeedback)
            : base(station, id, settings.Color)
        {
            Settings = settings; adapter = (ProductionOrbitalCombatAdapter)Combat;
            this.shotFeedback = shotFeedback;
            spreadRandom = new System.Random(1709 + id);
            ConfigureLabFeedback(settings.Color);
        }
        public override void UpdateVisualRotation(float radians) { }
        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            var target = Combat.FindNearest(WorldPosition, Settings.Range);
            if (target != null) AimAt(target.transform.position);
            if (deltaTime <= 0f || Mount == null) return;
            if (Station.Owner.IsDead) { burstRemaining = 0; chargeRemaining = -1f; LabCharge(0f); return; }
            if (chargeRemaining >= 0f)
            {
                if (target == null) { chargeRemaining = -1f; LabCharge(0f); return; }
                chargeRemaining -= deltaTime;
                LabCharge(1f - chargeRemaining / Mathf.Max(.001f, Settings.ChargeSeconds));
                if (chargeRemaining <= 0f) { chargeRemaining = -1f; Fire(target); }
                return;
            }
            if (Cooldown > 0f || target == null) return;
            if (burstRemaining > 0) Fire(target);
            else if (Settings.ChargeSeconds > 0f) chargeRemaining = Settings.ChargeSeconds;
            else Fire(target);
        }
        public override void ActivateCombat()
        {
            // Core pulses keep the same projectile rules and never skip charging or cadence.
            if (Cooldown > 0f || chargeRemaining >= 0f) return;
            var target = Combat.FindNearest(WorldPosition, Settings.Range);
            if (target == null) return;
            if (Settings.ChargeSeconds > 0f) chargeRemaining = Settings.ChargeSeconds;
            else Fire(target);
        }
        private void Fire(EnemyHealth target)
        {
            if (burstRemaining == 0) burstRemaining = Mathf.Max(1, Settings.BurstShots);
            Vector2 direction = ((Vector2)target.transform.position - WorldPosition).normalized;
            AimAt(target.transform.position);
            int pellets = Mathf.Max(1, Settings.Pellets);
            var volley = pellets > 1 ? new ProductionOrbitalCombatAdapter.LabVolley(++nextVolley) : null;
            for (int i = 0; i < pellets; i++)
            {
                float angle = pellets > 1 ? Mathf.Lerp(-.5f, .5f, (float)i / (pellets - 1)) * Settings.SpreadDegrees
                    : ((float)spreadRandom.NextDouble() - .5f) * Settings.SpreadDegrees;
                Vector2 flight = Quaternion.Euler(0f, 0f, angle) * direction;
                adapter.SpawnLabProjectile(WorldPosition, target, flight, Settings, volley, Settings.Damage * Power);
            }
            ShotsFired++;
            LabCharge(0f); LabRecoil(Settings.Recoil);
            float size = pellets > 1 ? .42f : Settings.BodyPierces > 1 ? .32f : .13f;
            Vector2 side = new(-direction.y, direction.x);
            Station.FlashLink(WorldPosition, WorldPosition + direction * size, Settings.Color, .06f);
            Station.FlashLink(WorldPosition - side * size * .4f, WorldPosition + side * size * .4f, Color.white, .05f);
            AudioService.Instance?.PlayAt(Settings.ShotSound, WorldPosition);
            shotFeedback(Settings.Recoil / .23f);
            if (--burstRemaining > 0) Cooldown = Mathf.Max(.02f, Settings.BurstInterval);
            else Cooldown = Mathf.Max(.05f, Settings.FireInterval);
        }
        public override void OnCorePulse() => ActivateCombat();
        public void ResetFiring() { burstRemaining = 0; chargeRemaining = -1f; Cooldown = .2f; ResetLabFeedback(); }
    }
}
#endif
