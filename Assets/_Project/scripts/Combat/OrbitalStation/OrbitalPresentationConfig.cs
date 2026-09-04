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

        [Header("Mount readability")]
        [Min(0.05f)] public float NormalMountSize = 0.18f;
        [Min(0.05f)] public float SelectionMountSize = 0.30f;
        [Range(0f, 1f)] public float NormalAlpha = 0.9f;
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
                VisualMaterial == null || CircleSprite == null || PixelSprite == null)
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
