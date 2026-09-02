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
        public bool HasMiniWeaponVisual => miniVisual != null && miniVisual.HasInstance;
        public int DisabledProductionColliders => miniVisual != null ? miniVisual.DisabledColliderCount : 0;
        public int MiniWeaponParticleSystems => miniVisual != null ? miniVisual.ParticleCount : 0;
        public bool MiniWeaponHasAnimator => miniVisual != null && miniVisual.HasAnimator;
        public bool MiniWeaponEffectPlaying => miniVisual != null && miniVisual.IsEffectPlaying;
        public bool MiniWeaponUsesLabUnlitMaterial => miniVisual != null && miniVisual.UsesLabUnlitMaterial;
        public bool ProductionCollidersDisabled => miniVisual != null && miniVisual.AllProductionCollidersDisabled;
        public int VisualActionCount { get; private set; }

        protected readonly LineRenderer RangeLine;
        protected readonly OrbitalCombatLabController Lab;
        protected float NextActionTime;
        private readonly Vector3 primitiveBaseScale;
        private readonly TrailRenderer trail;
        private readonly OrbitalMiniWeaponVisual miniVisual;
        private readonly SpriteRenderer mountMarker;
        private readonly SpriteRenderer muzzleMarker;
        private readonly LineRenderer forwardLine;
        private readonly LineRenderer colliderLine;
        private float resonanceFlashUntil;
        private float debugColliderRadius = .55f;

        protected OrbitalMountedObject(OrbitalMountType type, string name,
            OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory, Sprite sprite,
            Color color, Vector2 size)
        {
            Type = type;
            Lab = lab;
            Transform = new GameObject(name + " Mount Root").transform;
            Transform.SetParent(lab.WorldRoot, false);
            Renderer = factory.CreateSprite(name + " Primitive Visual", Transform,
                sprite, color, size, 11);
            primitiveBaseScale = Renderer.transform.localScale;
            RangeLine = factory.CreateCircleLine("Attack Radius", Transform, 3, 56);
            RangeLine.startColor = RangeLine.endColor = new Color(color.r, color.g, color.b, .16f);
            RangeLine.startWidth = RangeLine.endWidth = .025f;
            RangeLine.enabled = false;
            trail = Transform.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = factory.LineMaterial;
            trail.sortingOrder = 7;
            trail.minVertexDistance = .08f;
            trail.numCornerVertices = 2;
            trail.emitting = false;

            mountMarker = factory.CreateSprite("Debug Mount Root", Transform, factory.Circle,
                new Color(.2f, 1f, .72f, .92f), new Vector2(.13f, .13f), 31);
            muzzleMarker = factory.CreateSprite("Debug Muzzle", Transform, factory.Circle,
                new Color(1f, .9f, .16f, .95f), new Vector2(.12f, .12f), 32);
            forwardLine = factory.CreateCircleLine("Debug Visual Forward", Transform, 30, 2);
            forwardLine.loop = false;
            forwardLine.startWidth = forwardLine.endWidth = .035f;
            forwardLine.startColor = forwardLine.endColor = new Color(.25f, 1f, .45f, .9f);
            colliderLine = factory.CreateCircleLine("Debug Prototype Collider", Transform, 29, 48);
            colliderLine.startWidth = colliderLine.endWidth = .03f;
            colliderLine.startColor = colliderLine.endColor = new Color(1f, .48f, .08f, .8f);
            mountMarker.enabled = muzzleMarker.enabled = forwardLine.enabled = colliderLine.enabled = false;

            if (type == OrbitalMountType.Gun || type == OrbitalMountType.Blade || type == OrbitalMountType.Pusher)
                miniVisual = new OrbitalMiniWeaponVisual(type, Transform, lab, factory.LineMaterial);
            RefreshVisualMode();
        }

        public void Attach(OrbitalRing ring, int slot)
        {
            Detach();
            Ring = ring;
            Slot = slot;
            ring.Mounts[slot] = this;
            IsDragging = false;
            Renderer.color = BaseColor;
            Renderer.transform.localScale = primitiveBaseScale;
            miniVisual?.SetDragState(false, true);
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
            if (!IsDragging) Transform.position = Ring.GetMountedPosition(Lab.PlayerPosition, this);
            Transform.localScale = Vector3.one;
            Renderer.transform.localScale = Type == OrbitalMountType.LinkNode
                ? primitiveBaseScale * Lab.WeaponVisuals.LinkNodeScale : primitiveBaseScale;
            // Apply live visual scale/offset before combat so a shot on the first frame
            // (or immediately after a mode switch) uses the actual prefab muzzle socket.
            float powerGlow = Ring != null ? Mathf.Clamp01((Ring.Upgrades.DamageMultiplier - 1f) * .72f) : 0f;
            Renderer.color = Color.Lerp(BaseColor, Color.white, powerGlow * .28f);
            miniVisual?.SetPowerGlow(powerGlow);
            miniVisual?.Tick();
            TickCombat(deltaTime);
            RefreshPrimitiveVisibility();
            TickTrail();
            if (Time.unscaledTime < resonanceFlashUntil)
            {
                Renderer.color = Color.Lerp(Renderer.color, Color.white, Mathf.Clamp01(Lab.ResonanceFlash));
                Renderer.transform.localScale *= 1.24f;
            }
            miniVisual?.SetDragState(false, true);
            TickDebugVisuals();
        }

        public void SetDraggedPosition(Vector2 position)
        {
            Transform.position = new Vector3(position.x, position.y, 0f);
            Renderer.color = new Color(.35f, 1f, .55f, 1f);
            Renderer.transform.localScale = primitiveBaseScale * 1.22f;
            RangeLine.enabled = false;
            trail.emitting = false;
            trail.Clear();
            miniVisual?.SetDragState(true, true);
            TickDebugVisuals();
        }

        public void SetDragValidity(bool valid)
        {
            Renderer.color = valid ? new Color(.35f, 1f, .55f, 1f) : new Color(1f, .18f, .2f, 1f);
            miniVisual?.SetDragState(true, valid);
        }

        public virtual void SetRangesVisible(bool visible) =>
            RangeLine.enabled = visible && !IsDragging;

        public bool HitTest(Vector2 world, float padding = .16f)
        {
            if (IsDestroyed) return false;
            if (HasMiniWeaponVisual && miniVisual.HitTest(world, padding)) return true;
            float radius = Mathf.Max(Renderer.bounds.extents.x, Renderer.bounds.extents.y) + padding;
            return ((Vector2)Transform.position - world).sqrMagnitude <= radius * radius;
        }

        public void Destroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            Detach();
            miniVisual?.Destroy();
            Object.Destroy(Transform.gameObject);
        }

        public void FlashResonance(float duration)
        {
            resonanceFlashUntil = Mathf.Max(resonanceFlashUntil, Time.unscaledTime + duration);
            miniVisual?.Flash(duration);
        }

        public virtual void OnCorePulse(float power)
        {
            FlashResonance(.24f);
        }

        public void ClearTrail()
        {
            trail.emitting = false;
            trail.Clear();
        }

        public void RefreshVisualMode()
        {
            miniVisual?.RefreshMode();
            RefreshPrimitiveVisibility();
        }

        protected abstract Color BaseColor { get; }
        protected abstract void TickCombat(float deltaTime);
        protected float DamageMultiplier => (Ring != null ? Ring.Upgrades.DamageMultiplier : 1f) *
            Lab.Core.GlobalDamageMultiplier;
        protected float CooldownMultiplier => Ring != null ? Ring.Upgrades.CooldownMultiplier : 1f;
        protected float EffectSizeMultiplier => (Ring != null ? Ring.Upgrades.EffectSizeMultiplier : 1f) *
            Lab.Core.GlobalEffectSizeMultiplier;
        protected float PushMultiplier => Ring != null ? Ring.Upgrades.PushMultiplier : 1f;
        protected Vector2 MuzzlePosition => miniVisual != null && miniVisual.HasInstance
            ? miniVisual.MuzzlePosition : (Vector2)Transform.position;

        protected void TriggerVisual(OrbitalVisualAction action)
        {
            VisualActionCount++;
            miniVisual?.Trigger(action);
        }

        protected void SetPrimitiveVisualSize(Vector2 size) =>
            Renderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        protected void SetPrimitiveVisualScale(float multiplier) =>
            Renderer.transform.localScale = primitiveBaseScale * multiplier;

        protected void SetPrototypeColliderRadius(float radius) =>
            debugColliderRadius = Mathf.Max(.05f, radius);

        protected void SetRangeCircle(float radius) =>
            OrbitalPrimitiveFactory.SetCircle(RangeLine, Transform.position, radius);

        private void RefreshPrimitiveVisibility()
        {
            Renderer.enabled = Type == OrbitalMountType.LinkNode ||
                Lab.WeaponVisuals.Mode == OrbitalWeaponVisualMode.Primitives ||
                miniVisual == null || !miniVisual.HasInstance;
        }

        private void TickDebugVisuals()
        {
            mountMarker.enabled = Lab.WeaponVisuals.ShowMountRoots;
            mountMarker.transform.position = Transform.position;
            bool showMuzzle = Lab.WeaponVisuals.ShowMuzzlePoints && Type == OrbitalMountType.Gun;
            muzzleMarker.enabled = showMuzzle;
            if (showMuzzle) muzzleMarker.transform.position = MuzzlePosition;
            forwardLine.enabled = Lab.WeaponVisuals.ShowVisualForward;
            if (forwardLine.enabled)
            {
                Vector2 start = Transform.position;
                Vector2 direction = miniVisual != null && miniVisual.HasInstance ? miniVisual.Forward :
                    Type == OrbitalMountType.Blade ? (Vector2)Transform.up : (Vector2)Transform.right;
                forwardLine.SetPosition(0, start);
                forwardLine.SetPosition(1, start + direction.normalized * .85f);
            }
            colliderLine.enabled = Lab.WeaponVisuals.ShowPrototypeColliders;
            if (colliderLine.enabled) OrbitalPrimitiveFactory.SetCircle(colliderLine, Transform.position, debugColliderRadius);
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
                Type == OrbitalMountType.Pusher ? new Color(1f, .65f, .05f) : new Color(1f, .08f, .88f);
            color.a = Mathf.Clamp01(settings.Alpha * Lab.TrailAlpha);
            Color end = color;
            end.a = 0f;
            trail.startColor = color;
            trail.endColor = end;
        }
    }
}
