#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Subject42.Combat.OrbitalStation;
namespace Subject42.Combat.OrbitalStation
{
    // Lab presentation callback only; combat and category state belong to the production runtime.
    public sealed class EnemyOrbitalLabWeapon : OrbitalProjectileModule
    {
        public EnemyOrbitalLabWeapon(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings,
            Action<float> feedback, EnemyOrbitalLabController lab = null) : base(station, id, settings, feedback) { }
    }
}
#endif
