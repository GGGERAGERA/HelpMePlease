using UnityEngine;
using UnityEngine.UI;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed partial class OrbitalInteractionPresentation : MonoBehaviour
    {
        private string hintTitle;
        private string hintBody;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle panelStyle;
        private Texture2D panelTexture;
        private bool bound;
        private OrbitalStationRuntime station;
        [Header("Gameplay screen feedback")]
        [SerializeField] private Image screenEffect;
        [SerializeField, Range(0f, 1f)] private float selectionDim = .72f;
        [SerializeField, Min(0f)] private float selectionClearPadding = .7f;
        [SerializeField, Min(1f)] private float selectionRingEmphasis = 2.4f;
        [SerializeField, Range(0f, 1f)] private float bulletTimeTint = .16f;
        [SerializeField, Range(0f, 1f)] private float bulletTimeVignette = .45f;
        private Material screenMaterial;
        private Material authoredScreenMaterial;
        private Camera gameplayCamera;
        private Camera cameraOwner;
        public bool HasScreenEffect => screenEffect != null && screenEffect.material != null;
        public bool RingSelectionFocus { get; private set; }
        public float SelectionRingEmphasis => selectionRingEmphasis;
        public bool ScreenEffectVisible => screenEffect != null && screenEffect.enabled;

        public void SetRingSelectionFocus(bool active)
        {
            RingSelectionFocus = active;
            UpdateScreenEffect();
        }

        private void LateUpdate() { UpdateScreenEffect(); UpdateConstructor(); }

        private void UpdateScreenEffect()
        {
            if (screenEffect == null || screenMaterial == null) return;
            float bullet = station != null && station.InputOwner != null ? station.InputOwner.BulletTimeBlend : 0f;
            bool visible = station != null && station.IsInitialized && !SceneTransitionOverlay.IsTransitioning &&
                (RingSelectionFocus || bullet > 0f);
            screenEffect.enabled = visible;
            if (!visible) return;
            screenMaterial.SetFloat("_Dim", RingSelectionFocus ? selectionDim : 0f);
            screenMaterial.SetFloat("_Bullet", bullet);
            screenMaterial.SetFloat("_Tint", bulletTimeTint);
            screenMaterial.SetFloat("_Vignette", bulletTimeVignette);
            screenMaterial.SetFloat("_UnscaledTime", Time.unscaledTime);
            if (gameplayCamera != null)
            {
                Vector3 center = gameplayCamera.WorldToViewportPoint(station.transform.position);
                Vector2 footprint = station.PresentationExtents;
                float radius = Mathf.Max(station.SlowFieldRadius, Mathf.Max(footprint.x, footprint.y)) + selectionClearPadding;
                Vector2 extents = Vector2.one * radius;
                Vector3 edge = gameplayCamera.WorldToViewportPoint(station.transform.position + (Vector3)extents);
                screenMaterial.SetVector("_Focus", new Vector4(center.x, center.y,
                    Mathf.Max(.02f, Mathf.Abs(edge.x - center.x)), Mathf.Max(.02f, Mathf.Abs(edge.y - center.y))));
            }
        }

        public void Bind(OrbitalStationRuntime station)
        {
            if (bound || screenMaterial != null)
                Release();
            this.station = station;
            BindConstructor();
            gameplayCamera = cameraOwner;
            authoredScreenMaterial = screenEffect.material;
            screenMaterial = new Material(authoredScreenMaterial);
            screenEffect.material = screenMaterial;
            screenEffect.enabled = false;
            station.InputOwner.BindCamera(gameplayCamera);
            bound = true;
        }

        public void BindCamera(Camera camera)
        {
            cameraOwner = gameplayCamera = camera;
            station?.InputOwner?.BindCamera(camera);
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
            ReleaseConstructor();
            RingSelectionFocus = false;
            if (screenEffect != null)
            {
                screenEffect.enabled = false;
                if (authoredScreenMaterial != null) screenEffect.material = authoredScreenMaterial;
            }
            if (screenMaterial != null) Destroy(screenMaterial);
            screenMaterial = null;
            gameplayCamera = null;
            ClearHint();
            bound = false;
        }

        private void OnGUI()
        {
            if (!bound || string.IsNullOrEmpty(hintBody) ||
                station.InputOwner.IsGameplayInputBlocked)
                return;
            EnsureStyles();
            const float width = 252f;
            float height = string.IsNullOrEmpty(hintTitle) ? 58f : 76f;
            Rect panel = new(18f, Mathf.Clamp(Screen.height * 0.34f,
                104f, Screen.height - height - 90f), width, height);
            GUI.Box(panel, GUIContent.none, panelStyle);
            if (!string.IsNullOrEmpty(hintTitle))
                GUI.Label(new Rect(panel.x + 14f, panel.y + 9f,
                    width - 28f, 22f), LocalizationService.EnsureExists().Get(hintTitle), titleStyle);
            GUI.Label(new Rect(panel.x + 14f,
                panel.y + (string.IsNullOrEmpty(hintTitle) ? 11f : 34f),
                width - 28f, height - 18f), LocalizationService.EnsureExists().Get(hintBody), bodyStyle);
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

        private void OnDisable() => Release();

        private void OnDestroy()
        {
            Release();
            if (panelTexture != null)
                Destroy(panelTexture);
        }
    }
}
