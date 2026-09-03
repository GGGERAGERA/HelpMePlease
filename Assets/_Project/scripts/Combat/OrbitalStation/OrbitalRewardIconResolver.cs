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
        private static readonly List<Sprite> ownedSprites = new();
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
            // These are the production Station primitives: circle core/mount,
            // Arc circle + white square, Link nodes + pink circular centers.
            Sprite circle = OrbitalStationRuntime.CreateCircleSprite();
            ownedSprites.Add(circle);
            fallback = new Icon(circle, new Color(0.72f, 0.25f, 1f));
            icons[OrbitalRewardKind.CoreUpgrade] = fallback;
            icons[OrbitalRewardKind.LinkMatrix] = fallback;
            icons[OrbitalRewardKind.MaxHealth] = fallback;
            icons[OrbitalRewardKind.MoveSpeed] = fallback;
            icons[OrbitalRewardKind.AddMount] =
                new Icon(circle, new Color(0.72f, 0.8f, 0.85f));
            icons[OrbitalRewardKind.ArcEmitter] =
                new Icon(CreateStationIcon(OrbitalRewardKind.ArcEmitter), Color.white);
            icons[OrbitalRewardKind.LinkPair] =
                new Icon(CreateStationIcon(OrbitalRewardKind.LinkPair), Color.white);
            Icon ring = new(CreateStationIcon(OrbitalRewardKind.RingSpeed),
                Color.HSVToRGB(0.51f, 0.72f, 1f));
            icons[OrbitalRewardKind.RingSpeed] = ring;
            icons[OrbitalRewardKind.RingPower] = ring;

            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            icons[OrbitalRewardKind.Pistol] = FromPrefab(config.PistolPrefab);
            icons[OrbitalRewardKind.LaserSword] = FromPrefab(config.LaserSwordPrefab);
            icons[OrbitalRewardKind.ImpulseGun] = FromPrefab(config.ImpulseGunPrefab);
            icons[OrbitalRewardKind.ModuleDamage] = icons[OrbitalRewardKind.Pistol];
        }

        private static Icon FromPrefab(GameObject prefab)
        {
            if (prefab == null)
                return fallback;

            // P1 is the main body in all three production miniWeapons. Do not
            // select the first renderer: it can be a barrel, button or blade.
            foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name.EndsWith("P1", System.StringComparison.Ordinal) &&
                    renderer.sprite != null && renderer.color.a > 0f)
                    return new Icon(renderer.sprite, renderer.color);
            return fallback;
        }

        private static Sprite CreateStationIcon(OrbitalRewardKind kind)
        {
            const int size = 128;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) / size - Vector2.one * 0.5f;
                    Color color;
                    if (kind == OrbitalRewardKind.ArcEmitter)
                    {
                        color = new Color(0.82f, 0.66f, 1f,
                            Coverage(p.magnitude, 0.48f, size));
                        if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) <= 0.17f)
                            color = Color.white;
                    }
                    else if (kind == OrbitalRewardKind.LinkPair)
                    {
                        float distance = Mathf.Min((p - new Vector2(-0.27f, 0f)).magnitude,
                            (p - new Vector2(0.27f, 0f)).magnitude);
                        color = new Color(0.85f, 0.25f, 1f,
                            Coverage(distance, 0.21f, size));
                        if (Mathf.Abs(p.x) < 0.27f && Mathf.Abs(p.y) < 0.025f)
                            color.a = 1f;
                        float center = Coverage(distance, 0.21f * 0.46f, size);
                        color = Color.Lerp(color, new Color(1f, 0.72f, 1f), center);
                    }
                    else
                    {
                        // The production ring is a circular LineRenderer.
                        color = new Color(1f, 1f, 1f,
                            Coverage(Mathf.Abs(p.magnitude - 0.43f), 0.025f, size));
                    }
                    pixels[y * size + x] = color;
                }

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Orbital Reward {kind}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                Vector2.one * 0.5f, size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ownedSprites.Add(sprite);
            return sprite;
        }

        private static float Coverage(float distance, float radius, int size) =>
            Mathf.Clamp01((radius - distance) * size + 0.75f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            // Also handles Enter Play Mode with domain reload disabled.
            foreach (Sprite sprite in ownedSprites)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
            ownedSprites.Clear();
            icons = null;
            fallback = default;
        }
    }
}
