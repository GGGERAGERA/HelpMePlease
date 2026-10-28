using System.Linq;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalStationRuntime
    {
        public bool ConstructWeapon(OrbitalModuleKind kind, int ringId, int mount, out string error)
        {
            error = "Конструктор доступен только при выборе награды.";
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
            error = "Конструктор доступен только при выборе награды.";
            return InputOwner.CanEditConstructor && MoveModule(id, ringId, mount, out error);
        }

        public bool ConnectWeapons(int first, int second, SegmentedLinkKind kind, out string error)
        {
            error = "Конструктор доступен только при выборе награды.";
            return InputOwner.CanEditConstructor && State.TryConnectSegmented(first, second, kind, out error);
        }

        public bool DisconnectWeapons(int index) => InputOwner.CanEditConstructor && State.TryDisconnectSegmented(index);

        public float SegmentedDamageFactor(int id) => 1f + .2f * State.SegmentedBonusCount(id, SegmentedLinkKind.Damage);
        public float SegmentedRateFactor(int id) => 1f + .2f * State.SegmentedBonusCount(id, SegmentedLinkKind.FireRate);
        public int SegmentedRicochets(int id) => State.SegmentedBonusCount(id, SegmentedLinkKind.Ricochet);

        internal void SegmentedOrdinaryShot(int id)
        {
            if (!UsesSegmentedOrbit) return;
            foreach (var link in State.ActiveSegmentedLinks())
            {
                if (link.Kind != SegmentedLinkKind.ExtraShot || !link.Touches(id) || UnityEngine.Time.time < link.NextExtraShot) continue;
                var neighbour = modules.FirstOrDefault(gun => gun.StableModuleId == link.Other(id)) as OrbitalProjectileModule;
                if (neighbour == null) continue;
                link.NextExtraShot = UnityEngine.Time.time + .5f;
                neighbour.FireExtraShot();
            }
        }
    }
}
