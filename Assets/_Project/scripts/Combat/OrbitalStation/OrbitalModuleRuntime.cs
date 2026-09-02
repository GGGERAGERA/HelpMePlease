using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalModuleKind
    {
        Pistol,
        LaserSword,
        ImpulseGun,
        ArcEmitter,
        LinkNode
    }

    public abstract class OrbitalModuleRuntime
    {
        protected readonly OrbitalStationRuntime Station;
        protected readonly IOrbitalCombatAdapter Combat;
        protected readonly GameObject Visual;
        private readonly OrbitalModuleVisual presentation;
        protected OrbitalMountRuntime Mount;
        protected float Cooldown;

        public abstract OrbitalModuleKind Kind { get; }
        public OrbitalMountRuntime CurrentMount => Mount;
        public int StableModuleId { get; }
        public Vector2 WorldPosition => Visual != null ? Visual.transform.position : Vector2.zero;
        internal float RuntimeCooldown
        {
            get => Cooldown;
            set => Cooldown = value;
        }
        protected float Power => (Mount?.Ring.PowerMultiplier ?? 1f) *
            Station.Core.DamageMultiplier;

        protected OrbitalModuleRuntime(OrbitalStationRuntime station,
            int stableModuleId, Color color)
        {
            Station = station;
            Combat = station.Combat;
            StableModuleId = stableModuleId;
            presentation = new OrbitalModuleVisual(station, Kind, GetType().Name, color);
            Visual = presentation.GameObject;
        }

        public virtual void Attach(OrbitalMountRuntime mount)
        {
            Mount = mount;
            Visual.transform.SetParent(mount.Transform, false);
            Visual.transform.localPosition = Vector3.zero;
            Visual.SetActive(true);
        }

        public virtual void Detach()
        {
            Mount = null;
            Visual.SetActive(false);
        }

        public void SetRewardPresentationVisible(bool visible)
        {
            if (Visual != null)
                Visual.SetActive(visible);
        }

        public void SetHighlighted(bool highlighted) =>
            presentation.SetHighlighted(highlighted);

        public bool HitTest(Vector2 world, float padding) =>
            presentation.HitTest(world, padding) ||
            (WorldPosition - world).sqrMagnitude <= padding * padding;

        public void SetDragValidity(bool valid) =>
            presentation.SetDragState(true, valid);

        public void BeginPresentationDrag(Transform dragRoot)
        {
            if (Visual == null)
                return;
            Visual.transform.SetParent(dragRoot, true);
            SetHighlighted(true);
            presentation.SetDragState(true, true);
        }

        public void SetDragPosition(Vector2 worldPosition)
        {
            if (Visual != null)
                Visual.transform.position = worldPosition;
        }

        public void CancelPresentationDrag()
        {
            if (Visual == null || Mount == null)
                return;
            Visual.transform.SetParent(Mount.Transform, false);
            Visual.transform.localPosition = Vector3.zero;
            SetHighlighted(false);
            presentation.SetDragState(false, true);
        }

        public virtual void Tick(float deltaTime)
        {
            Cooldown -= deltaTime;
            presentation.Tick();
        }

        public virtual void ActivateCombat() { }
        public virtual void OnCorePulse() => ActivateCombat();
        protected void TriggerPresentation() => presentation.Trigger();

        protected void AimAt(Vector2 worldTarget)
        {
            if (Visual == null)
                return;
            Vector2 direction = worldTarget - (Vector2)Visual.transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            Visual.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public virtual void UpdateVisualRotation(float radians)
        {
            Visual.transform.localRotation = Quaternion.Euler(0f, 0f,
                radians * Mathf.Rad2Deg);
        }

        public virtual void Teardown()
        {
            Mount = null;
            if (Visual != null)
                Object.Destroy(Visual);
        }
    }

    public sealed class OrbitalPistolModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.Pistol;
        public OrbitalPistolModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, new Color(0.35f, 0.95f, 1f)) { }

        // A turret keeps its last aim instead of inheriting the ring's spin.
        public override void UpdateVisualRotation(float radians) { }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (Mount != null)
            {
                EnemyHealth aimTarget = Combat.FindNearest(
                    Visual.transform.position, 8f);
                if (aimTarget != null)
                    AimAt(aimTarget.transform.position);
            }
            if (Cooldown <= 0f)
                ActivateCombat();
        }

        public override void ActivateCombat()
        {
            if (Mount == null)
                return;
            EnemyHealth target = Combat.FindNearest(Visual.transform.position, 8f);
            if (target == null)
                return;
            AimAt(target.transform.position);
            Combat.SpawnProjectile(Visual.transform.position, target, 13f,
                8f * Power, new Color(0.25f, 0.95f, 1f));
            TriggerPresentation();
            Cooldown = 0.55f * Station.Core.CooldownMultiplier;
            Station.FlashCore(new Color(0.25f, 0.95f, 1f));
        }
    }

    public sealed class OrbitalLaserSwordModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.LaserSword;
        public OrbitalLaserSwordModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, new Color(1f, 0.25f, 0.8f)) { }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (Cooldown > 0f || Mount == null)
                return;
            EnemyHealth target = Combat.FindNearest(Visual.transform.position, 0.75f);
            if (target != null)
            {
                Combat.ApplyDamage(target, 13f * Power, Visual.transform.position);
                TriggerPresentation();
                Cooldown = 0.32f * Station.Core.CooldownMultiplier;
            }
        }
    }

    public sealed class OrbitalImpulseGunModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.ImpulseGun;
        public OrbitalImpulseGunModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, new Color(1f, 0.75f, 0.2f)) { }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (Cooldown <= 0f)
                ActivateCombat();
        }

        public override void ActivateCombat()
        {
            if (Mount == null)
                return;
            EnemyHealth target = Combat.FindNearest(Visual.transform.position, 5f);
            if (target == null)
                return;
            Vector2 direction = ((Vector2)target.transform.position -
                (Vector2)Station.Owner.Transform.position).normalized;
            Combat.ApplyDamage(target, 6f * Power, target.transform.position);
            TriggerPresentation();
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body != null)
                body.AddForce(direction * 4.5f *
                    Mount.Ring.PowerMultiplier, ForceMode2D.Impulse);
            Cooldown = 1.25f * Station.Core.CooldownMultiplier;
        }
    }

    public sealed class OrbitalArcEmitterModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.ArcEmitter;
        public OrbitalArcEmitterModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, new Color(0.72f, 0.3f, 1f)) { }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (Cooldown <= 0f)
                ActivateCombat();
        }

        public override void ActivateCombat()
        {
            if (Mount == null)
                return;
            List<EnemyHealth> targets = Combat.FindNearestMany(
                Visual.transform.position, 6f, 3);
            Vector2 from = Visual.transform.position;
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyHealth target = targets[i];
                Station.FlashLink(from, target.transform.position,
                    new Color(0.72f, 0.3f, 1f), 0.12f);
                Combat.ApplyDamage(target, (7f - i * 1.25f) * Power,
                    target.transform.position);
                from = target.transform.position;
            }
            if (targets.Count > 0)
                Cooldown = 1.15f * Station.Core.CooldownMultiplier;
        }
    }

    public sealed class OrbitalLinkNodeModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.LinkNode;
        public OrbitalLinkNodeModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, new Color(0.85f, 0.25f, 1f)) { }
        public override void Tick(float deltaTime) => base.Tick(deltaTime);
    }
}
