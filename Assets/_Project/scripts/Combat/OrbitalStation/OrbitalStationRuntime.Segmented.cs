using System.Linq;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalStationRuntime
    {
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
            error = "Откройте орбитальный конструктор.";
            return InputOwner.CanEditConstructor && MoveModule(id, ringId, mount, out error);
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

        public float SegmentedDamageFactor(int id) => 1f + .2f * State.SegmentedBonusCount(id, SegmentedLinkKind.Damage);
        public float SegmentedRateFactor(int id) => 1f + .2f * State.SegmentedBonusCount(id, SegmentedLinkKind.FireRate);
        public int SegmentedRicochets(int id) => State.SegmentedBonusCount(id, SegmentedLinkKind.Ricochet);

        internal void SegmentedOrdinaryShot(int id)
        {
            if (!UsesSegmentedOrbit) return;
            foreach (var link in State.ActiveSegmentedLinks())
            {
                if (link.Kind != SegmentedLinkKind.ExtraShot || UnityEngine.Time.time < link.NextExtraShot) continue;
                var targets = State.SegmentedModuleTargets(link).ToArray();
                if (!targets.Any(gun => gun.StableModuleId == id)) continue;
                int targetId = targets.FirstOrDefault(gun => gun.StableModuleId != id)?.StableModuleId ?? id;
                var neighbour = modules.FirstOrDefault(gun => gun.StableModuleId == targetId) as OrbitalProjectileModule;
                if (neighbour == null) continue;
                link.NextExtraShot = UnityEngine.Time.time + .5f;
                neighbour.FireExtraShot();
            }
        }
    }
}
