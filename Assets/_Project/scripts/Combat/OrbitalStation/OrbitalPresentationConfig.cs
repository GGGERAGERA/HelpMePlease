using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [CreateAssetMenu(menuName = "Subject42/Orbital Presentation Config")]
    public sealed class OrbitalPresentationConfig : ScriptableObject
    {
        private const string ResourcePath = "OrbitalStation/OrbitalPresentationConfig";
        private static OrbitalPresentationConfig active;

        [Header("Production miniWeapons (visual only)")]
        public GameObject PistolPrefab;
        public GameObject LaserSwordPrefab;
        public GameObject ImpulseGunPrefab;

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
        [Range(0.05f, 1f)] public float RelocationTimeScale = 0.2f;

        public static OrbitalPresentationConfig Active
        {
            get
            {
                if (active == null)
                    active = Resources.Load<OrbitalPresentationConfig>(ResourcePath);
                if (active == null)
                {
                    active = CreateInstance<OrbitalPresentationConfig>();
                    active.hideFlags = HideFlags.DontSave;
                    Debug.LogWarning("[OrbitalStation] Presentation config resource missing; using safe defaults.");
                }
                return active;
            }
        }

        public GameObject GetPrefab(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => PistolPrefab,
            OrbitalModuleKind.LaserSword => LaserSwordPrefab,
            OrbitalModuleKind.ImpulseGun => ImpulseGunPrefab,
            _ => null
        };

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
