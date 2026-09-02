using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    /// <summary>Lab-only pooled mine field. No production weapon/runtime dependencies.</summary>
    public sealed class OrbitalMineSystem
    {
        private const int Capacity = 256;

        private sealed class Mine
        {
            public Transform Transform;
            public SpriteRenderer Core;
            public SpriteRenderer Halo;
            public OrbitalMineLayer Owner;
            public float Damage;
            public float TriggerRadius;
            public float ExplosionRadius;
            public float Push;
            public float DieAt;
            public bool Active;
        }

        private readonly Mine[] mines = new Mine[Capacity];
        private readonly OrbitalCombatLabController lab;
        private int cursor;
        public int ActiveCount { get; private set; }

        public OrbitalMineSystem(OrbitalCombatLabController lab, Transform parent, OrbitalPrimitiveFactory factory)
        {
            this.lab = lab;
            Transform root = new GameObject("Mine Pool").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                Transform mineRoot = new GameObject($"Mine {i + 1:000}").transform;
                mineRoot.SetParent(root, false);
                SpriteRenderer halo = factory.CreateSprite("Trigger Halo", mineRoot, factory.Circle,
                    new Color(.28f, 1f, .32f, .12f), Vector2.one, 5);
                SpriteRenderer core = factory.CreateSprite("Core", mineRoot, factory.Circle,
                    new Color(1f, .58f, .08f, .95f), new Vector2(.23f, .23f), 8);
                mineRoot.gameObject.SetActive(false);
                mines[i] = new Mine { Transform = mineRoot, Core = core, Halo = halo };
            }
        }

        public bool Drop(OrbitalMineLayer owner, Vector2 position, float damage, float triggerRadius,
            float explosionRadius, float lifetime, float push)
        {
            if (owner == null) return false;
            Mine mine = mines[cursor++];
            if (cursor >= Capacity) cursor = 0;
            if (mine.Active) Deactivate(mine);
            mine.Owner = owner;
            mine.Damage = damage;
            mine.TriggerRadius = triggerRadius;
            mine.ExplosionRadius = explosionRadius;
            mine.Push = push;
            mine.DieAt = Time.unscaledTime + Mathf.Max(.2f, lifetime);
            mine.Active = true;
            mine.Transform.position = position;
            mine.Transform.localScale = Vector3.one;
            mine.Halo.transform.localScale = Vector3.one * triggerRadius * 2f;
            mine.Transform.gameObject.SetActive(true);
            owner.NotifyMineAdded();
            ActiveCount++;
            return true;
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < Capacity; i++)
            {
                Mine mine = mines[i];
                if (!mine.Active) continue;
                float pulse = 1f + Mathf.Sin(now * 6f + i * .7f) * .14f;
                mine.Core.transform.localScale = Vector3.one * (.23f * pulse);
                if (now >= mine.DieAt)
                {
                    Deactivate(mine);
                    continue;
                }
                int target = lab.Crowd.FindNearest(mine.Transform.position, mine.TriggerRadius);
                if (target >= 0) Explode(mine);
            }
            lab.Stats.ActiveMines = ActiveCount;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) Deactivate(mines[i]);
        }

        private void Explode(Mine mine)
        {
            Vector2 origin = mine.Transform.position;
            float radiusSqr = mine.ExplosionRadius * mine.ExplosionRadius;
            for (int i = 0; i < lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[i];
                if (!enemy.Active || ((Vector2)enemy.Transform.position - origin).sqrMagnitude > radiusSqr) continue;
                lab.Crowd.Damage(i, mine.Damage);
                if (enemy.Active && mine.Push > 0f) lab.Crowd.Push(i, origin, mine.Push);
            }
            lab.EmitPulse(origin, new Color(.55f, 1f, .12f, .82f), mine.ExplosionRadius * 2f, .28f);
            lab.ImpulseCamera(.035f);
            Deactivate(mine);
        }

        private void Deactivate(Mine mine)
        {
            if (mine == null || !mine.Active) return;
            mine.Active = false;
            mine.Transform.gameObject.SetActive(false);
            mine.Owner?.NotifyMineRemoved();
            mine.Owner = null;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }
    }

    public sealed class OrbitalMineLayer : OrbitalMountedObject
    {
        protected override Color BaseColor => new(.42f, 1f, .16f, 1f);
        public int ActiveMines { get; private set; }

        public OrbitalMineLayer(OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory)
            : base(OrbitalMountType.MineLayer, "Mine Layer", lab, factory, factory.Square,
                new Color(.42f, 1f, .16f, 1f), new Vector2(.42f, .32f)) { }

        protected override void TickCombat(float deltaTime)
        {
            MineSettings settings = Lab.Mines;
            float explosion = settings.ExplosionRadius * EffectSizeMultiplier;
            SetRangeCircle(explosion);
            SetPrototypeColliderRadius(.3f);
            Transform.rotation = Quaternion.Euler(0f, 0f, Ring.GetMountedAngle(this));
            if (Time.time < NextActionTime || ActiveMines >= settings.MaximumActivePerLayer) return;
            NextActionTime = Time.time + settings.DropInterval * CooldownMultiplier;
            if (Lab.MineSystem.Drop(this, Transform.position, settings.Damage * DamageMultiplier,
                settings.TriggerRadius * EffectSizeMultiplier, explosion, settings.Lifetime,
                settings.PushForce * PushMultiplier))
            {
                FlashResonance(.12f);
                Lab.EmitPulse(Transform.position, new Color(.5f, 1f, .18f, .5f), .4f, .12f);
            }
        }

        public override void OnCorePulse(float power)
        {
            base.OnCorePulse(power);
            NextActionTime = 0f;
        }

        public void NotifyMineAdded() => ActiveMines++;
        public void NotifyMineRemoved() => ActiveMines = Mathf.Max(0, ActiveMines - 1);
    }
}
