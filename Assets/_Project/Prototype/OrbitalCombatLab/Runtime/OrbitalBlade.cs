using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalBlade : OrbitalMountedObject
    {
        protected override Color BaseColor => new(1f, .16f, .18f, 1f);
        private readonly float[] lastHit = new float[OrbitalEnemyCrowd.Capacity];

        public OrbitalBlade(OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory)
            : base(OrbitalMountType.Blade, "Blade", lab, factory, factory.Square,
                new Color(1f, .16f, .18f, 1f), new Vector2(.28f, 1.05f))
        {
            for (int i = 0; i < lastHit.Length; i++) lastHit[i] = -99f;
        }

        protected override void TickCombat(float deltaTime)
        {
            BladeSettings settings = Lab.Blade;
            float size = settings.Size * EffectSizeMultiplier;
            SetPrimitiveVisualSize(new Vector2(.28f, size));
            float radial = Ring.GetMountedAngle(this);
            float orientation = Lab.WeaponVisuals.BladeOrientation == OrbitalBladeOrientation.Tangential
                ? radial : radial - 90f;
            Transform.rotation = Quaternion.Euler(0f, 0f, orientation);
            float radius = Mathf.Max(.32f, size * .48f);
            SetPrototypeColliderRadius(radius);
            float now = Time.time;
            bool visualHit = false;
            for (int i = 0; i < Lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = Lab.Crowd.Enemies[i];
                if (!enemy.Active || now - lastHit[i] < settings.HitCooldown) continue;
                if (((Vector2)enemy.Transform.position - (Vector2)Transform.position).sqrMagnitude >
                    radius * radius) continue;
                lastHit[i] = now;
                Lab.Crowd.Damage(i, settings.Damage * DamageMultiplier);
                Lab.Stats.BladeHits++;
                Lab.EmitPulse(enemy.Transform.position, new Color(1f, .2f, .15f, .68f), .34f, .12f);
                visualHit = true;
            }
            if (visualHit) TriggerVisual(OrbitalVisualAction.BladeHit);
        }

        public override void SetRangesVisible(bool visible)
        {
            base.SetRangesVisible(false);
        }
    }
}
