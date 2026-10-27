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
            (lab.FixedDirection || lab.WeaponModules.HasModule(StableModuleId, "MULTISHOT"));
        protected override float? LabExtraShotAngle => lab != null && lab.isActiveAndEnabled
            ? lab.WeaponModules.ExtraShotAngle(StableModuleId) : null;
        public EnemyOrbitalLabWeapon(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings,
            Action<float> feedback, EnemyOrbitalLabController lab = null) : base(station, id,
                lab != null ? lab.WeaponModules.CreateProfile(id, settings) : OrbitalCombatV2WeaponModules.CopyProfile(settings), feedback)
        { this.lab = lab; SourceProfile = settings; }
    }
}
#endif
