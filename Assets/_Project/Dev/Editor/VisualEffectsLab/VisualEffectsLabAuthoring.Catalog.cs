#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Dev.VisualEffects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class VisualEffectsLabAuthoring
{
    private static readonly string[] FxFolders =
    {
        "Assets/_Project/prefabs/fx/", "Assets/Epic Toon FX/Prefabs/",
        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/"
    };

    public static EffectLabCatalog RebuildCatalog()
    {
        EnsureFolder(EffectLabCatalog.Folder);
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var production = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled)
            .SelectMany(s => AssetDatabase.GetDependencies(s.path, true)), StringComparer.Ordinal);
        var entries = new List<EffectLabCatalog.Entry>();
        var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Cannot load prefab: " + path);
            prefabs.Add(path, prefab);
            bool standalone = FxFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));
            if (standalone) Add(prefab.transform);
            else
            {
                var roots = new HashSet<Transform>(prefab.GetComponentsInChildren<ParticleSystem>(true).Select(p => p.transform)
                    .Concat(prefab.GetComponentsInChildren<TrailRenderer>(true).Select(p => p.transform))
                    .Concat(prefab.GetComponentsInChildren<LineRenderer>(true).Select(p => p.transform)));
                foreach (var root in roots.Where(t => !Ancestors(t).Any(roots.Contains)).OrderBy(t => Hierarchy(t, prefab.transform), StringComparer.Ordinal)) Add(root);
            }

            void Add(Transform root)
            {
                int[] indices = Indices(root, prefab.transform);
                string hierarchy = Hierarchy(root, prefab.transform);
                EffectLabKind kind = root.GetComponentsInChildren<ParticleSystem>(true).Length > 0 ? EffectLabKind.Particles :
                    root.GetComponentsInChildren<TrailRenderer>(true).Length > 0 ? EffectLabKind.Trail :
                    root.GetComponentsInChildren<LineRenderer>(true).Length > 0 ? EffectLabKind.Line : EffectLabKind.SpriteOrScreen;
                entries.Add(new EffectLabCatalog.Entry
                {
                    Id = AssetDatabase.AssetPathToGUID(path) + ":" + string.Join(".", indices),
                    DisplayName = prefab.name + (indices.Length > 0 ? " / " + hierarchy : ""),
                    SourceAssetPath = path, TransformIndices = indices, HierarchyPath = hierarchy,
                    Kind = kind, Library = Library(path), InProduction = production.Contains(path),
                    Notes = kind == EffectLabKind.Line
                        ? "Исходный материал и параметры линии. Для пустых endpoints используется геометрия стенда; игровую геометрию формирует вызывающая система."
                        : "Автономный ассет. Рядом — визуальный макет источника; его игровая логика отключена. Параметры production-вызова могут отличаться."
                });
            }
        }

        var byPath = entries.GroupBy(e => e.SourceAssetPath).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var callers = entries.ToDictionary(e => e.Id, _ => new List<EffectLabCatalog.Caller>(), StringComparer.Ordinal);
        foreach (var pair in prefabs)
            foreach (var component in pair.Value.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null) InspectReferences(component, pair.Key, Hierarchy(component.transform, pair.Value.transform),
                    MonoScript.FromMonoBehaviour(component), pair.Value.transform);

        string[] dataFolders = { "Assets/_Project/Data", "Assets/_Project/Resources" };
        foreach (string path in AssetDatabase.FindAssets("t:ScriptableObject", dataFolders.Where(AssetDatabase.IsValidFolder).ToArray())
            .Select(AssetDatabase.GUIDToAssetPath).Distinct())
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (data != null && data.GetType().Assembly == typeof(EnemyHealth).Assembly)
                InspectReferences(data, path, "", MonoScript.FromScriptableObject(data), null);
        }
        foreach (var entry in entries)
            entry.Callers = callers[entry.Id].OrderByDescending(c => c.InProduction).ThenBy(c => c.PrefabPath, StringComparer.Ordinal)
                .ThenBy(c => c.PropertyPath, StringComparer.Ordinal).ToArray();

        AddPresentationDemos(entries, production);
        var catalog = AssetDatabase.LoadAssetAtPath<EffectLabCatalog>(EffectLabCatalog.CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<EffectLabCatalog>(); AssetDatabase.CreateAsset(catalog, EffectLabCatalog.CatalogPath); }
        catalog.Effects = entries.OrderByDescending(e => e.Demo != EffectLabDemo.Asset).ThenByDescending(e => e.InProduction)
            .ThenBy(e => e.DisplayName, StringComparer.Ordinal).ToArray();
        catalog.ScannedPrefabs = paths.Length;
        catalog.CoveredPrefabs = entries.Select(e => e.SourceAssetPath).Distinct().Count();
        catalog.GeneratedUtc = DateTime.UtcNow.ToString("O");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;

        void InspectReferences(Object owner, string ownerPath, string ownerHierarchy, MonoScript script, Transform prefabRoot)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object reference = property.objectReferenceValue;
                GameObject target = reference is GameObject go ? go : reference is Component c ? c.gameObject : null;
                if (target == null) continue;
                string targetPath = AssetDatabase.GetAssetPath(target);
                if (!byPath.TryGetValue(targetPath, out var matches)) continue;
                Transform root = target.transform.root;
                int[] targetIndices = Indices(target.transform, root);
                foreach (var entry in matches)
                {
                    if (!Prefix(entry.TransformIndices, targetIndices) && !Prefix(targetIndices, entry.TransformIndices)) continue;
                    if (ownerPath == entry.SourceAssetPath && FxFolders.Any(f => ownerPath.StartsWith(f, StringComparison.Ordinal))) continue;
                    var list = callers[entry.Id];
                    if (list.Any(caller => caller.PrefabPath == ownerPath && caller.HierarchyPath == ownerHierarchy && caller.PropertyPath == property.propertyPath)) continue;
                    list.Add(new EffectLabCatalog.Caller
                    {
                        PrefabPath = ownerPath, HierarchyPath = ownerHierarchy, ComponentType = owner.GetType().Name,
                        PropertyPath = property.propertyPath, ScriptPath = script != null ? AssetDatabase.GetAssetPath(script) : "",
                        InProduction = production.Contains(ownerPath)
                    });
                }
            }
        }
    }

    private static void AddPresentationDemos(List<EffectLabCatalog.Entry> entries, HashSet<string> production)
    {
        const string project = "Assets/_Project/";
        Demo("Портал · слои, искры, телепортация", "prefabs/Environment/PortalVisual.prefab", "", EffectLabDemo.Portal,
            "ProductionPortalPair", "ProductionPortalVisual.Render", "scripts/Run/Exploration/ProductionPortalPair.cs");
        Demo("Враг · вспышка попадания", "prefabs/Enemies/ShieldShooter.prefab", "", EffectLabDemo.EnemyFlash,
            "EnemyHealthPresentation", "EnemyWhiteFlash.Flash", "scripts/UI/HUD/EnemyHealthPresentation.cs");
        Demo("Игрок · вспышка урона", "prefabs/Characters/DiMag.prefab", "", EffectLabDemo.PlayerFlash,
            "PlayerFeedbackPresentation", "PlayerWhiteFlash.Flash", "scripts/UI/HUD/PlayerFeedbackPresentation.cs");
        Demo("Relay · вспышка передачи", "prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab", "Nodes/Node 1/SpawnFeedback", EffectLabDemo.Relay,
            "OrbitalRelayPresentation", "OrbitalRelaySpawnFeedback.Play", "scripts/World/Events/OrbitalRelayPresentation.cs");
        Demo("Corridor · состояние и импульс ворот", "prefabs/Environment/WorldEvents/Corridor/PF_CorridorGate.prefab", "", EffectLabDemo.Corridor,
            "CorridorPresentation", "CorridorNodeView.Pulse", "scripts/World/Events/Corridor/CorridorPresentation.cs");
        Demo("False Signal · диагностика передатчика", "prefabs/Environment/WorldEvents/FalseSignalPoint.prefab", "Presentation", EffectLabDemo.FalseSignal,
            "FalseSignalPoint", "FalseSignalPointView.Render", "scripts/World/Events/FalseSignalPoint.cs");
        Demo("Кот · сердце после взаимодействия", "prefabs/Bunker/PF_CatHeartFX.prefab", "", EffectLabDemo.CatHeart,
            "CatPetInteractable", "CatHeartFx", "scripts/Bunker/Interaction/CatPetInteractable.cs");
        Demo("Бомбер · ударная волна", "prefabs/fx/fx_ExplosionShockwave.prefab", "", EffectLabDemo.Shockwave,
            "EnemyBomberMovement", "ShockwaveFx", "scripts/Combat/Enemies/EnemyBomberMovement.cs");

        void Demo(string label, string sourcePath, string childPath, EffectLabDemo demo, string caller, string method, string scriptPath)
        {
            string path = project + sourcePath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing presentation demo source: " + path);
            Transform source = childPath.Length == 0 ? prefab.transform : prefab.transform.Find(childPath);
            if (source == null) throw new InvalidOperationException("Missing presentation subtree: " + path + "/" + childPath);
            entries.Add(new EffectLabCatalog.Entry
            {
                Id = "presentation:" + demo, DisplayName = label, SourceAssetPath = path,
                TransformIndices = Indices(source, prefab.transform), HierarchyPath = childPath,
                Kind = EffectLabKind.Presentation, Demo = demo, Library = "Subject#42 · presentation API",
                InProduction = production.Contains(path),
                Notes = "Вызывается исходный локальный presentation API. Урон, телепортация, награды и игровые контроллеры не запускаются.",
                Callers = new[] { new EffectLabCatalog.Caller { PrefabPath = demo switch {
                    EffectLabDemo.Relay => path,
                    EffectLabDemo.FalseSignal => path,
                    EffectLabDemo.CatHeart => project + "prefabs/Characters/Cat.prefab",
                    EffectLabDemo.Shockwave => project + "prefabs/Enemies/p_Enemy_Bomber.prefab",
                    _ => "" }, HierarchyPath = "", ComponentType = caller,
                    PropertyPath = method, ScriptPath = project + scriptPath, InProduction = production.Contains(path) } }
            });
        }
    }

    private static bool Prefix(int[] prefix, int[] value) => prefix.Length <= value.Length && prefix.SequenceEqual(value.Take(prefix.Length));
    private static IEnumerable<Transform> Ancestors(Transform transform)
    { for (Transform parent = transform.parent; parent != null; parent = parent.parent) yield return parent; }
    private static int[] Indices(Transform child, Transform root)
    {
        var indices = new List<int>();
        for (Transform current = child; current != root; current = current.parent) indices.Add(current.GetSiblingIndex());
        indices.Reverse(); return indices.ToArray();
    }
    private static string Hierarchy(Transform child, Transform root)
    {
        var names = new List<string>();
        for (Transform current = child; current != root; current = current.parent) names.Add(current.name);
        names.Reverse(); return string.Join("/", names);
    }
    private static string Library(string path) => path.StartsWith("Assets/_Project/", StringComparison.Ordinal) ? "Subject#42" :
        path.StartsWith("Assets/Epic Toon FX/", StringComparison.Ordinal) ? "Epic Toon FX" :
        path.StartsWith("Assets/JMO Assets/", StringComparison.Ordinal) ? "Cartoon FX Remaster" : "Assets";
}
#endif
