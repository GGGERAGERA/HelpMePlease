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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (station.Combat is ProductionOrbitalCombatAdapter adapter && adapter.LabModuleFactory != null)
                module = adapter.LabModuleFactory(station, state);
            if (module != null) { error = null; return true; }
#endif
            module = state.ModuleType switch
            {
                OrbitalModuleKind.Pistol => new OrbitalPistolModule(station, state.StableModuleId),
                OrbitalModuleKind.Saw => new OrbitalSawModule(station, state.StableModuleId, OrbitalPresentationConfig.Active.GetWeaponSettings(state.ModuleType)),
                OrbitalModuleKind.Sniper or OrbitalModuleKind.Shotgun => new OrbitalProjectileModule(station, state.StableModuleId, OrbitalPresentationConfig.Active.GetWeaponSettings(state.ModuleType)),
                OrbitalModuleKind.ArcEmitter => new OrbitalArcEmitterModule(station, state.StableModuleId),
                OrbitalModuleKind.LinkNode => new OrbitalLinkNodeModule(station, state.StableModuleId),
                _ => null
            };
            error = module == null ? $"unsupported module kind '{state.ModuleType}'" : null;
            return module != null;
        }
    }
}
