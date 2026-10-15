#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Subject42.Combat.OrbitalStation;
namespace Subject42.Combat.OrbitalStation
{
    public sealed class EnemyOrbitalLabSaw : OrbitalSawModule
    {
        public EnemyOrbitalLabSaw(OrbitalStationRuntime station, int id, OrbitalWeaponSettings settings,
            EnemyOrbitalLabController lab) : base(station, id, settings) { }
    }
}
#endif
