using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalRingView : MonoBehaviour
    {
        public LineRenderer BackLine, FrontLine;
        public Transform MountsRoot;
        [SerializeField, Range(0f, 1f)] private float backOpacityMultiplier = .5f;
        [Tooltip("Fraction of the back semicircle used to blend opacity at each side.")]
        [SerializeField, Range(.01f, .5f)] private float sideBlendFraction = .12f;

        private readonly Gradient backGradient = new();
        private readonly Gradient frontGradient = new();
        private readonly GradientColorKey[] colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] frontAlphaKeys = new GradientAlphaKey[2];
        private readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[8];
        private Color lastColor;
        private bool hasColor;
        private bool lastDepthAware;
        private int depthSortingLayer;
        private int backSortingOrder;

        private static readonly int SurfaceId = Shader.PropertyToID("_RingSurface");
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
            BackLine.SetPropertyBlock(surface);
            FrontLine.SetPropertyBlock(surface);
            transitionAge = Mathf.Min(transitionAge + unscaledDeltaTime, config.RingTierTransitionDuration);
            energyTime = Mathf.Repeat(energyTime + unscaledDeltaTime * currentStyle.PulseSpeed, 1f);
        }

        public bool IsValid => BackLine != null && FrontLine != null &&
            BackLine != FrontLine && MountsRoot != null;

        // Unit semicircles are authored once; only their presentation changes at runtime.
        public void SetAppearance(float radius, float width, Color color, bool depthAware = true)
        {
            Vector3 scale = Vector3.one * radius;
            BackLine.transform.localScale = FrontLine.transform.localScale = scale;
            BackLine.widthMultiplier = FrontLine.widthMultiplier = width;
            if (hasColor && lastColor == color && lastDepthAware == depthAware) return;
            if (!hasColor)
            {
                depthSortingLayer = BackLine.sortingLayerID;
                backSortingOrder = BackLine.sortingOrder;
            }
            BackLine.sortingLayerID = FrontLine.sortingLayerID = depthAware
                ? depthSortingLayer : SortingLayer.NameToID("Player");
            BackLine.sortingOrder = depthAware ? backSortingOrder : FrontLine.sortingOrder;
            hasColor = true;
            lastDepthAware = depthAware;
            lastColor = color;
            colorKeys[0] = new GradientColorKey(color, 0f);
            colorKeys[1] = new GradientColorKey(color, 1f);
            // Use gradients for both arcs: startColor/endColor quantize to Color32.
            frontAlphaKeys[0] = new GradientAlphaKey(color.a, 0f);
            frontAlphaKeys[1] = new GradientAlphaKey(color.a, 1f);
            frontGradient.SetKeys(colorKeys, frontAlphaKeys);
            FrontLine.colorGradient = frontGradient;
            if (!depthAware)
            {
                BackLine.colorGradient = frontGradient;
                return;
            }
            // Equal opacity at both shared endpoints, without overlapping transparent caps.
            // Intermediate keys approximate a smoothstep, avoiding a sharp fade boundary.
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                float alpha = color.a * Mathf.Lerp(1f, backOpacityMultiplier, t * t * (3f - 2f * t));
                alphaKeys[i] = new GradientAlphaKey(alpha, sideBlendFraction * t);
                alphaKeys[7 - i] = new GradientAlphaKey(alpha, 1f - sideBlendFraction * t);
            }
            backGradient.SetKeys(colorKeys, alphaKeys);
            BackLine.colorGradient = backGradient;
        }
    }
}
