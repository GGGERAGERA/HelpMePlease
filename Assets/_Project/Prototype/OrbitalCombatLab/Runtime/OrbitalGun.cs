using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalGun : OrbitalMountedObject
    {
        protected override Color BaseColor => new(.12f, .78f, 1f, 1f);
        private float flashUntil;

        public OrbitalGun(OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory)
            : base(OrbitalMountType.Gun, "Gun", lab, factory, factory.Square,
                new Color(.12f, .78f, 1f, 1f), new Vector2(.58f, .25f)) { }

        protected override void TickCombat(float deltaTime)
        {
            GunSettings settings = Lab.Gun;
            SetRangeCircle(settings.Range * EffectSizeMultiplier);
            SetPrototypeColliderRadius(.34f);
            Renderer.color = Time.time < flashUntil ? Color.white : BaseColor;
            float range = settings.Range * EffectSizeMultiplier;
            int target = Lab.Crowd.FindNearest(Transform.position, range);
            if (target < 0) return;
            Vector2 targetPosition = Lab.Crowd.Enemies[target].Transform.position;
            Vector2 direction = targetPosition - (Vector2)Transform.position;
            Transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (Time.time < NextActionTime) return;
            NextActionTime = Time.time + CooldownMultiplier / Mathf.Max(.05f, settings.FireRate);
            Vector2 muzzle = MuzzlePosition;
            direction = targetPosition - muzzle;
            Lab.Projectiles.Fire(muzzle, direction, settings.ProjectileSpeed,
                settings.Damage * DamageMultiplier, range);
            Lab.Stats.Shots++;
            flashUntil = Time.time + .055f;
            TriggerVisual(OrbitalVisualAction.GunFire);
            Lab.EmitPulse(muzzle, new Color(.15f, .9f, 1f, .75f), .3f, .09f);
        }

        public void FireResonance(Vector2 direction, float damageMultiplier = 1.8f)
        {
            GunSettings settings = Lab.Gun;
            Transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Lab.Projectiles.Fire(MuzzlePosition, direction, settings.ProjectileSpeed * 1.25f,
                settings.Damage * DamageMultiplier * damageMultiplier,
                settings.Range * EffectSizeMultiplier * 1.25f);
            Lab.Stats.Shots++;
            flashUntil = Time.time + .12f;
            TriggerVisual(OrbitalVisualAction.GunFire);
        }

        public override void OnCorePulse(float power)
        {
            base.OnCorePulse(power);
            Vector2 direction = ((Vector2)Transform.position - Lab.PlayerPosition).normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector2.right;
            FireResonance(direction, Mathf.Max(.35f, power));
        }
    }
}
