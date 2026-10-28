using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalModuleKind
    {
        Pistol = 0,
        // Serialized 1 and 2 are retired. Never reuse them.
        ArcEmitter = 3,
        LinkNode = 4,
        Sniper = 5,
        Shotgun = 6,
        Saw = 7
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
        public float PresentationReach => presentation.FramingReach;
        internal float RuntimeCooldown
        {
            get => Cooldown;
            set => Cooldown = value;
        }
        protected float Power => (Mount?.Ring.PowerMultiplier ?? 1f) *
            Station.GetModuleMetaDamageMultiplier(Kind) *
            Station.GetModuleDamageMultiplier(StableModuleId);

        public static float GetBaseDamage(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => OrbitalPistolModule.BaseDamage,
            OrbitalModuleKind.Saw => OrbitalSawModule.BaseDamage,
            OrbitalModuleKind.Sniper => 32f,
            OrbitalModuleKind.Shotgun => 3.5f,
            _ => 0f
        };

        protected OrbitalModuleRuntime(OrbitalStationRuntime station,
            int stableModuleId, Color color, GameObject visualPrefab = null, OrbitalModuleKind? presentationKind = null)
        {
            Station = station;
            Combat = station.Combat;
            StableModuleId = stableModuleId;
            presentation = new OrbitalModuleVisual(station, presentationKind ?? Kind, GetType().Name, color, visualPrefab);
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

        // Mounted body contact, excluding hover halos, previews and a body being dragged.
        public bool HasBodyContact(Vector2 center, float radius) =>
            Mount != null && Visual != null && Visual.activeInHierarchy &&
            Visual.transform.parent == Mount.Transform && presentation.HasBodyContact(center, radius);


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
            if (Mount != null && Mount.Ring.Geometry.Type == OrbitalPathType.FigureEight)
                presentation.DepthOpacity = Mathf.Lerp(1f, .6f,
                    Mount.Ring.Geometry.BackAmount(Mount.Transform.localPosition));
            presentation.Tick();
        }

        public virtual void ActivateCombat() { }
        protected void TriggerPresentation() => presentation.Trigger();
        protected void ConfigureWeaponFeedback(Color color, bool preserveColors = false) => presentation.ConfigureWeaponFeedback(color, preserveColors);
        protected void WeaponRecoil(float strength) => presentation.WeaponRecoil(strength);
        protected void WeaponCharge(float amount) => presentation.WeaponCharge(amount);
        protected void ResetWeaponFeedback() => presentation.ResetWeaponFeedback();
        public void TriggerUpgradePresentation() => presentation.Trigger();

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

    public sealed class OrbitalPistolModule : OrbitalProjectileModule
    {
        public const float BaseDamage = 8f;
        public OrbitalPistolModule(OrbitalStationRuntime station, int id) :
            base(station, id, OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol)) { }
    }

    public sealed class OrbitalArcEmitterModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.ArcEmitter;
        public OrbitalArcEmitterModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, OrbitalRewardIconResolver.ModuleColor(OrbitalModuleKind.ArcEmitter)) { }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (deltaTime > 0f && Cooldown <= 0f)
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
                    OrbitalRewardIconResolver.ModuleColor(OrbitalModuleKind.ArcEmitter), 0.12f, OrbitalFlashFamily.Arc);
                Combat.ApplyDamage(target, (7f - i * 1.25f) * Power * Station.State.ShootingDamageMultiplier,
                    WorldPosition);
                from = target.transform.position;
            }
            if (targets.Count > 0)
            {
                AudioService.Instance?.PlayAt(AudioCueId.OrbitalArcFire, Visual.transform.position);
                Cooldown = 1.15f / Station.State.ShootingRateMultiplier;
            }
        }
    }

    public sealed class OrbitalLinkNodeModule : OrbitalModuleRuntime
    {
        public override OrbitalModuleKind Kind => OrbitalModuleKind.LinkNode;
        public OrbitalLinkNodeModule(OrbitalStationRuntime station, int stableModuleId) :
            base(station, stableModuleId, OrbitalRewardIconResolver.ModuleColor(OrbitalModuleKind.LinkNode)) { }
        public override void Tick(float deltaTime) => base.Tick(deltaTime);
    }
}
