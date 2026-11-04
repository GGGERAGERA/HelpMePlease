using System.Linq;
using Subject42.Combat.Telekinetic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalRunState
    {
        public TelekineticBuild Telekinetic;
        public bool UsesTelekineticRig => Telekinetic != null;
        public float TelekineticAngle;
        public bool TelekineticAutoRotate = true;

        internal void AwardRouteLevel(bool weapon)
        {
            if (Telekinetic == null) return;
            Telekinetic.AwardRouteLevel(weapon);
            Revision++;
        }

        public void EnableTelekineticRig()
        {
            if (Telekinetic != null)
            {
                if (Telekinetic.RemoveLegacyFrames()) Revision++;
                return;
            }
            EnableSegmentedOrbit();
            Telekinetic = new TelekineticBuild { AvailableRadius = Mathf.Clamp(UnlockedSegmentedLevels+1,2,TelekineticBuild.Radius) };
            TelekineticAngle = Rings.Count > 0 ? Rings[0].CurrentPhase : 0;
            // Import a previous in-memory run once. Preserve the old data as a rollback source.
            foreach (var gun in Modules.Concat(SegmentedWeapons))
            {
                var part = gun.ModuleType switch {
                    OrbitalModuleKind.Shotgun => TelekineticPart.Shotgun,
                    OrbitalModuleKind.Saw => TelekineticPart.Saw,
                    OrbitalModuleKind.Sniper => TelekineticPart.Sniper,
                    _ => TelekineticPart.Pistol };
                var item = Telekinetic.Grant(part);
                item.Level = 1 + gun.DamageLevel;
                // Keep initial / already installed weapons usable; surplus stays in reserve.
                if (Modules.Contains(gun))
                    for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
                        if (Telekinetic.Reserve.Contains(item)) Telekinetic.Install(item, new Vector2Int(x,y));
            }
            foreach (var link in SegmentedLinks.Select(l => l.Kind).Concat(SegmentedLinkStock))
                Telekinetic.Grant(link switch {
                    SegmentedLinkKind.Damage => TelekineticPart.DMG,
                    SegmentedLinkKind.FireRate => TelekineticPart.FR,
                    SegmentedLinkKind.ExtraShot => TelekineticPart.Double,
                    _ => TelekineticPart.RIC });
            Revision++;
        }
    }
}
