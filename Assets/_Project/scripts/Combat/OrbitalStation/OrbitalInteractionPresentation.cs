using UnityEngine;
using UnityEngine.UI;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalCursorState
    {
        Normal,
        Grabbable,
        Dragging,
        ValidDrop,
        InvalidDrop
    }

    [DisallowMultipleComponent]
    public sealed class OrbitalInteractionPresentation : MonoBehaviour
    {
        private RectTransform cursorRect;
        private Image cursorImage;
        private Sprite originalSprite;
        private Color originalColor;
        private Vector2 originalSize;
        private bool originalPreserveAspect;
        private Sprite[] cursorSprites;
        private Texture2D[] cursorTextures;
        private string hintTitle;
        private string hintBody;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle panelStyle;
        private Texture2D panelTexture;
        private bool bound;

        public void Bind(OrbitalStationRuntime station)
        {
            if (bound)
                Release();
            UICrosshairFollowMouse crosshair =
                FindFirstObjectByType<UICrosshairFollowMouse>();
            if (crosshair == null)
                return;
            cursorRect = crosshair.GetComponent<RectTransform>();
            cursorImage = crosshair.GetComponent<Image>();
            if (cursorRect == null || cursorImage == null)
                return;
            originalSprite = cursorImage.sprite;
            originalColor = cursorImage.color;
            originalSize = cursorRect.sizeDelta;
            originalPreserveAspect = cursorImage.preserveAspect;
            BuildCursorSprites();
            cursorRect.sizeDelta = new Vector2(30f, 30f);
            cursorImage.preserveAspect = true;
            bound = true;
            SetCursor(OrbitalCursorState.Normal);
        }

        public void SetCursor(OrbitalCursorState state)
        {
            if (!bound || cursorImage == null || cursorSprites == null)
                return;
            int index = Mathf.Clamp((int)state, 0, cursorSprites.Length - 1);
            cursorImage.sprite = cursorSprites[index];
            cursorImage.color = Color.white;
            cursorRect.sizeDelta = state == OrbitalCursorState.Dragging
                ? new Vector2(34f, 34f)
                : new Vector2(30f, 30f);
        }

        public void ShowHint(string title, string body)
        {
            hintTitle = title;
            hintBody = body;
        }

        public void ClearHint()
        {
            hintTitle = null;
            hintBody = null;
        }

        public void Release()
        {
            ClearHint();
            if (bound && cursorImage != null)
            {
                cursorImage.sprite = originalSprite;
                cursorImage.color = originalColor;
                cursorImage.preserveAspect = originalPreserveAspect;
                if (cursorRect != null)
                    cursorRect.sizeDelta = originalSize;
            }
            if (cursorSprites != null)
                for (int i = 0; i < cursorSprites.Length; i++)
                    if (cursorSprites[i] != null)
                        Destroy(cursorSprites[i]);
            if (cursorTextures != null)
                for (int i = 0; i < cursorTextures.Length; i++)
                    if (cursorTextures[i] != null)
                        Destroy(cursorTextures[i]);
            cursorSprites = null;
            cursorTextures = null;
            cursorImage = null;
            cursorRect = null;
            bound = false;
        }

        private void OnGUI()
        {
            if (!bound || string.IsNullOrEmpty(hintBody) ||
                Subject42DebugMenu.IsDebugMenuOpen)
                return;
            EnsureStyles();
            const float width = 252f;
            float height = string.IsNullOrEmpty(hintTitle) ? 58f : 76f;
            Rect panel = new(18f, Mathf.Clamp(Screen.height * 0.34f,
                104f, Screen.height - height - 90f), width, height);
            GUI.Box(panel, GUIContent.none, panelStyle);
            if (!string.IsNullOrEmpty(hintTitle))
                GUI.Label(new Rect(panel.x + 14f, panel.y + 9f,
                    width - 28f, 22f), hintTitle, titleStyle);
            GUI.Label(new Rect(panel.x + 14f,
                panel.y + (string.IsNullOrEmpty(hintTitle) ? 11f : 34f),
                width - 28f, height - 18f), hintBody, bodyStyle);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
                return;
            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave
            };
            panelTexture.SetPixel(0, 0, new Color(0.015f, 0.045f, 0.06f, 0.86f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTexture },
                border = new RectOffset(1, 1, 1, 1)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.95f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.84f, 0.91f, 0.94f) }
            };
        }

        private void BuildCursorSprites()
        {
            cursorSprites = new Sprite[5];
            cursorTextures = new Texture2D[5];
            for (int i = 0; i < cursorTextures.Length; i++)
            {
                Texture2D texture = new(24, 24, TextureFormat.RGBA32, false)
                {
                    name = $"Orbital Cursor {(OrbitalCursorState)i}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                Color[] clear = new Color[24 * 24];
                texture.SetPixels(clear);
                DrawShape(texture, (OrbitalCursorState)i);
                texture.Apply();
                cursorTextures[i] = texture;
                cursorSprites[i] = Sprite.Create(texture,
                    new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 24f);
            }
        }

        private static void DrawShape(Texture2D texture, OrbitalCursorState state)
        {
            Color cyan = new(0.15f, 1f, 0.88f, 1f);
            Color color = state == OrbitalCursorState.ValidDrop
                ? new Color(0.25f, 1f, 0.45f, 1f)
                : state == OrbitalCursorState.InvalidDrop
                    ? new Color(1f, 0.22f, 0.28f, 1f) : cyan;
            if (state == OrbitalCursorState.Normal)
            {
                Line(texture, 5, 19, 5, 6, color, 2);
                Line(texture, 5, 19, 15, 9, color, 2);
                Line(texture, 5, 6, 9, 10, color, 2);
                Line(texture, 9, 10, 13, 4, color, 2);
            }
            else if (state == OrbitalCursorState.Grabbable)
            {
                Line(texture, 7, 13, 7, 18, color, 2);
                Line(texture, 10, 11, 10, 19, color, 2);
                Line(texture, 13, 11, 13, 19, color, 2);
                Line(texture, 16, 13, 16, 18, color, 2);
                Line(texture, 7, 13, 5, 11, color, 2);
                Line(texture, 5, 11, 5, 8, color, 2);
                Line(texture, 7, 6, 16, 6, color, 2);
                Line(texture, 7, 6, 5, 8, color, 2);
                Line(texture, 16, 6, 18, 9, color, 2);
            }
            else if (state == OrbitalCursorState.Dragging)
            {
                Line(texture, 6, 15, 9, 19, color, 3);
                Line(texture, 9, 19, 17, 16, color, 3);
                Line(texture, 17, 16, 16, 8, color, 3);
                Line(texture, 16, 8, 8, 8, color, 3);
                Line(texture, 8, 8, 6, 15, color, 3);
            }
            else if (state == OrbitalCursorState.ValidDrop)
            {
                Line(texture, 4, 9, 4, 4, color, 2); Line(texture, 4, 4, 9, 4, color, 2);
                Line(texture, 15, 4, 20, 4, color, 2); Line(texture, 20, 4, 20, 9, color, 2);
                Line(texture, 4, 15, 4, 20, color, 2); Line(texture, 4, 20, 9, 20, color, 2);
                Line(texture, 15, 20, 20, 20, color, 2); Line(texture, 20, 20, 20, 15, color, 2);
                Line(texture, 9, 12, 11, 14, color, 2); Line(texture, 11, 14, 16, 9, color, 2);
            }
            else
            {
                Line(texture, 5, 5, 19, 19, color, 3);
                Line(texture, 5, 19, 19, 5, color, 3);
            }
        }

        private static void Line(Texture2D texture, int x0, int y0,
            int x1, int y1, Color color, int width)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                for (int x = -width / 2; x <= width / 2; x++)
                    for (int y = -width / 2; y <= width / 2; y++)
                        if (x0 + x >= 0 && x0 + x < texture.width &&
                            y0 + y >= 0 && y0 + y < texture.height)
                            texture.SetPixel(x0 + x, y0 + y, color);
                if (x0 == x1 && y0 == y1)
                    break;
                int doubled = 2 * error;
                if (doubled >= dy) { error += dy; x0 += sx; }
                if (doubled <= dx) { error += dx; y0 += sy; }
            }
        }

        private void OnDisable() => Release();

        private void OnDestroy()
        {
            Release();
            if (panelTexture != null)
                Destroy(panelTexture);
        }
    }
}
