using System;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    // Shared mounted projectile weapon; authored profiles determine cadence and collision rules.
    public class OrbitalProjectileModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => Settings.Kind;
        public readonly OrbitalWeaponSettings Settings;
        private readonly ProductionOrbitalCombatAdapter adapter;
        private readonly System.Random spreadRandom;
        private readonly Action<float> shotFeedback;
        
        private float chargeRemaining = -1f;
        private int burstRemaining;
        private static long nextVolley;
        public int ShotsFired { get; private set; }
        public float DamageMultiplier => Station.State.ShootingDamageMultiplier;
        public float FireRateMultiplier => Station.State.ShootingRateMultiplier;
        public OrbitalProjectileModule(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings, Action<float> shotFeedback = null)
            : base(station, id, settings.Color, settings.WeaponVisual, settings.Kind)
        {
            Settings = settings; adapter = (ProductionOrbitalCombatAdapter)Combat;
            
            this.shotFeedback = shotFeedback;
            spreadRandom = new System.Random(1709 + id);
            ConfigureWeaponFeedback(settings.Color, true);
            
            Cooldown = .1f + id % 7 * .07f;
        }
        public override void UpdateVisualRotation(float radians) { }
        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            var target = Combat.FindNearest(WorldPosition, Settings.Range);
            if (target != null) AimAt(target.transform.position);
            if (deltaTime <= 0f || Mount == null) return;
            if (Station.Owner.IsDead) { burstRemaining = 0; chargeRemaining = -1f; WeaponCharge(0f); return; }
            if (chargeRemaining >= 0f)
            {
                if (target == null) { chargeRemaining = -1f; WeaponCharge(0f); return; }
                chargeRemaining -= deltaTime;
                WeaponCharge(1f - chargeRemaining / Mathf.Max(.001f, Settings.ChargeSeconds));
                
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
            var volley = pellets > 1 ? new ProductionOrbitalCombatAdapter.ProjectileVolley(++nextVolley) : null;
            for (int i = 0; i < pellets; i++)
            {
                float angle = pellets > 1 ? Mathf.Lerp(-.5f, .5f, (float)i / (pellets - 1)) * Settings.SpreadDegrees
                    : ((float)spreadRandom.NextDouble() - .5f) * Settings.SpreadDegrees;
                Vector2 flight = Quaternion.Euler(0f, 0f, angle) * direction;
                adapter.SpawnWeaponProjectile(WorldPosition, target, flight, Settings, volley, Settings.Damage * Power * DamageMultiplier);
            }
            ShotsFired++;
            WeaponCharge(0f); WeaponRecoil(Settings.Recoil);
            float size = pellets > 1 ? .42f : Settings.BodyPierces > 1 ? .32f : .13f;
            Vector2 side = new(-direction.y, direction.x);
            Station.FlashLink(WorldPosition, WorldPosition + direction * size, Settings.Color, .06f);
            Station.FlashLink(WorldPosition - side * size * .4f, WorldPosition + side * size * .4f, Color.white, .05f);
            AudioService.Instance?.PlayAt(Settings.ShotSound, WorldPosition);
            shotFeedback?.Invoke(Settings.Recoil / .23f);
            if (--burstRemaining > 0) Cooldown = Mathf.Max(.02f, Settings.BurstInterval / FireRateMultiplier);
            else Cooldown = Mathf.Max(.05f, Settings.FireInterval / FireRateMultiplier);
        }
        public override void OnCorePulse() => ActivateCombat();
        public void ResetFiring() { burstRemaining = 0; chargeRemaining = -1f; Cooldown = .2f; ResetWeaponFeedback(); }
    }
}
