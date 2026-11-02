using System;
using Subject42.Combat.Telekinetic;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalRewardProvider
    {
        public static TelekineticPart? GetRigPart(OrbitalRewardKind kind) => kind switch
        {
            OrbitalRewardKind.Pistol=>TelekineticPart.Pistol, OrbitalRewardKind.Shotgun=>TelekineticPart.Shotgun,
            OrbitalRewardKind.Saw=>TelekineticPart.Saw, OrbitalRewardKind.Sniper=>TelekineticPart.Sniper,
            OrbitalRewardKind.DamageLink=>TelekineticPart.DMG, OrbitalRewardKind.FireRateLink=>TelekineticPart.FR,
            OrbitalRewardKind.ExtraShotLink=>TelekineticPart.Double, OrbitalRewardKind.RicochetLink=>TelekineticPart.RIC,
            OrbitalRewardKind.RigFrame=>TelekineticPart.Frame, OrbitalRewardKind.RigEnergy=>TelekineticPart.Energy,
            OrbitalRewardKind.RigPierce=>TelekineticPart.Pierce, OrbitalRewardKind.RigSplash=>TelekineticPart.Splash,
            OrbitalRewardKind.RigChain=>TelekineticPart.Chain, OrbitalRewardKind.RigCrit=>TelekineticPart.Crit,
            OrbitalRewardKind.RigSlow=>TelekineticPart.Slow, OrbitalRewardKind.RigShred=>TelekineticPart.Shred,
            OrbitalRewardKind.RigKnock=>TelekineticPart.Knock, OrbitalRewardKind.RigEcho=>TelekineticPart.Echo,
            _=>null
        };
        private static bool RigSupported(OrbitalRewardKind kind) => GetRigPart(kind).HasValue || kind is
            OrbitalRewardKind.RigExpansion or OrbitalRewardKind.MaxHealth or OrbitalRewardKind.MoveSpeed or
            OrbitalRewardKind.ShootingDamage or OrbitalRewardKind.ShootingRate or OrbitalRewardKind.ContactDamage or OrbitalRewardKind.ContactSize;
        private void CreateRigDefinitions()
        {
            Add(OrbitalRewardKind.RigExpansion,"Расширение конструкции","Больше пространства для целых предметов.",config.RingWeight,false);
            foreach (OrbitalRewardKind kind in Enum.GetValues(typeof(OrbitalRewardKind)))
            {
                if (kind < OrbitalRewardKind.RigEnergy || GetRigPart(kind) is not TelekineticPart part) continue;
                Add(kind,TelekineticBuild.Label(part),"Один предмет в запас. Разместите его в конструкторе; эффекты передаются по общей стороне.",config.ModuleWeight,false);
            }
        }
    }
}
