namespace Subject42.Combat.OrbitalStation
{
    public static class OrbitalModuleRuntimeFactory
    {
        public static bool TryCreate(OrbitalStationRuntime station,
            OrbitalModuleState state,
            out OrbitalModuleRuntime module,
            out string error)
        {
            module = null;
            if (station == null)
            {
                error = "station is missing";
                return false;
            }
            if (state == null)
            {
                error = "module state is missing";
                return false;
            }

            module = state.ModuleType switch
            {
                OrbitalModuleKind.Pistol => new OrbitalPistolModule(station, state.StableModuleId),
                OrbitalModuleKind.LaserSword => new OrbitalLaserSwordModule(station, state.StableModuleId),
                OrbitalModuleKind.ImpulseGun => new OrbitalImpulseGunModule(station, state.StableModuleId),
                OrbitalModuleKind.ArcEmitter => new OrbitalArcEmitterModule(station, state.StableModuleId),
                OrbitalModuleKind.LinkNode => new OrbitalLinkNodeModule(station, state.StableModuleId),
                _ => null
            };
            error = module == null ? $"unsupported module kind '{state.ModuleType}'" : null;
            return module != null;
        }
    }
}
