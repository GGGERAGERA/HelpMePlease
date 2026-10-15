using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public class OrbitalSawModule : OrbitalModuleRuntime
    {
        public const float BaseDamage = 13f;
        public const float ContactInterval = .32f;
        public override OrbitalModuleKind Kind => OrbitalModuleKind.Saw;
        public readonly OrbitalWeaponSettings Settings;
        
        private readonly List<Collider2D> overlaps = new();
        private readonly HashSet<EnemyHealth> targets = new();
        private readonly Dictionary<EnemyHealth, float> nextHit = new();
        private ContactFilter2D filter = new() { useTriggers = true };
        public float ContactRadius => Settings.ContactRadius * Station.State.ContactSizeMultiplier;
        public float DamageMultiplier => Station.State.ContactDamageMultiplier;
        public int BodyHits { get; private set; }
        public OrbitalSawModule(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings) : base(station, id, settings.Color, settings.WeaponVisual)
        { Settings = settings; RefreshSize(); }

        public void RefreshSize() => Visual.transform.localScale = Vector3.one * (ContactRadius / .65f);
        public override void UpdateVisualRotation(float radians) { }
        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (Mount == null || Visual.transform.parent != Mount.Transform || Station.Owner.IsDead || deltaTime <= 0f) return;
            RefreshSize();
            Visual.transform.Rotate(0f, 0f, Settings.SpinDegreesPerSecond * deltaTime, Space.Self);
            ActivateCombat();
        }
        public override void ActivateCombat()
        {
            if (Mount == null || Visual.transform.parent != Mount.Transform || Station.Owner.IsDead || !Visual.activeInHierarchy) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Optional lab geometry can own a swept contact; never resolve the same contact twice.
            if (ProductionOrbitalCombatAdapter.LabInterceptor?.HandlesContact(this) == true) return;
#endif
            overlaps.Clear(); targets.Clear();
            Physics2D.OverlapCircle(WorldPosition, ContactRadius, filter, overlaps);
            foreach (var hit in overlaps)
            {
                var enemy = hit.GetComponentInParent<EnemyHealth>();
                if (enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead && enemy.gameObject.scene == Visual.scene)
                    targets.Add(enemy);
            }
            foreach (var enemy in targets)
            {
                if (nextHit.TryGetValue(enemy, out float until) && Time.time < until) continue;
                if (enemy.TryGetComponent<EnemyDirectionalShield>(out var shield) &&
                    shield.BlockDiskContact(WorldPosition, ContactRadius))
                { nextHit[enemy] = Time.time + Settings.ContactHitInterval; continue; }
                foreach (var hit in overlaps)
                {
                    if (hit.GetComponentInParent<EnemyHealth>() != enemy || shield != null && hit == shield.ShieldCollider) continue;
                    if (Combat.ApplyDamage(enemy, Settings.Damage * Power * DamageMultiplier, hit.ClosestPoint(WorldPosition)))
                    {
                        BodyHits++; nextHit[enemy] = Time.time + Mathf.Max(.05f, Settings.ContactHitInterval);
                        AudioService.Instance?.PlayAt(Settings.ShotSound, WorldPosition); TriggerPresentation();
                    }
                    break;
                }
            }
        }
        public override void OnCorePulse() => ActivateCombat();
        public override void Detach() { ResetContact(); base.Detach(); }
        public void ResetContact() { nextHit.Clear(); targets.Clear(); overlaps.Clear(); }
    }
}
