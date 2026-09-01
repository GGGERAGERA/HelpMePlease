using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalPrimitiveFactory
    {
        private Texture2D pixel;
        private Sprite square;
        private Sprite circle;
        private Material lineMaterial;

        public Sprite Square => square;
        public Sprite Circle => circle;
        public Material LineMaterial => lineMaterial;

        public OrbitalPrimitiveFactory()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "OrbitalLabPixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply(false, true);
            square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
            square.name = "OrbitalLabSquare";
            square.hideFlags = HideFlags.DontSave;

            const int size = 64;
            Texture2D circleTexture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "OrbitalLabCircleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = new((size - 1) * .5f, (size - 1) * .5f);
            float radius = size * .48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + .75f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            circleTexture.SetPixels(pixels);
            circleTexture.Apply(false, true);
            circle = Sprite.Create(circleTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            circle.name = "OrbitalLabCircle";
            circle.hideFlags = HideFlags.DontSave;

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            lineMaterial = new Material(shader)
            {
                name = "OrbitalLabLineMaterial",
                hideFlags = HideFlags.DontSave
            };
        }

        public SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite,
            Color color, Vector2 size, int order)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public LineRenderer CreateCircleLine(string name, Transform parent, int order = 0,
            int segments = 96)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            line.material = lineMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sortingOrder = order;
            return line;
        }

        public static void SetCircle(LineRenderer line, Vector2 center, float radius)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius, 0f));
            }
        }

        public void Dispose()
        {
            if (lineMaterial != null) Object.Destroy(lineMaterial);
            if (square != null) Object.Destroy(square);
            Texture2D circleTexture = circle != null ? circle.texture : null;
            if (circle != null) Object.Destroy(circle);
            if (circleTexture != null) Object.Destroy(circleTexture);
            if (pixel != null) Object.Destroy(pixel);
        }
    }
}
