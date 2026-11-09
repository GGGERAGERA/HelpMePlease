using Subject42.Combat.Telekinetic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    /// <summary>Module presentation is authored once on its visual prefab.</summary>
    public static class OrbitalRewardIconResolver
    {
        public readonly struct Icon
        {
            public readonly Sprite Sprite;
            public readonly Color Tint;
            public readonly Color ImageTint;
            public Icon(Sprite sprite, Color tint, bool usesOwnColors = false)
            {
                Sprite = sprite;
                Tint = tint;
                ImageTint = usesOwnColors ? Color.white : tint;
            }
        }

        public static Icon Resolve(OrbitalModuleKind kind)
        {
            var view = OrbitalPresentationConfig.Active.GetPrefab(kind).GetComponent<OrbitalModuleView>();
            return view.IconSource != null
                ? new Icon(view.IconSource.sprite, view.IconSource.color)
                : new Icon(view.Icon, view.IconTint, view.IconUsesOwnColors);
        }

        public static Color ModuleColor(OrbitalModuleKind kind) => Resolve(kind).Tint;

        public static Icon Resolve(TelekineticPart part) =>
            OrbitalPresentationConfig.Active.TelekineticRigPrefab.GetItemIcon(part);

        internal static Icon ResolveBunkerModule(OrbitalModuleKind kind) =>
            OrbitalRewardProvider.GetRigPart(kind) is TelekineticPart part ? Resolve(part) : Resolve(kind);

        public static Icon Resolve(SegmentedLinkKind kind) =>
            new(SegmentedModulePresentation.Icon(kind), SegmentedModulePresentation.Color(kind));

        public static Icon Resolve(OrbitalRewardKind kind) =>
            OrbitalRewardProvider.GetModuleKind(kind) is OrbitalModuleKind module
                ? Resolve(module) : kind switch
        {
            OrbitalRewardKind.DamageLink => Resolve(SegmentedLinkKind.Damage),
            OrbitalRewardKind.FireRateLink => Resolve(SegmentedLinkKind.FireRate),
            OrbitalRewardKind.ExtraShotLink => Resolve(SegmentedLinkKind.ExtraShot),
            OrbitalRewardKind.RicochetLink => Resolve(SegmentedLinkKind.Ricochet),
            OrbitalRewardKind.ModuleDamage => Resolve(OrbitalModuleKind.Pistol),
            OrbitalRewardKind.MaxHealth => new Icon(OrbitalPresentationConfig.Active.MaxHealthRewardIcon, Color.white),
            OrbitalRewardKind.MoveSpeed => new Icon(OrbitalPresentationConfig.Active.MoveSpeedRewardIcon, Color.white),
            OrbitalRewardKind.ShootingDamage => new Icon(OrbitalPresentationConfig.Active.ShootingDamageRewardIcon, Color.white),
            OrbitalRewardKind.ShootingRate => new Icon(OrbitalPresentationConfig.Active.ShootingRateRewardIcon, Color.white),
            OrbitalRewardKind.ContactDamage => new Icon(OrbitalPresentationConfig.Active.ContactDamageRewardIcon, Color.white),
            OrbitalRewardKind.ContactSize => new Icon(OrbitalPresentationConfig.Active.ContactSizeRewardIcon, Color.white),
            OrbitalRewardKind.AddMount => new Icon(OrbitalPresentationConfig.Active.NewMountIcon, Color.white),
            OrbitalRewardKind.RingCapacity => new Icon(OrbitalPresentationConfig.Active.RingCapacityIcon, Color.white),
            OrbitalRewardKind.RingPower => new Icon(OrbitalPresentationConfig.Active.RingDamageIcon, Color.white),
            OrbitalRewardKind.RingSpeed => new Icon(OrbitalPresentationConfig.Active.RingSpeedIcon, Color.white),
            OrbitalRewardKind.NewRing => new Icon(OrbitalPresentationConfig.Active.NewRingIcon, Color.white),
            _ => new Icon(null, Color.white)
        };

        public static Icon Resolve(OrbitalRewardData reward)
        {
            if (RunStateManager.Instance?.OrbitalStationState?.UsesTelekineticRig == true &&
                OrbitalRewardProvider.GetRigPart(reward.RewardKind) is TelekineticPart part)
                return Resolve(part);
            if (reward.RewardKind==OrbitalRewardKind.RigExpansion) return new Icon(OrbitalPresentationConfig.Active.NewMountIcon,Color.cyan);
            return Resolve(reward.RewardKind);
        }
    }
}
