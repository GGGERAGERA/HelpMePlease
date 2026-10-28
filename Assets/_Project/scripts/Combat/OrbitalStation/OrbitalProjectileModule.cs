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
        private EnemyHealth previousAimTarget;
        private Vector2 previousAimPosition;
        private Vector2 estimatedTargetVelocity;
        private static long nextVolley;
        public int ShotsFired { get; private set; }
        public float DamageMultiplier => Station.State.ShootingDamageMultiplier;
        public float FireRateMultiplier => Station.State.ShootingRateMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        protected virtual bool LabFixedDirection => false;
        protected virtual float? LabExtraShotAngle => null;
        protected virtual float LabDamageFactor => 1f;
        protected virtual float LabRateFactor => 1f;
        protected virtual int LabRicochets => 0;
        protected virtual void LabOrdinaryShot() { }

        // One volley through the same projectile/feedback path, without touching charge, burst or cooldown.
        public bool FireLabExtraShot()
        {
            if (Mount == null || Station.Owner.IsDead || !Visual.activeInHierarchy || !UsesFixedDirection) return false;
            EmitShot(null);
            return true;
        }
#endif
        private bool UsesFixedDirection
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return LabFixedDirection;
#else
                return false;
#endif
            }
        }
        private Vector2 RadialDirection => Mount == null ? Vector2.right :
            ((Vector2)Mount.Transform.position - (Vector2)Station.transform.position).normalized;
        public OrbitalProjectileModule(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings, Action<float> shotFeedback = null)
            : base(station, id, settings.Color, settings.WeaponVisual, settings.Kind)
        {
            Settings = settings; adapter = (ProductionOrbitalCombatAdapter)Combat;
            
            this.shotFeedback = shotFeedback;
            spreadRandom = new System.Random(1709 + id);
            ConfigureWeaponFeedback(settings.Color, true);
            
            Cooldown = .1f + id % 7 * .07f;
        }
        public override void UpdateVisualRotation(float radians)
        {
            if (UsesFixedDirection && Mount != null) AimAt(WorldPosition + RadialDirection);
        }
        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            bool fixedDirection = UsesFixedDirection;
            var target = fixedDirection ? null : Combat.FindNearest(WorldPosition, Settings.Range);
            if (!fixedDirection) TrackAimTarget(target, deltaTime);
            if (fixedDirection && Mount != null) AimAt(WorldPosition + RadialDirection);
            else if (target != null) AimAt(WorldPosition + AimDirection(target));
            if (deltaTime <= 0f || Mount == null) return;
            if (Station.Owner.IsDead) { burstRemaining = 0; chargeRemaining = -1f; WeaponCharge(0f); return; }
            if (chargeRemaining >= 0f)
            {
                if (target == null && !fixedDirection) { chargeRemaining = -1f; WeaponCharge(0f); return; }
                chargeRemaining -= deltaTime;
                WeaponCharge(1f - chargeRemaining / Mathf.Max(.001f, Settings.ChargeSeconds));
                
                if (chargeRemaining <= 0f) { chargeRemaining = -1f; Fire(target); }
                return;
            }
            if (Cooldown > 0f || target == null && !fixedDirection) return;
            if (burstRemaining > 0) Fire(target);
            else if (Settings.ChargeSeconds > 0f) chargeRemaining = Settings.ChargeSeconds;
            else Fire(target);
        }
        public override void ActivateCombat()
        {
            // Core pulses keep the same projectile rules and never skip charging or cadence.
            if (Cooldown > 0f || chargeRemaining >= 0f) return;
            var target = UsesFixedDirection ? null : Combat.FindNearest(WorldPosition, Settings.Range);
            if (target == null && !UsesFixedDirection) return;
            if (Settings.ChargeSeconds > 0f) chargeRemaining = Settings.ChargeSeconds;
            else Fire(target);
        }
        private void Fire(EnemyHealth target)
        {
            if (burstRemaining == 0) burstRemaining = Mathf.Max(1, Settings.BurstShots);
            EmitShot(target);
            float rate = FireRateMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            rate *= LabRateFactor;
#endif
            if (--burstRemaining > 0) Cooldown = Mathf.Max(.02f, Settings.BurstInterval / rate);
            else Cooldown = Mathf.Max(.05f, Settings.FireInterval / rate);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LabOrdinaryShot();
#endif
        }
        private void EmitShot(EnemyHealth target)
        {
            Vector2 direction = UsesFixedDirection ? RadialDirection : AimDirection(target);
            AimAt(WorldPosition + direction);
            int pellets = Mathf.Max(1, Settings.Pellets);
            int projectileCount = pellets;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float? extraAngle = LabExtraShotAngle;
            if (extraAngle.HasValue) projectileCount++;
#endif
            var volley = projectileCount > 1 ? new ProductionOrbitalCombatAdapter.ProjectileVolley(++nextVolley) : null;
            float damage = Settings.Damage * Power * DamageMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            damage *= LabDamageFactor;
#endif
            for (int i = 0; i < projectileCount; i++)
            {
                float angle;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (extraAngle.HasValue && i == pellets) angle = extraAngle.Value;
                else
#endif
                    angle = pellets > 1 ? Mathf.Lerp(-.5f, .5f, (float)i / (pellets - 1)) * Settings.SpreadDegrees
                    : ((float)spreadRandom.NextDouble() - .5f) * Settings.SpreadDegrees;
                Vector2 flight = Quaternion.Euler(0f, 0f, angle) * direction;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (UsesFixedDirection)
                    adapter.SpawnLabDirectionalProjectile(WorldPosition, flight, Settings, volley, damage, StableModuleId, LabRicochets);
                else
#endif
                    adapter.SpawnWeaponProjectile(WorldPosition, target, flight, Settings, volley, damage);
            }
            ShotsFired++;
            WeaponCharge(0f); WeaponRecoil(Settings.Recoil);
            float size = pellets > 1 ? .42f : Settings.Kind == OrbitalModuleKind.Sniper ? .36f : .28f;
            Vector2 side = new(-direction.y, direction.x);
            var family = Settings.Kind switch
            {
                OrbitalModuleKind.Shotgun => OrbitalFlashFamily.MuzzleShotgun,
                OrbitalModuleKind.Sniper => OrbitalFlashFamily.MuzzleSniper,
                _ => OrbitalFlashFamily.MuzzlePistol
            };
            Color muzzleColor = Settings.Presentation != null ? Settings.Presentation.Color : Settings.Color;
            Station.FlashLink(WorldPosition, WorldPosition + direction * size, muzzleColor,
                OrbitalPresentationConfig.Active.ProjectileMuzzleDuration, family);
            Station.FlashLink(WorldPosition - side * size * .4f, WorldPosition + side * size * .4f,
                Color.Lerp(muzzleColor, Color.white, .25f), .045f, family);
            AudioService.Instance?.PlayAt(Settings.ShotSound, WorldPosition);
            shotFeedback?.Invoke(Settings.Recoil / .23f);
        }
        public override void OnCorePulse() => ActivateCombat();
        private void TrackAimTarget(EnemyHealth target, float deltaTime)
        {
            if (Settings.AimLeadSeconds <= 0f) return;
            Vector2 position = target != null ? (Vector2)target.transform.position : Vector2.zero;
            // MovePosition enemies have transient Rigidbody velocity. Observe their
            // displacement and smooth physics/render sampling at different frame rates.
            estimatedTargetVelocity = target != null && target == previousAimTarget && deltaTime > 0f
                ? Vector2.Lerp(estimatedTargetVelocity, (position - previousAimPosition) / deltaTime,
                    1f - Mathf.Exp(-deltaTime / .08f))
                : Vector2.zero;
            previousAimTarget = target;
            previousAimPosition = position;
        }

        private Vector2 AimDirection(EnemyHealth target) => EvaluateAimDirection(WorldPosition,
            target.transform.position, target == previousAimTarget ? estimatedTargetVelocity : Vector2.zero,
            Settings.ProjectileSpeed, Settings.AimLeadSeconds, Settings.MaxAimCorrectionDegrees);

        // Correct the launch direction only. A fired projectile remains a straight
        // physical shot: no target retention, retargeting or steering in flight.
        public static Vector2 EvaluateAimDirection(Vector2 origin, Vector2 target, Vector2 targetVelocity,
            float projectileSpeed, float maximumLeadSeconds, float maximumCorrectionDegrees)
        {
            Vector2 offset = target - origin;
            Vector2 direct = offset.normalized;
            if (maximumLeadSeconds <= 0f || maximumCorrectionDegrees <= 0f) return direct;
            float lead = Mathf.Min(maximumLeadSeconds, offset.magnitude / projectileSpeed);
            Vector2 predicted = (offset + targetVelocity * lead).normalized;
            return Vector3.RotateTowards(direct, predicted, maximumCorrectionDegrees * Mathf.Deg2Rad, 0f);
        }

        public void ResetFiring()
        {
            burstRemaining = 0; chargeRemaining = -1f; Cooldown = .2f;
            previousAimTarget = null; estimatedTargetVelocity = Vector2.zero;
            ResetWeaponFeedback();
        }
    }
}
