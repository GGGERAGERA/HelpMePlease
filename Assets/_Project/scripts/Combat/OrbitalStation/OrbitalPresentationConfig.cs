using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [CreateAssetMenu(menuName = "Subject42/Orbital Presentation Config")]
    public sealed class OrbitalPresentationConfig : ScriptableObject
    {
        private const string ResourcePath = "OrbitalStation/OrbitalPresentationConfig";
        private static OrbitalPresentationConfig active;

        [System.Serializable]
        public sealed class PlayerVariant { public GameObject Source; public GameObject Production; }
        [Header("Authored composition")]
        public GameObject StationPrefab;
        public OrbitalRingView RingPrefab;
        public OrbitalMountView MountPrefab;
        public Material VisualMaterial;
        public Sprite PixelSprite, CircleSprite, RingIcon;
        public Sprite CoreIcon;
        [Header("Run reward icons — leave missing authored art unassigned")]
        public Sprite NewMountIcon, RingCapacityIcon, RingDamageIcon, RingSpeedIcon, NewRingIcon;
        public LineRenderer EnergyLinePrefab;
        [Header("Core pulse overlay")]
        public Color CoreCyan = new(.15f, .85f, 1f);
        public Color CoreViolet = new(.65f, .35f, 1f);
        public Color CoreGold = new(1f, .85f, .4f);
        public float CoreIdleHighlight = .045f;
        public float CorePulseBrightness = 2f;
        public float CoreIdleParticleInterval = .7f;
        public float CoreChargeParticleInterval = .06f;
        public int CoreRingSparkCount = 2;
        public float CoreHaloSize = .75f;
        public float CoreHaloPulseSize = 1.45f;
        public float CoreRayDuration = .16f;
        public float CoreShakeDuration = .08f;
        public float CoreShakeMagnitude = .025f;
        public int FlashPoolCapacity = 128;
        public int ProjectilePrewarmCount = 96;
        public Color GetCoreWaveColor(int wave) => wave switch
        { 1 => CoreCyan, 2 => CoreViolet, _ => CoreGold };
        public PlayerVariant[] PlayerVariants;
        [Header("Visual-only modules")]
        public GameObject PistolPrefab;
        public GameObject LaserSwordPrefab;
        public GameObject ImpulseGunPrefab;
        public GameObject ArcPrefab, LinkPrefab;

        [Header("Module visual scale")]
        [Min(0.1f)] public float PistolVisualScale = 2.15f;
        [Min(0.1f)] public float LaserSwordVisualScale = 1.85f;
        [Min(0.1f)] public float ImpulseVisualScale = 2.15f;
        [Min(0.1f)] public float ArcVisualScale = 0.46f;
        [Min(0.1f)] public float LinkNodeVisualScale = 0.42f;
        public int MountedWeaponSortingOffset = 12;

        [Header("Ring spatial presentation")]
        [Min(0f)] public float RingRadiusPadding = .75f;
        [Tooltip("The production blade points along local +Y; -180 aligns it across the path, along its normal.")]
        public float LaserSwordTangentOffset = -180f;

        [Header("Figure-eight spatial presentation")]
        [Min(.1f)] public float FigureEightWidth = 1.4f;
        [Min(.1f)] public float FigureEightHeight = .65f;
        [Min(1f)] public float FigureEightSpacing = 1.25f;
        [Min(.05f), Tooltip("World-space front/back blend range above the crossing.")]
        public float FigureEightDepthRange = .3f;

        [System.Serializable]
        public struct RingTierStyle
        {
            public Color BaseColor;
            [Range(0f, 1f)] public float Alpha;
            [Min(0f)] public float Emission;
            [Range(0f, 1f)] public float Glow;
            [Range(0f, 1f)] public float CoreWhitening;
            [Range(0f, .2f)] public float PulseStrength;
            [Min(0f), Tooltip("Cycles per second")] public float PulseSpeed;

            public RingTierStyle(Color color, float alpha, float emission, float glow,
                float coreWhitening, float pulseStrength, float pulseSpeed)
            {
                BaseColor = color; Alpha = alpha; Emission = emission; Glow = glow;
                CoreWhitening = coreWhitening; PulseStrength = pulseStrength; PulseSpeed = pulseSpeed;
            }
        }

        [Header("Ring tiers: 0 / 1 / 2 / 3+ ring upgrades")]
        public RingTierStyle TierI = new(new Color(.88f, .96f, 1f), .72f, 1f, .12f, .08f, 0f, 0f);
        public RingTierStyle TierII = new(new Color(.04f, .65f, 1f), .82f, 1.15f, .22f, .12f, 0f, 0f);
        public RingTierStyle TierIII = new(new Color(.65f, .16f, 1f), .9f, 1.3f, .32f, .18f, .045f, .65f);
        public RingTierStyle TierIV = new(new Color(1f, .57f, .10f), 1f, 1.5f, .42f, .55f, .055f, .75f);
        [Min(.001f)] public float RingLineWidth = .045f;
        [Range(.25f, .5f)] public float RingTierTransitionDuration = .4f;
        [Min(0f)] public float RingUpgradeBrightness = .65f;
        [Range(0f, 1f)] public float RingUpgradeGlow = .3f;
        [Min(0f)] public float RingHighlightBrightness = .25f;
        [Range(0f, 1f)] public float RingDimmedAlpha = .32f;

        public RingTierStyle GetRingTier(int tier) => tier switch
        {
            1 => TierI, 2 => TierII, 3 => TierIII, _ => TierIV
        };

        [Header("Mount readability")]
        [Min(0.05f)] public float NormalMountSize = 0.18f;
        [Min(0.05f)] public float SelectionMountSize = 0.30f;
        [Range(0f, 1f)] public float NormalAlpha = 0.48f;
        [Range(0f, 1f)] public float HoverAlpha = 1f;
        [Min(0.05f)] public float HaloSize = 0.38f;
        [Range(0f, 1f)] public float RingLineAlpha = 0.52f;
        [Min(0.1f)] public float ModuleHitRadius = 0.16f;
        [Min(0.1f)] public float MountHitRadius = 0.72f;
        [Min(8f)] public float MountSelectionRadiusPixels = 26f;
        [Min(0f)] public float MountHoverHysteresisPixels = 8f;
        [Min(0f)] public float MountSwitchAdvantagePixels = 4f;
        [Range(0.05f, 1f)] public float RelocationTimeScale = 0.2f;

        [Header("World telekinesis")]
        [Min(0.5f)] public float TelekinesisGrabRange = 8f;
        [Min(0.1f)] public float TelekinesisPullSpeed = 12f;
        [Range(0.01f, 1f)] public float TelekinesisFollowSmoothness = 0.12f;
        [Min(0f)] public float TelekinesisThrowStrength = 1f;
        [Min(0f)] public float TelekinesisMaxThrowSpeed = 18f;
        [Min(0f)] public float TelekinesisThrowDrag = 3.5f;

        public static OrbitalPresentationConfig Active
        {
            get
            {
                if (active == null)
                    active = Resources.Load<OrbitalPresentationConfig>(ResourcePath);
                return active;
            }
        }

        public static bool TryGetRequired(out OrbitalPresentationConfig config, out string error)
        {
            if (active == null)
                active = Resources.Load<OrbitalPresentationConfig>(ResourcePath);
            config = active;
            if (config == null)
            {
                error = "required OrbitalPresentationConfig resource is missing";
                return false;
            }
            return config.ValidateRequiredReferences(out error);
        }

        public bool ValidateRequiredReferences(out string error)
        {
            if (StationPrefab == null || !StationPrefab.TryGetComponent<OrbitalStationView>(out var station) ||
                !station.IsValid || StationPrefab.GetComponent<OrbitalStationRuntime>() == null ||
                RingPrefab == null || !RingPrefab.IsValid || MountPrefab == null || !MountPrefab.IsValid ||
                VisualMaterial == null || EnergyLinePrefab == null || CircleSprite == null || PixelSprite == null)
            { error = "required authored station/ring/mount/material/sprite reference is missing"; return false; }
            foreach (OrbitalModuleKind kind in System.Enum.GetValues(typeof(OrbitalModuleKind)))
            {
                GameObject prefab = GetPrefab(kind);
                if (prefab == null || !prefab.TryGetComponent<OrbitalModuleView>(out var view) || !view.IsValid)
                { error = $"required {kind} presentation prefab/view is missing"; return false; }
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is BaseWeapon || component is Collider || component is Collider2D ||
                        component is Rigidbody || component is Rigidbody2D || component is AudioSource ||
                        (component is MonoBehaviour && component is not OrbitalModuleView &&
                         !component.GetType().FullName.Contains("Rendering.Universal.Light2D")))
                    { error = $"{kind} visual prefab contains missing or gameplay component"; return false; }
                }
            }
            if (PlayerVariants == null || PlayerVariants.Length == 0 || RingIcon == null)
            { error = "required production player variants or ring icon missing"; return false; }
            foreach (var entry in PlayerVariants)
                if (entry.Source == null || entry.Production == null ||
                    entry.Production.GetComponentsInChildren<OrbitalStationView>(true).Length != 1 ||
                    entry.Production.GetComponentsInChildren<BaseWeapon>(true).Length != 0)
                { error = "invalid ORBITAL production player variant"; return false; }
            error = "OK";
            return true;
        }

        public GameObject GetPrefab(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => PistolPrefab,
            OrbitalModuleKind.LaserSword => LaserSwordPrefab,
            OrbitalModuleKind.ImpulseGun => ImpulseGunPrefab,
            OrbitalModuleKind.ArcEmitter => ArcPrefab,
            OrbitalModuleKind.LinkNode => LinkPrefab,
            _ => null
        };

        public GameObject GetPlayerPrefab(GameObject source)
        {
            if (PlayerVariants != null)
                foreach (var entry in PlayerVariants)
                    if (entry.Source == source) return entry.Production;
            return null;
        }

        public float GetScale(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => PistolVisualScale,
            OrbitalModuleKind.LaserSword => LaserSwordVisualScale,
            OrbitalModuleKind.ImpulseGun => ImpulseVisualScale,
            OrbitalModuleKind.ArcEmitter => ArcVisualScale,
            OrbitalModuleKind.LinkNode => LinkNodeVisualScale,
            _ => 1f
        };
    }
}
