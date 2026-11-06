#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Subject42.Dev.VisualEffects
{
    public enum EffectLabKind { Particles, Trail, Line, SpriteOrScreen, Presentation }
    public enum EffectLabDemo { Asset, Portal, EnemyFlash, PlayerFlash, Relay, Corridor, FalseSignal, CatHeart, Shockwave }

    public sealed class EffectLabCatalog : ScriptableObject
    {
        public const string Folder = "Assets/_Project/Dev/Labs/VisualEffectsLab";
        public const string CatalogPath = Folder + "/EffectsCatalog.asset";
        public const string EffectsScenePath = Folder + "/EffectsLab.unity";
        public const string ShaderScenePath = Folder + "/ShaderLab.unity";
        public const string PendingSceneKey = "Subject42.VisualEffectsLab.PendingScene";
        public const string Output = "Artifacts/GeneratedQA/VisualEffectsLab";

        [Serializable]
        public sealed class Caller
        {
            public string PrefabPath = "";
            public string HierarchyPath = "";
            public string ComponentType = "";
            public string PropertyPath = "";
            public string ScriptPath = "";
            public bool InProduction;
        }

        [Serializable]
        public sealed class Entry
        {
            public string Id = "";
            public string DisplayName = "";
            public string SourceAssetPath = "";
            public string HierarchyPath = "";
            public int[] TransformIndices = Array.Empty<int>();
            public EffectLabKind Kind;
            public EffectLabDemo Demo;
            public string Library = "";
            public bool InProduction;
            public string Notes = "";
            public Caller[] Callers = Array.Empty<Caller>();
        }

        public Entry[] Effects = Array.Empty<Entry>();
        public int ScannedPrefabs;
        public int CoveredPrefabs;
        public string GeneratedUtc;
    }
}
#endif
