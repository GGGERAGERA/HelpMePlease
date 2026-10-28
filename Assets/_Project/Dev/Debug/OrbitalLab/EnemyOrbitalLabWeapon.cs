#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Subject42.Combat.OrbitalStation;
namespace Subject42.Combat.OrbitalStation
{
    // Instance-only lab settings/direction; the shared runtime owns firing and collision.
    public sealed class EnemyOrbitalLabWeapon : OrbitalProjectileModule
    {
        private readonly EnemyOrbitalLabController lab;
        public OrbitalWeaponSettings SourceProfile { get; }
        protected override bool LabFixedDirection => lab != null && lab.isActiveAndEnabled &&
            (lab.SegmentedOrbit || lab.FixedDirection || lab.WeaponModules.HasModule(StableModuleId, "MULTISHOT"));
        protected override float? LabExtraShotAngle => lab != null && lab.isActiveAndEnabled
            ? lab.WeaponModules.ExtraShotAngle(StableModuleId) : null;
        protected override float LabDamageFactor => lab?.Segmented?.DamageFactor(StableModuleId) ?? 1f;
        protected override float LabRateFactor => lab?.Segmented?.RateFactor(StableModuleId) ?? 1f;
        protected override int LabRicochets => lab?.Segmented?.Ricochets(StableModuleId) ?? 0;
        protected override void LabOrdinaryShot() => lab?.Segmented?.OnOrdinaryShot(StableModuleId);
        public EnemyOrbitalLabWeapon(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings,
            Action<float> feedback, EnemyOrbitalLabController lab = null) : base(station, id,
                lab != null ? lab.WeaponModules.CreateProfile(id, settings) : OrbitalCombatV2WeaponModules.CopyProfile(settings), feedback)
        { this.lab = lab; SourceProfile = settings; }
    }
}
#endif
