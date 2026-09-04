using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    /// <summary>Session-cached presentation only; never instantiates weapon prefabs.</summary>
    public static class OrbitalRewardIconResolver
    {
        public readonly struct Icon
        {
            public readonly Sprite Sprite;
            public readonly Color Tint;

            public Icon(Sprite sprite, Color tint)
            {
                Sprite = sprite;
                Tint = tint;
            }
        }

        private static Dictionary<OrbitalRewardKind, Icon> icons;
        private static Icon fallback;

        public static Icon Resolve(OrbitalRewardKind kind)
        {
            EnsureInitialized();
            return icons.TryGetValue(kind, out Icon icon) && icon.Sprite != null
                ? icon : fallback;
        }

        public static Icon Resolve(OrbitalRewardData reward)
        {
            Icon icon = Resolve(reward.RewardKind);
            // Read the existing subject icon without modifying the legacy asset.
            if ((reward.RewardKind == OrbitalRewardKind.MaxHealth ||
                 reward.RewardKind == OrbitalRewardKind.MoveSpeed) &&
                reward.BodyUpgrade != null && reward.BodyUpgrade.icon != null)
                return new Icon(reward.BodyUpgrade.icon, Color.white);
            return icon;
        }

        private static void EnsureInitialized()
        {
            if (icons != null && fallback.Sprite != null)
                return;

            icons = new Dictionary<OrbitalRewardKind, Icon>();
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            if (config == null) return;
            Sprite circle = config.CircleSprite;
            fallback = new Icon(circle, new Color(0.72f, 0.25f, 1f));
            icons[OrbitalRewardKind.CoreUpgrade] = fallback;
            icons[OrbitalRewardKind.LinkMatrix] = fallback;
            icons[OrbitalRewardKind.MaxHealth] = fallback;
            icons[OrbitalRewardKind.MoveSpeed] = fallback;
            icons[OrbitalRewardKind.AddMount] =
                new Icon(circle, new Color(0.72f, 0.8f, 0.85f));
            Icon ring = new(config.RingIcon, Color.HSVToRGB(0.51f, 0.72f, 1f));
            icons[OrbitalRewardKind.RingSpeed] = ring;
            icons[OrbitalRewardKind.RingPower] = ring;
            foreach (OrbitalModuleKind module in System.Enum.GetValues(typeof(OrbitalModuleKind)))
            {
                var prefab = config.GetPrefab(module);
                if (prefab == null || !prefab.TryGetComponent<OrbitalModuleView>(out var view)) continue;
                OrbitalRewardKind reward = module == OrbitalModuleKind.LinkNode ? OrbitalRewardKind.LinkPair :
                    (OrbitalRewardKind)System.Enum.Parse(typeof(OrbitalRewardKind), module.ToString());
                icons[reward] = new Icon(view.Icon, view.IconTint);
            }
            icons[OrbitalRewardKind.ModuleDamage] = icons[OrbitalRewardKind.Pistol];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            icons = null;
            fallback = default;
        }
    }
}
