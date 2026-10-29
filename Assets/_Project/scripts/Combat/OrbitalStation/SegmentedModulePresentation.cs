using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    // Small code-authored pixel glyphs shared by cards, constructor and world nodes.
    public static class SegmentedModulePresentation
    {
        public const float WorldSize = .36f;
        public static readonly Color LinkColor = new(.25f, .78f, 1f);
        private static readonly Sprite[] icons = new Sprite[4];
        private static readonly string[][] masks =
        {
            new[] { ".....##.....", "..#..##..#..", "...######...", "..########..", "...######...", "############", "############", "...######...", "..########..", "...######...", "..#..##..#..", ".....##....." },
            new[] { "......####..", ".....####...", "....####....", "...####.....", "..########..", ".########...", "....####....", "...####.....", "..####......", ".####.......", "####........", "##.........." },
            new[] { "............", ".##....##...", ".###...###..", "..###...###.", "...###...###", "....###...##", "....###...##", "...###...###", "..###...###.", ".###...###..", ".##....##...", "............" },
            new[] { ".......##...", ".......###..", "..#########.", "..##########", "..##...####.", "..##...###..", "..##...##...", "..##........", "..######....", "..######....", "............", "............" }
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIcons()
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                Object.Destroy(icons[i].texture); Object.Destroy(icons[i]); icons[i] = null;
            }
        }

        public static Color Color(SegmentedLinkKind kind) => kind switch
        {
            SegmentedLinkKind.Damage => new Color(1f, .4f, .36f),
            SegmentedLinkKind.FireRate => new Color(1f, .73f, .24f),
            SegmentedLinkKind.ExtraShot => new Color(.2f, .55f, 1f),
            _ => new Color(.15f, .9f, .8f)
        };

        public static Sprite Icon(SegmentedLinkKind kind)
        {
            int index = (int)kind;
            if (icons[index] != null) return icons[index];
            const int side = 16;
            var pixels = new Color32[side * side];
            var mask = masks[index];
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    if (mask[y][x] == '#')
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                pixels[(13 - y + dy) * side + x + 2 + dx] = new Color32(7, 15, 22, 255);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    if (mask[y][x] == '#') pixels[(13 - y) * side + x + 2] = new Color32(255, 255, 255, 255);
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                name = "Segmented " + kind + " glyph", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            icons[index] = Sprite.Create(texture, new Rect(0, 0, side, side), Vector2.one * .5f, side);
            icons[index].name = texture.name;
            icons[index].hideFlags = HideFlags.HideAndDontSave;
            return icons[index];
        }
    }
}
