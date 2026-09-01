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

        protected readonly LineRenderer RangeLine;
        protected readonly OrbitalCombatLabController Lab;
        protected float NextActionTime;
        private readonly Vector3 baseScale;

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
                Transform.position = Ring.GetSlotPosition(Lab.PlayerPosition, Slot);
            TickCombat(deltaTime);
        }

        public void SetDraggedPosition(Vector2 position)
        {
            Transform.position = new Vector3(position.x, position.y, 0f);
            Renderer.color = new Color(.35f, 1f, .55f, 1f);
            Transform.localScale = baseScale * 1.22f;
            RangeLine.enabled = false;
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

        protected abstract Color BaseColor { get; }
        protected abstract void TickCombat(float deltaTime);

        protected void SetRangeCircle(float radius)
        {
            OrbitalPrimitiveFactory.SetCircle(RangeLine, Transform.position, radius);
        }
    }
}
