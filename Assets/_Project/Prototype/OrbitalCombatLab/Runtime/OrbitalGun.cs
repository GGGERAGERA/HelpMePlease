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
            SetRangeCircle(settings.Range);
            Renderer.color = Time.time < flashUntil ? Color.white : BaseColor;
            int target = Lab.Crowd.FindNearest(Transform.position, settings.Range);
            if (target < 0) return;
            Vector2 targetPosition = Lab.Crowd.Enemies[target].Transform.position;
            Vector2 direction = targetPosition - (Vector2)Transform.position;
            Transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (Time.time < NextActionTime) return;
            NextActionTime = Time.time + 1f / Mathf.Max(.05f, settings.FireRate);
            Lab.Projectiles.Fire(Transform.position, direction, settings.ProjectileSpeed,
                settings.Damage, settings.Range);
            Lab.Stats.Shots++;
            flashUntil = Time.time + .055f;
            Lab.EmitPulse(Transform.position, new Color(.15f, .9f, 1f, .75f), .3f, .09f);
        }
    }
}
