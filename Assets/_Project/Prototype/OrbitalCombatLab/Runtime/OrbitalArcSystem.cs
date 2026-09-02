using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    /// <summary>Short-lived pooled lightning flashes, intentionally distinct from persistent Link lines.</summary>
    public sealed class OrbitalArcSystem
    {
        public float VisualBrightness = 1f;
        private const int Capacity = 64;
        private readonly LineRenderer[] lines = new LineRenderer[Capacity];
        private readonly float[] dieAt = new float[Capacity];
        private int cursor;

        public OrbitalArcSystem(Transform parent, OrbitalPrimitiveFactory factory)
        {
            Transform root = new GameObject("Arc Flash Pool").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                LineRenderer line = factory.CreateCircleLine($"Arc {i + 1:00}", root, 15, 4);
                line.loop = false;
                line.enabled = false;
                lines[i] = line;
            }
        }

        public void Show(Vector2 from, Vector2 to, float width, float duration, float seed)
        {
            LineRenderer line = lines[cursor];
            int index = cursor++;
            if (cursor >= Capacity) cursor = 0;
            Vector2 perpendicular = Vector2.Perpendicular((to - from).normalized);
            float jitter = Mathf.Sin(seed * 13.17f + Time.unscaledTime * 31f) * .16f;
            line.SetPosition(0, from);
            line.SetPosition(1, Vector2.Lerp(from, to, .34f) + perpendicular * jitter);
            line.SetPosition(2, Vector2.Lerp(from, to, .68f) - perpendicular * jitter * .7f);
            line.SetPosition(3, to);
            line.startWidth = width;
            line.endWidth = width * .35f;
            Color color = new Color(.82f, .68f, 1f, .96f) * Mathf.Max(.1f, VisualBrightness);
            color.a = .96f;
            line.startColor = line.endColor = color;
            line.enabled = true;
            dieAt[index] = Time.unscaledTime + duration;
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < Capacity; i++)
                if (lines[i].enabled && now >= dieAt[i]) lines[i].enabled = false;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) lines[i].enabled = false;
        }
    }

    public sealed class OrbitalArcEmitter : OrbitalMountedObject
    {
        protected override Color BaseColor => new(.82f, .66f, 1f, 1f);
        private readonly int[] chained = new int[12];
        private float pulseBonus = 1f;

        public OrbitalArcEmitter(OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory)
            : base(OrbitalMountType.ArcEmitter, "Arc Emitter", lab, factory, factory.Circle,
                new Color(.82f, .66f, 1f, 1f), new Vector2(.42f, .42f)) { }

        protected override void TickCombat(float deltaTime)
        {
            ArcSettings settings = Lab.Arc;
            float range = settings.Range * EffectSizeMultiplier;
            SetRangeCircle(range);
            SetPrototypeColliderRadius(.28f);
            if (Time.time < NextActionTime) return;
            int first = Lab.Crowd.FindNearest(Transform.position, range);
            Lab.Stats.ArcChecks++;
            if (first < 0) return;
            NextActionTime = Time.time + settings.Cooldown * CooldownMultiplier;
            Discharge(first, pulseBonus);
            pulseBonus = 1f;
        }

        public override void OnCorePulse(float power)
        {
            base.OnCorePulse(power);
            pulseBonus = Mathf.Max(pulseBonus, Lab.Arc.PulseBonus * power);
            NextActionTime = 0f;
        }

        private void Discharge(int first, float bonus)
        {
            ArcSettings settings = Lab.Arc;
            Lab.Stats.ArcDischarges++;
            int count = Mathf.Clamp(settings.ChainCount + (settings.LinkConduction && Lab.HasLinkNodeOnRing(Ring) ? 1 : 0), 1, chained.Length);
            for (int i = 0; i < chained.Length; i++) chained[i] = -1;
            Vector2 from = Transform.position;
            int current = first;
            for (int hop = 0; hop < count && current >= 0; hop++)
            {
                OrbitalEnemyCrowd.Enemy enemy = Lab.Crowd.Enemies[current];
                if (!enemy.Active || enemy.Transform == null) break;
                Vector2 to = enemy.Transform.position;
                // Long enough to read as a branched discharge, still far shorter than a persistent Link line.
                Lab.ArcSystem.Show(from, to, .075f * EffectSizeMultiplier, .19f, current + hop * 17f);
                Lab.Crowd.Damage(current, settings.Damage * DamageMultiplier * bonus * Mathf.Pow(.82f, hop));
                Lab.Stats.ArcHits++;
                chained[hop] = current;
                from = to;
                current = FindNext(from, settings.ChainRange * EffectSizeMultiplier, hop + 1);
            }
            Lab.EmitPulse(Transform.position, new Color(.78f, .55f, 1f, .7f), .55f, .13f);
        }

        private int FindNext(Vector2 position, float range, int used)
        {
            int best = -1;
            float bestSqr = range * range;
            for (int i = 0; i < Lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = Lab.Crowd.Enemies[i];
                if (!enemy.Active || enemy.Transform == null || WasUsed(i, used)) continue;
                float sqr = ((Vector2)enemy.Transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        private bool WasUsed(int index, int used)
        {
            for (int i = 0; i < used; i++) if (chained[i] == index) return true;
            return false;
        }
    }
}
