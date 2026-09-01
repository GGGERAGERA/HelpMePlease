using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public abstract class OrbitalMountedObject
    {
        public readonly OrbitalMountType Type;
        public readonly Transform Transform;
        public readonly SpriteRenderer Renderer;
        public OrbitalRing Ring { get; private set; }
        public int Slot { get; private set; } = -1;
        public bool IsDragging { get; set; }
        public bool IsDestroyed { get; private set; }
        public float PhaseOffset;

        protected readonly LineRenderer RangeLine;
        protected readonly OrbitalCombatLabController Lab;
        protected float NextActionTime;
        private readonly Vector3 baseScale;
        private readonly TrailRenderer trail;
        private float resonanceFlashUntil;

        protected OrbitalMountedObject(OrbitalMountType type, string name,
            OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory, Sprite sprite,
            Color color, Vector2 size)
        {
            Type = type;
            Lab = lab;
            Renderer = factory.CreateSprite(name, lab.WorldRoot, sprite, color, size, 11);
            Transform = Renderer.transform;
            baseScale = Transform.localScale;
            RangeLine = factory.CreateCircleLine("Attack Radius", Transform, 3, 56);
            RangeLine.startColor = RangeLine.endColor = new Color(color.r, color.g, color.b, .16f);
            RangeLine.startWidth = RangeLine.endWidth = .025f;
            RangeLine.enabled = false;
            trail = Renderer.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = factory.LineMaterial;
            trail.sortingOrder = 7;
            trail.minVertexDistance = .08f;
            trail.numCornerVertices = 2;
            trail.emitting = false;
        }

        public void Attach(OrbitalRing ring, int slot)
        {
            Detach();
            Ring = ring;
            Slot = slot;
            ring.Mounts[slot] = this;
            IsDragging = false;
            Renderer.color = BaseColor;
            Transform.localScale = baseScale;
            trail.Clear();
        }

        public void Detach()
        {
            if (Ring != null && Slot >= 0 && Slot < Ring.Mounts.Length && Ring.Mounts[Slot] == this)
                Ring.Mounts[Slot] = null;
            Ring = null;
            Slot = -1;
        }

        public void Tick(float deltaTime)
        {
            if (IsDestroyed || Ring == null) return;
            if (!IsDragging)
                Transform.position = Ring.GetMountedPosition(Lab.PlayerPosition, this);
            Transform.localScale = baseScale;
            TickCombat(deltaTime);
            TickTrail();
            if (Time.unscaledTime < resonanceFlashUntil)
            {
                Renderer.color = Color.Lerp(Renderer.color, Color.white,
                    Mathf.Clamp01(Lab.ResonanceFlash));
                Transform.localScale *= 1.24f;
            }
        }

        public void SetDraggedPosition(Vector2 position)
        {
            Transform.position = new Vector3(position.x, position.y, 0f);
            Renderer.color = new Color(.35f, 1f, .55f, 1f);
            Transform.localScale = baseScale * 1.22f;
            RangeLine.enabled = false;
            trail.emitting = false;
            trail.Clear();
        }

        public void SetDragValidity(bool valid)
        {
            Renderer.color = valid ? new Color(.35f, 1f, .55f, 1f) : new Color(1f, .18f, .2f, 1f);
        }

        public virtual void SetRangesVisible(bool visible)
        {
            RangeLine.enabled = visible && !IsDragging;
        }

        public bool HitTest(Vector2 world, float radius = .55f) =>
            !IsDestroyed && ((Vector2)Transform.position - world).sqrMagnitude <= radius * radius;

        public void Destroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            Detach();
            Object.Destroy(Transform.gameObject);
        }

        public void FlashResonance(float duration) =>
            resonanceFlashUntil = Mathf.Max(resonanceFlashUntil, Time.unscaledTime + duration);

        public void ClearTrail()
        {
            trail.emitting = false;
            trail.Clear();
        }

        protected abstract Color BaseColor { get; }
        protected abstract void TickCombat(float deltaTime);

        protected void SetRangeCircle(float radius)
        {
            OrbitalPrimitiveFactory.SetCircle(RangeLine, Transform.position, radius);
        }

        private void TickTrail()
        {
            TrailSettings settings = Lab.Trails;
            bool enabled = settings.Mode != OrbitalTrailMode.Off && !IsDragging;
            trail.emitting = enabled;
            if (!enabled)
            {
                trail.Clear();
                return;
            }
            float modeLength = settings.Mode == OrbitalTrailMode.Short ? .32f :
                settings.Mode == OrbitalTrailMode.Medium ? .85f : 2.25f;
            trail.time = Mathf.Max(.05f, settings.Length * modeLength);
            trail.startWidth = settings.Width;
            trail.endWidth = settings.Width * .08f;
            Color color = Type == OrbitalMountType.Gun ? new Color(.12f, .9f, 1f) :
                Type == OrbitalMountType.Blade ? new Color(1f, .12f, .16f) :
                Type == OrbitalMountType.Pusher ? new Color(1f, .65f, .05f) :
                new Color(1f, .08f, .88f);
            color.a = Mathf.Clamp01(settings.Alpha * Lab.TrailAlpha);
            Color end = color;
            end.a = 0f;
            trail.startColor = color;
            trail.endColor = end;
        }
    }
}
