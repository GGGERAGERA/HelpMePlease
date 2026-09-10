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
            return new Icon(view.Icon, view.IconTint, view.IconUsesOwnColors);
        }

        public static Color ModuleColor(OrbitalModuleKind kind) => Resolve(kind).Tint;

        public static Icon Resolve(OrbitalRewardKind kind) => kind switch
        {
            OrbitalRewardKind.Pistol => Resolve(OrbitalModuleKind.Pistol),
            OrbitalRewardKind.LaserSword => Resolve(OrbitalModuleKind.LaserSword),
            OrbitalRewardKind.ImpulseGun => Resolve(OrbitalModuleKind.ImpulseGun),
            OrbitalRewardKind.ArcEmitter => Resolve(OrbitalModuleKind.ArcEmitter),
            OrbitalRewardKind.LinkPair => Resolve(OrbitalModuleKind.LinkNode),
            OrbitalRewardKind.ModuleDamage => Resolve(OrbitalModuleKind.Pistol),
            OrbitalRewardKind.CoreUpgrade => new Icon(OrbitalPresentationConfig.Active.CoreIcon, Color.white),
            OrbitalRewardKind.AddMount => new Icon(OrbitalPresentationConfig.Active.NewMountIcon, Color.white),
            OrbitalRewardKind.RingCapacity => new Icon(OrbitalPresentationConfig.Active.RingCapacityIcon, Color.white),
            OrbitalRewardKind.RingPower => new Icon(OrbitalPresentationConfig.Active.RingDamageIcon, Color.white),
            OrbitalRewardKind.RingSpeed => new Icon(OrbitalPresentationConfig.Active.RingSpeedIcon, Color.white),
            OrbitalRewardKind.NewRing => new Icon(OrbitalPresentationConfig.Active.NewRingIcon, Color.white),
            // ART REQUIRED: no unrelated circle/ring placeholders in production cards.
            _ => new Icon(null, Color.white)
        };

        public static Icon Resolve(OrbitalRewardData reward)
        {
            if ((reward.RewardKind == OrbitalRewardKind.MaxHealth || reward.RewardKind == OrbitalRewardKind.MoveSpeed) &&
                reward.BodyUpgrade != null)
                return new Icon(reward.BodyUpgrade.icon, Color.white);
            return Resolve(reward.RewardKind);
        }
    }
}
