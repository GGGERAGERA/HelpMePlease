using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalFlashFamily
    {
        Other, MuzzlePistol, MuzzleShotgun, MuzzleSniper, Impact, Arc, CoreRay, Relocation,
        ImpactPistol, ImpactShotgun, ImpactSniper
    }

    [System.Serializable]
    public sealed class OrbitalFlashStyle
    {
        public Color Tint = Color.white;
        [Range(0f, 1f)] public float TintAmount;
        [Min(0f)] public float Brightness = 1f;
        [Range(0f, 1f)] public float Opacity = 1f;
        [Min(0f)] public float Duration = 1f;
        [Min(0f)] public float Width = 1f;

        public Color ApplyColor(Color source)
        {
            float alpha = source.a;
            Color color = Color.LerpUnclamped(source, Tint, TintAmount);
            color.r *= Brightness; color.g *= Brightness; color.b *= Brightness;
            color.a = Mathf.LerpUnclamped(alpha, Tint.a, TintAmount) * Opacity;
            return color;
        }

        public static float TailOpacity(float normalizedAge, float fade) =>
            1f - Mathf.Clamp01(fade) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, 1f, normalizedAge));
    }

    [CreateAssetMenu(menuName = "Subject42/Orbital Presentation Config")]
    public sealed class OrbitalPresentationConfig : ScriptableObject
    {

        private static OrbitalPresentationConfig active;
        private static OrbitalPresentationConfig authored;
        public static bool IsConfigured => active != null;
        public static int ConfigurationRevision { get; private set; }

        [Header("Authored composition")]
        public GameObject StationPrefab;
        public OrbitalRingView RingPrefab;
        public OrbitalMountView MountPrefab;
        public CustomOrbitDrawing CustomDrawingPrefab;
        public Material VisualMaterial;
        public Sprite PixelSprite, CircleSprite, RingIcon;
        [Header("Run reward icons — leave missing authored art unassigned")]
        public Sprite NewMountIcon, RingCapacityIcon, RingDamageIcon, RingSpeedIcon, NewRingIcon;
        public LineRenderer EnergyLinePrefab;
        [Header("Cannon feedback — visual only, no contact geometry")]
        [Min(.01f)] public float ShotHaloDuration = .12f;
        [Range(0f, 1f)] public float ShotHaloAlpha = .62f;
        [Min(1f)] public float ShotHaloScale = 1.16f;
        [Min(.01f)] public float ProjectileMuzzleDuration = .10f;
        [Min(.01f)] public float ProjectileImpactDuration = .15f;
        [Min(.01f)] public float ProjectileImpactSize = .25f;
        [Range(0f, 1f)] public float ProjectileImpactWhitening = .35f;
        [Range(0f, 1f)] public float FlashTailFade = 1f;
        [Header("FlashLink families — visual overrides, identity by default")]
        public OrbitalFlashStyle OtherFlash = new();
        public OrbitalFlashStyle PistolMuzzleFlash = new();
        public OrbitalFlashStyle ShotgunMuzzleFlash = new();
        public OrbitalFlashStyle SniperMuzzleFlash = new();
        public OrbitalFlashStyle ImpactFlash = new();
        public OrbitalFlashStyle PistolImpactFlash = new();
        public OrbitalFlashStyle ShotgunImpactFlash = new();
        public OrbitalFlashStyle SniperImpactFlash = new();
        public OrbitalFlashStyle ArcFlash = new();
        public OrbitalFlashStyle RelocationFlash = new();

        public OrbitalFlashStyle GetFlashStyle(OrbitalFlashFamily family) => family switch
        {
            OrbitalFlashFamily.MuzzlePistol => PistolMuzzleFlash,
            OrbitalFlashFamily.MuzzleShotgun => ShotgunMuzzleFlash,
            OrbitalFlashFamily.MuzzleSniper => SniperMuzzleFlash,
            OrbitalFlashFamily.Impact => ImpactFlash,
            OrbitalFlashFamily.ImpactPistol => PistolImpactFlash,
            OrbitalFlashFamily.ImpactShotgun => ShotgunImpactFlash,
            OrbitalFlashFamily.ImpactSniper => SniperImpactFlash,
            OrbitalFlashFamily.Arc => ArcFlash,
            OrbitalFlashFamily.Relocation => RelocationFlash,
            _ => OtherFlash
        };
        public int FlashPoolCapacity = 128;
        public int ProjectilePrewarmCount = 96;
        public GameObject PistolProjectilePrefab;
        [Header("Mounted projectile presentation (body is primary, trail is decorative)")]
        public GameObject WeaponProjectilePrefab;
        public OrbitalWeaponSettings.ProjectileStyle PistolProjectile = new();
        public OrbitalWeaponSettings.ProjectileStyle ShotgunProjectile = new()
        {
            Color = new(.3f, 1f, .25f), BodySize = new(.12f, .12f),
            TrailSeconds = .025f, TrailWidth = .018f, TrailOpacity = .18f
        };
        public OrbitalWeaponSettings.ProjectileStyle SniperProjectile = new()
        {
            Color = new(1f, .18f, .12f), BodySize = new(.5f, .07f),
            TrailSeconds = .012f, TrailWidth = .022f, TrailOpacity = .32f
        };
        [Header("Visual-only modules")]
        public GameObject PistolPrefab;
        public GameObject SawPrefab;
        public GameObject ShotgunPrefab;
        public GameObject SniperPrefab;
        public GameObject ArcPrefab, LinkPrefab;

        [Header("Module visual scale")]
        [Min(0.1f)] public float PistolVisualScale = 2.15f;
        [Min(0.1f)] public float SawVisualScale = 1.85f;
        [Min(0.1f)] public float ShotgunVisualScale = 1f;
        [Min(0.1f)] public float ArcVisualScale = 0.46f;
        [Min(0.1f)] public float LinkNodeVisualScale = 0.42f;
        public int MountedWeaponSortingOffset = 12;

        [Header("Ring spatial presentation")]
        [Min(0f)] public float RingRadiusPadding = .75f;
        [Tooltip("The production blade points along local +Y; -180 aligns it across the path, along its normal.")]
        public float SawTangentOffset = -180f;

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
        public enum RingPattern { Continuous, Dashed, Energy }
        [System.Serializable]
        public sealed class RingSurfaceSettings
        {
            [Range(0f, 2f)] public float Saturation = 1f;
            [Range(0f, 3f)] public float Brightness = 1f;
            [Range(0f, 3f)] public float Glow = 1f;
            public RingPattern Pattern;
            [Range(8, 128)] public int DashCount = 48;
            [Range(.1f, .95f)] public float DashFill = .65f;
            [Range(-1f, 1f)] public float PatternSpeed = .025f;
        }
        [Header("Orbit surface experiments — shared by all tiers")]
        public RingSurfaceSettings RingSurface = new();
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

        public static void Configure(OrbitalPresentationConfig config)
        {
            if (config == null) throw new System.ArgumentNullException(nameof(config));
            // Presentation is mutable during play; the authored asset remains the explicit save destination.
            ReleaseRuntimeConfiguration();
            authored = config;
            active = Instantiate(config);
            active.name = config.name + " (Runtime)";
            active.hideFlags = HideFlags.DontSave;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (PlayerPrefs.HasKey(RingAppearancePreferenceKey))
            {
                var saved = JsonUtility.FromJson<SavedRingAppearance>(PlayerPrefs.GetString(RingAppearancePreferenceKey));
                active.RingSurface = saved.Surface;
                active.RingLineWidth = saved.Width;
                active.RingLineAlpha = saved.Alpha;
            }
#endif
            ConfigurationRevision++;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string RingAppearancePreferenceKey = "Subject42.Orbits.Appearance";
        [System.Serializable]
        private sealed class SavedRingAppearance
        {
            public RingSurfaceSettings Surface;
            public float Width, Alpha;
        }

        public void SaveRingAppearance()
        {
#if UNITY_EDITOR
            var destination = this == active ? authored : this;
            if (destination == null || !UnityEditor.AssetDatabase.Contains(destination))
                throw new System.InvalidOperationException("The ring appearance has no authored save destination.");
            UnityEditor.Undo.RecordObject(destination, "Save ORBITAL ring appearance");
            destination.RingSurface = JsonUtility.FromJson<RingSurfaceSettings>(JsonUtility.ToJson(RingSurface));
            destination.RingLineWidth = RingLineWidth;
            destination.RingLineAlpha = RingLineAlpha;
            UnityEditor.EditorUtility.SetDirty(destination);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(destination);
#else
            PlayerPrefs.SetString(RingAppearancePreferenceKey, JsonUtility.ToJson(new SavedRingAppearance
            { Surface = RingSurface, Width = RingLineWidth, Alpha = RingLineAlpha }));
            PlayerPrefs.Save();
#endif
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConfiguration()
        {
            ReleaseRuntimeConfiguration();
            authored = null;
            ConfigurationRevision = 0;
        }

        private static void ReleaseRuntimeConfiguration()
        {
            if (active == null) return;
            if (Application.isPlaying) Destroy(active);
            else DestroyImmediate(active);
            active = null;
        }

        public static OrbitalPresentationConfig Active => active != null ? active :
            throw new System.InvalidOperationException("OrbitalPresentationConfig is not assigned by ProductionSceneComposition.");

        public static bool TryGetRequired(out OrbitalPresentationConfig config, out string error)
        {
            config = active;
            if (config == null)
            {
                error = "ProductionSceneComposition must assign OrbitalPresentationConfig before station creation.";
                return false;
            }
            return config.ValidateRequiredReferences(out error);
        }

        public bool ValidateRequiredReferences(out string error)
        {
            if (StationPrefab == null || !StationPrefab.TryGetComponent<OrbitalStationView>(out var station) ||
                !station.IsValid || StationPrefab.GetComponent<OrbitalStationRuntime>() == null ||
                RingPrefab == null || !RingPrefab.IsValid || MountPrefab == null || !MountPrefab.IsValid ||
                VisualMaterial == null || EnergyLinePrefab == null || CircleSprite == null || PixelSprite == null ||
                WeaponProjectilePrefab == null)
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
            if (RingIcon == null)
            { error = "required ring icon missing"; return false; }
            error = "OK";
            return true;
        }

        public GameObject GetPrefab(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => PistolPrefab,
            OrbitalModuleKind.Saw => SawPrefab,
            OrbitalModuleKind.Shotgun => ShotgunPrefab,
            OrbitalModuleKind.Sniper => SniperPrefab,
            OrbitalModuleKind.ArcEmitter => ArcPrefab,
            OrbitalModuleKind.LinkNode => LinkPrefab,
            _ => null
        };

        public OrbitalWeaponSettings GetWeaponSettings(OrbitalModuleKind kind)
        {
            var settings = kind switch
            {
                OrbitalModuleKind.Shotgun => OrbitalWeaponSettings.Shotgun(),
                OrbitalModuleKind.Sniper => OrbitalWeaponSettings.Sniper(),
                OrbitalModuleKind.Saw => OrbitalWeaponSettings.Saw(),
                _ => OrbitalWeaponSettings.Pistol()
            };
            settings.Kind = kind;
            settings.Category = OrbitalCategories.ForModule(kind);
            settings.WeaponVisual = GetPrefab(kind);
            if (kind is OrbitalModuleKind.Pistol or OrbitalModuleKind.Shotgun or OrbitalModuleKind.Sniper)
            {
                settings.Presentation = kind switch
                {
                    OrbitalModuleKind.Shotgun => ShotgunProjectile,
                    OrbitalModuleKind.Sniper => SniperProjectile,
                    _ => PistolProjectile
                };
                settings.ProjectileVisual = WeaponProjectilePrefab;
                settings.Color = settings.Presentation.Color;
            }
            return settings;
        }

        public float GetScale(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => PistolVisualScale,
            OrbitalModuleKind.Saw => SawVisualScale,
            OrbitalModuleKind.Shotgun => ShotgunVisualScale,
            OrbitalModuleKind.ArcEmitter => ArcVisualScale,
            OrbitalModuleKind.LinkNode => LinkNodeVisualScale,
            _ => 1f
        };
    }
}
