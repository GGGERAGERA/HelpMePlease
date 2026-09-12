using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalRingView : MonoBehaviour
    {
        public LineRenderer BackLine, FrontLine;
        public Transform MountsRoot;
        private readonly Gradient frontGradient = new();
        private readonly GradientColorKey[] colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] frontAlphaKeys = new GradientAlphaKey[2];
        private Color lastColor;
        private bool hasColor;
        private OrbitalPathGeometry geometry = OrbitalPathGeometry.Circle;

        public void InitializeGeometry(OrbitalPathGeometry path)
        {
            geometry = path;
            // Keep the legacy prefab reference, but render one continuous closed trajectory.
            BackLine.enabled = false;
            FrontLine.loop = true;
            FrontLine.sortingLayerID = SortingLayer.NameToID("Player");
            const int segments = 256;
            FrontLine.positionCount = segments;
            for (int i = 0; i < segments; i++)
                FrontLine.SetPosition(i, path.Position(i / (float)segments, 1f) / path.Scale(1f));
        }
        private static readonly int SurfaceId = Shader.PropertyToID("_RingSurface");
        private static readonly int CoreId = Shader.PropertyToID("_CoreEnergy");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private Vector4 coreEnergy;
        private Color coreColor;
        public void SetCoreEnergy(int level, Color color, float pulse, float time)
        {
            coreEnergy = new Vector4(level * OrbitalPresentationConfig.Active.CoreIdleHighlight,
                pulse * OrbitalPresentationConfig.Active.CorePulseBrightness, time, 0f);
            coreColor = color;
        }
        private MaterialPropertyBlock surface;
        private OrbitalPresentationConfig.RingTierStyle previousStyle, currentStyle;
        private int displayedTier;
        private float transitionAge;
        private float energyTime;
        public Color AccentColor => currentStyle.BaseColor;
        public bool IsTierTransitionActive => transitionAge < OrbitalPresentationConfig.Active.RingTierTransitionDuration;

        public void InitializeTier(int tier)
        {
            surface ??= new MaterialPropertyBlock();
            displayedTier = tier;
            previousStyle = currentStyle = OrbitalPresentationConfig.Active.GetRingTier(tier);
            transitionAge = OrbitalPresentationConfig.Active.RingTierTransitionDuration;
        }

        // Transient feedback is view-owned. Restores initialize directly from persisted progression.
        public void UpdateTierAppearance(int tier, float radius, float corePulse, float highlight,
            bool dimmed, bool depthAware, float unscaledDeltaTime)
        {
            var config = OrbitalPresentationConfig.Active;
            if (tier != displayedTier)
            {
                previousStyle = currentStyle;
                transitionAge = tier > displayedTier ? 0f : config.RingTierTransitionDuration;
                displayedTier = tier;
            }
            float t = Mathf.Clamp01(transitionAge / config.RingTierTransitionDuration);
            float blend = Mathf.SmoothStep(0f, 1f, t);
            var target = config.GetRingTier(tier);
            currentStyle = new OrbitalPresentationConfig.RingTierStyle(
                Color.Lerp(previousStyle.BaseColor, target.BaseColor, blend),
                Mathf.Lerp(previousStyle.Alpha, target.Alpha, blend),
                Mathf.Lerp(previousStyle.Emission, target.Emission, blend),
                Mathf.Lerp(previousStyle.Glow, target.Glow, blend),
                Mathf.Lerp(previousStyle.CoreWhitening, target.CoreWhitening, blend),
                Mathf.Lerp(previousStyle.PulseStrength, target.PulseStrength, blend),
                Mathf.Lerp(previousStyle.PulseSpeed, target.PulseSpeed, blend));
            float burst = Mathf.Sin(t * Mathf.PI);
            float energy = 1f + currentStyle.PulseStrength * Mathf.Sin(energyTime * Mathf.PI * 2f);
            float brightness = energy + burst * config.RingUpgradeBrightness +
                (highlight + corePulse) * config.RingHighlightBrightness;
            Color color = currentStyle.BaseColor;
            color.a = currentStyle.Alpha * config.RingLineAlpha * (dimmed ? config.RingDimmedAlpha : 1f);
            // The mesh includes the soft skirt; the visible core remains RingLineWidth wide.
            SetAppearance(radius, config.RingLineWidth * 3f, color, depthAware);
            surface.SetVector(SurfaceId, new Vector4(currentStyle.Emission * brightness,
                currentStyle.Glow + burst * config.RingUpgradeGlow, currentStyle.CoreWhitening, 0f));
            surface.SetVector(CoreId, coreEnergy);
            surface.SetColor(CoreColorId, coreColor);
            FrontLine.SetPropertyBlock(surface);
            transitionAge = Mathf.Min(transitionAge + unscaledDeltaTime, config.RingTierTransitionDuration);
            energyTime = Mathf.Repeat(energyTime + unscaledDeltaTime * currentStyle.PulseSpeed, 1f);
        }

        public bool IsValid => BackLine != null && FrontLine != null &&
            BackLine != FrontLine && MountsRoot != null;

        // depthAware is retained for callers of the old view API; ring lines no longer split by depth.
        public void SetAppearance(float radius, float width, Color color, bool depthAware = true)
        {
            FrontLine.transform.localScale = Vector3.one * geometry.Scale(radius);
            FrontLine.widthMultiplier = width;
            if (hasColor && lastColor == color) return;
            hasColor = true;
            lastColor = color;
            colorKeys[0] = new GradientColorKey(color, 0f);
            colorKeys[1] = new GradientColorKey(color, 1f);
            frontAlphaKeys[0] = new GradientAlphaKey(color.a, 0f);
            frontAlphaKeys[1] = new GradientAlphaKey(color.a, 1f);
            frontGradient.SetKeys(colorKeys, frontAlphaKeys);
            FrontLine.colorGradient = frontGradient;
        }
    }
}
