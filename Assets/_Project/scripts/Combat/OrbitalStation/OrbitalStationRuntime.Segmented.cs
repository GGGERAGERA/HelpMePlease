using System.Linq;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalStationRuntime
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugSetInertialSawExperiment(bool enabled)
        {
            if (!IsInitialized || !UsesSegmentedOrbit || Owner.IsDead || IsRunEnded ||
                runStateManager == null || State.InertialSawExperiment == enabled ||
                enabled && State.PulsedSawExperiment) return false;
            if (enabled) runStateManager.DebugMarkDevelopmentRun();
            State.SetInertialSawExperiment(enabled);
            foreach (var module in modules)
                if (module is OrbitalSawModule saw) saw.ResetInertialPhaseSample();
            return true;
        }

        public bool DebugSetMissReturnPistolExperiment(bool enabled)
        {
            if (!IsInitialized || !UsesSegmentedOrbit || Owner.IsDead || IsRunEnded ||
                runStateManager == null || State.MissReturnPistolExperiment == enabled) return false;
            if (enabled) runStateManager.DebugMarkDevelopmentRun();
            State.SetMissReturnPistolExperiment(enabled);
            foreach (var module in modules)
                if (module is OrbitalProjectileModule projectile && projectile.Kind == OrbitalModuleKind.Pistol)
                    projectile.RefreshMissReturnPistolExperiment();
            return true;
        }

        public bool DebugSetPulsedSawExperiment(bool enabled)
        {
            if (!IsInitialized || !UsesSegmentedOrbit || Owner.IsDead || IsRunEnded ||
                runStateManager == null || State.PulsedSawExperiment == enabled ||
                enabled && State.InertialSawExperiment) return false;
            if (enabled) runStateManager.DebugMarkDevelopmentRun();
            State.SetPulsedSawExperiment(enabled);
            foreach (var module in modules)
                if (module is OrbitalSawModule saw) saw.ResetPulsePhase();
            return true;
        }

        public bool DebugSetChargedLineSniperExperiment(bool enabled)
        {
            if (!IsInitialized || !UsesSegmentedOrbit || Owner.IsDead || IsRunEnded ||
                runStateManager == null || State.ChargedLineSniperExperiment == enabled) return false;
            // Mark before changing combat: disabling the experiment cannot restore progression eligibility.
            if (enabled) runStateManager.DebugMarkDevelopmentRun();
            State.SetChargedLineSniperExperiment(enabled);
            foreach (var module in modules)
                if (module is OrbitalProjectileModule projectile && projectile.Kind == OrbitalModuleKind.Sniper)
                    projectile.RefreshChargedLineSniperExperiment();
            return true;
        }
#endif
        public bool ConstructWeapon(OrbitalModuleKind kind, int ringId, int mount, out string error)
        {
            error = "Откройте орбитальный конструктор.";
            if (!InputOwner.CanEditConstructor || !State.TryInstallSegmentedWeapon(kind, ringId, mount, out var gun, out error)) return false;
            SyncCommitted("ConstructWeapon", ringId, gun.StableModuleId, () =>
            {
                if (!InstallModulePresentation(RequireMount(ringId, mount), gun))
                    throw new System.InvalidOperationException("constructor weapon attach failed");
            });
            return IsInitialized;
        }

        public bool StowWeapon(int id)
        {
            if (!InputOwner.CanEditConstructor || !State.TryStowSegmentedWeapon(id)) return false;
            SyncCommitted("StowWeapon", 0, id, () => RemoveModulePresentation(RequireModule(id)));
            return IsInitialized;
        }

        public bool RepositionWeapon(int id, int ringId, int mount, out string error)
        {
            return TransferConstructorWeapon(id, false, ringId, mount, out error);
        }

        public bool TransferConstructorWeapon(int id, bool fromStock, int ringId, int mount, out string error)
        {
            error = "Откройте орбитальный конструктор.";
            if (!InputOwner.CanEditConstructor || !State.CanTransferSegmentedWeapon(id, fromStock, ringId, mount, out error)) return false;
            var source = fromStock ? null : modules.FirstOrDefault(gun => gun.StableModuleId == id);
            var destination = RequireMount(ringId, mount);
            var displaced = destination.Module;
            var originalMount = source?.CurrentMount;
            if (!State.TryTransferSegmentedWeapon(id, fromStock, ringId, mount, out error)) return false;
            SyncCommitted("ConstructorTransfer", ringId, id, () =>
            {
                if (fromStock)
                {
                    if (displaced != null) RemoveModulePresentation(displaced);
                    if (!InstallModulePresentation(destination, State.FindModule(id)))
                        throw new System.InvalidOperationException("constructor weapon attach failed");
                }
                else
                {
                    originalMount.Detach();
                    displaced?.CurrentMount.Detach();
                    if (!destination.Attach(source) || displaced != null && !originalMount.Attach(displaced))
                        throw new System.InvalidOperationException("constructor weapon swap attach failed");
                    source.CancelPresentationDrag();
                    displaced?.CancelPresentationDrag();
                }
            });
            return IsInitialized;
        }

        public bool ConstructModule(SegmentedLinkKind kind, int ringId, int mount, out string error)
        {
            error = "Откройте орбитальный конструктор.";
            return InputOwner.CanEditConstructor && State.TryInstallSegmentedModule(kind, ringId, mount, out error);
        }

        public bool RepositionModule(int index, int ringId, int mount, out string error)
        {
            error = "Откройте орбитальный конструктор.";
            return InputOwner.CanEditConstructor && State.TryMoveSegmentedModule(index, ringId, mount, out error);
        }

        public bool StowModule(int index) => InputOwner.CanEditConstructor && State.TryDisconnectSegmented(index);

        public float SegmentedDamageFactor(int id) => OrbitModulesSuppressed ? 1f : 1f + .2f * State.SegmentedBonusCount(id, SegmentedLinkKind.Damage);
        public float SegmentedRateFactor(int id) => OrbitModulesSuppressed ? 1f : 1f + .2f * State.SegmentedBonusCount(id, SegmentedLinkKind.FireRate);
        public int SegmentedRicochets(int id) => OrbitModulesSuppressed ? 0 : State.SegmentedBonusCount(id, SegmentedLinkKind.Ricochet);

        internal void SegmentedOrdinaryShot(int id)
        {
            if (!UsesSegmentedOrbit || OrbitModulesSuppressed) return;
            var weapon = modules.FirstOrDefault(gun => gun.StableModuleId == id) as OrbitalProjectileModule;
            if (weapon == null) return;
            foreach (var link in State.ActiveSegmentedLinks())
            {
                if (link.Kind != SegmentedLinkKind.ExtraShot ||
                    !State.SegmentedModuleTargets(link).Any(gun => gun.StableModuleId == id) ||
                    link.NextExtraShotByWeapon.TryGetValue(id, out float nextShot) && UnityEngine.Time.time < nextShot) continue;
                if (weapon.FireExtraShot()) link.NextExtraShotByWeapon[id] = UnityEngine.Time.time + .5f;
            }
        }
    }
}
