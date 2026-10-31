using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Subject42.Combat.OrbitalStation;

namespace Subject42.Visuals
{
    // Scene-owned presentation overrides. The development UI is only a client.
    [DefaultExecutionOrder(32000)]
    public sealed class LiveVisualRuntime : MonoBehaviour
    {
        public static LiveVisualRuntime Instance { get; private set; }
        public VisualTuningPreset Preset { get; private set; }
        public VisualProfile Original { get; private set; } = new();
        public VisualProfile Custom { get; private set; } = new();
        public VisualComparisonMode Mode { get; private set; } = VisualComparisonMode.Current;
        public readonly List<VisualTarget> Targets = new();
        public int Revision { get; private set; }
        public int OriginalMatchedCount { get; private set; }
        public int OriginalUnmatchedCount { get; private set; }
        public event Action Changed;
        private readonly HashSet<int> registered = new();
        private readonly Dictionary<GameObject, string> sources = new();
        private readonly Dictionary<string, string> sourceNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, float>> rules = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, float>> originalRules = new(StringComparer.Ordinal);
        private readonly List<(VisualTarget target, VisualProperty property, float value)> applied = new();
        private readonly List<VisualProfile> undo = new(), redo = new();
        private int orbitalRevision = -1;
        private Action applySavedRingValues, restoreSavedRingValues;
        private bool refreshPending;
        private readonly List<EnemyHealth> pendingEnemies = new();
        private float nextCleanup;
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public static void Attach(ProductionSceneComposition owner, VisualTuningPreset preset)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (preset.LiveProductionProfile == null) return;
#endif
            var runtime = owner.GetComponent<LiveVisualRuntime>() ?? owner.gameObject.AddComponent<LiveVisualRuntime>();
            runtime.Initialize(preset);
        }
        private void Initialize(VisualTuningPreset preset)
        {
            Instance = this; Preset = preset;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (preset.LiveOriginalBaseline != null)
            {
                Original = JsonUtility.FromJson<VisualProfile>(preset.LiveOriginalBaseline.text);
                Original.Validate();
                Index(Original, originalRules);
            }
#endif
            if (preset.LiveProductionProfile != null)
            {
                Custom = JsonUtility.FromJson<VisualProfile>(preset.LiveProductionProfile.text);
                Custom.Validate(); Custom = Custom.ForProduction(); Mode = VisualComparisonMode.Custom;
            }
            foreach (var source in preset.LiveSources)
            {
                if (source.Prefab == null) continue;
                sources[source.Prefab] = source.AssetPath;
                // Name fallback only for scene-instantiated prefab roots discovered during initial registration.
                if (!sourceNames.ContainsKey(source.Prefab.name)) sourceNames.Add(source.Prefab.name, source.AssetPath);
                else sourceNames[source.Prefab.name] = null; // ambiguous names require an explicit factory binding
            }
            EnemyHealth.Spawned += EnemySpawned;
            SceneManager.sceneLoaded += SceneLoaded;
            StartCoroutine(RegisterInitializedScene());
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) { if (scene == gameObject.scene) refreshPending = true; }
        private IEnumerator RegisterInitializedScene()
        {
            yield return null; yield return null;
            RefreshScene();
            // Production composes procedural decorations/station after the player spawn.
            yield return new WaitForSecondsRealtime(1);
            RefreshScene();
        }
        private void EnemySpawned(EnemyHealth enemy) => pendingEnemies.Add(enemy);
        public static void RegisterHierarchy(GameObject root, GameObject sourcePrefab = null)
        {
            if (Instance == null || root == null) return;
            var runtime = Instance;
            string source = null;
            if (sourcePrefab != null) runtime.sources.TryGetValue(sourcePrefab, out source);
#if UNITY_EDITOR
            if (source == null && sourcePrefab != null) source = UnityEditor.AssetDatabase.GetAssetPath(sourcePrefab);
#endif
            if (string.IsNullOrEmpty(source)) runtime.sourceNames.TryGetValue(root.name.Replace("(Clone)", "").Trim(), out source);
            runtime.RegisterRoot(root, source);
        }

        public static string RelativePath(Transform transform, Transform root)
        {
            if (transform == root) return "";
            var parts = new List<string>();
            for (var item = transform; item != null && item != root; item = item.parent)
                parts.Add(item.name + "[" + item.GetSiblingIndex() + "]");
            parts.Reverse(); return string.Join("/", parts);
        }
        public static void ApplyHierarchy(GameObject root)
        {
            var runtime = Instance;
            if (runtime == null || root == null) return;
            if (runtime.refreshPending) { runtime.refreshPending = false; runtime.Rebuild(); }
            foreach (var item in runtime.applied)
                if (item.target.IsAlive && item.target.Component != null && item.target.Component.transform.IsChildOf(root.transform))
                { item.target.Maintain?.Invoke(); item.property.Write(item.value); }
        }
        public static void CaptureConfiguredHierarchy(GameObject root)
        {
            var runtime = Instance;
            if (runtime == null || root == null) return;
            foreach (var target in runtime.Targets)
            {
                if (!target.IsAlive || target.Component == null || !target.Component.transform.IsChildOf(root.transform)) continue;
                foreach (var property in target.Properties)
                    if (property.Id is "particles.size" or "particles.lifetime" or "particles.speed" or "particles.emission" or "trail.width")
                        property.CaptureConfiguredCurrent();
            }
        }
        public void RefreshScene()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects()) RegisterRoot(root, null);
            BindOrbital();
            RemoveDeadTargets();
            Rebuild();
        }
        private void RemoveDeadTargets()
        {
            for (int i = Targets.Count - 1; i >= 0; i--)
            {
                var target = Targets[i];
                if (target.IsAlive) continue;
                target.Release?.Invoke();
                if (int.TryParse(target.Key, out int id)) registered.Remove(id);
                Targets.RemoveAt(i); refreshPending = true;
            }
        }
        private void BindOrbital()
        {
            if (!OrbitalPresentationConfig.IsConfigured || orbitalRevision == OrbitalPresentationConfig.ConfigurationRevision) return;
            orbitalRevision = OrbitalPresentationConfig.ConfigurationRevision;
            Targets.RemoveAll(t => t.TypeKey == "config:orbital");
            Targets.Add(OrbitalVisualAdapter.CreateTarget());
        }
        private void RegisterRoot(GameObject root, string explicitSource)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || registered.Contains(component.GetInstanceID()) ||
                    component is not Renderer && component is not Light2D && component is not Light &&
                    component is not ParticleSystem && component is not Camera && component is not Volume) continue;
                // uGUI Image/TMP UI use CanvasRenderer and are not supported targets here.
                // World VFX may be parked under an owner's Canvas while pooled: do not discard them.
                if (component is LineRenderer && component.GetComponentInParent<OrbitalStationView>() != null)
                    continue; // station line ownership is exposed by the ORBITAL/config adapter
                Transform sourceRoot = root.transform;
                string source = explicitSource;
                if (string.IsNullOrEmpty(source))
                {
#if UNITY_EDITOR
                    var prefabRoot = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
                    if (prefabRoot != null)
                    {
                        sourceRoot = prefabRoot.transform;
                        source = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
                    }
#endif
                    if (string.IsNullOrEmpty(source))
                    {
                        for (var node = component.transform; node != null; node = node.parent)
                        {
                            if (!sourceNames.TryGetValue(node.name.Replace("(Clone)", "").Trim(), out var candidate) || string.IsNullOrEmpty(candidate)) continue;
                            source = candidate; sourceRoot = node; break;
                        }
                    }
                }
                string path;
                if (string.IsNullOrEmpty(source))
                {
                    source = root.scene.path;
                    path = root.name + "[" + root.transform.GetSiblingIndex() + "]";
                    string relative = RelativePath(component.transform, root.transform);
                    if (relative.Length > 0) path += "/" + relative;
                }
                else path = RelativePath(component.transform, sourceRoot);
                int ordinal = Array.IndexOf(component.GetComponents(component.GetType()), component);
                string typeKey = source + "::" + path + "::" + component.GetType().Name + ":" + ordinal;
                string group = GroupFor(component);
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
                if (!Custom.Values.Any(value => value.Scope == "Type" && value.Target == typeKey || value.Scope == "Group" && value.Target == group)) continue;
#endif
                var target = new VisualTarget(component.GetInstanceID().ToString(), typeKey, group,
                    component.gameObject.name + " · " + component.GetType().Name, component);
                VisualComponentAdapters.Populate(target, Preset, Original);
                if (target.Properties.Count == 0) continue;
                Targets.Add(target); registered.Add(component.GetInstanceID());
                target.SetComparison?.Invoke(Mode);
            }
            refreshPending = true;
        }
        private static string GroupFor(Component component)
        {
            var enemy = component.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                if (enemy.IsBoss) return "Враги / Боссы";
                var identity = enemy.GetComponent<EnemyIdentity>();
                if (identity != null && identity.EnemyId != null && identity.EnemyId.StartsWith("Elite_", StringComparison.OrdinalIgnoreCase)) return "Враги / Элита";
                if (enemy.GetComponent<EnemyBomberMovement>() != null) return "Враги / Подрывники";
                if (enemy.GetComponent<EnemyShooterMovement>() != null) return "Враги / Стрелки";
                if (enemy.GetComponent<TurretEnemyBehaviour>() != null) return "Враги / Турели";
                if (enemy.GetComponent<EyesEnemyBehaviour>() != null) return "Враги / Глаза";
                return "Враги / Обычные";
            }
            if (component is Light2D || component is Light) return "Освещение";
            if (component is ParticleSystem || component is ParticleSystemRenderer) return "Частицы";
            if (component is Camera || component is Volume) return "Камера и post-processing";
            if (component.GetComponentInParent<OrbitalStationView>() != null) return "ORBITAL";
            if (component.GetComponentInParent<ProductionAnomalySite>() != null) return "Предупреждения и аномалии";
            return "Декор и окружение";
        }
        private void LateUpdate()
        {
            BindOrbital();
            foreach (var enemy in pendingEnemies) if (enemy != null) RegisterHierarchy(enemy.gameObject);
            pendingEnemies.Clear();
            if (Time.unscaledTime >= nextCleanup)
            {
                nextCleanup = Time.unscaledTime + .5f;
                RemoveDeadTargets();
            }
            if (refreshPending) { refreshPending = false; Rebuild(); }
            if (Mode == VisualComparisonMode.Original)
                foreach (var target in Targets) if (target.IsAlive && target.IsActive) target.Maintain?.Invoke();
            foreach (var item in applied)
                if (item.target.IsAlive && item.target.IsActive) { item.target.Maintain?.Invoke(); item.property.Write(item.value); }
            applySavedRingValues?.Invoke();
        }
        private static string RuleKey(string scope, string target) => scope + "\n" + target;
        private static void Index(VisualProfile profile, Dictionary<string, Dictionary<string, float>> index)
        {
            index.Clear();
            foreach (var value in profile.Values)
            {
                string key = RuleKey(value.Scope, value.Target);
                if (!index.TryGetValue(key, out var values)) index[key] = values = new Dictionary<string, float>(StringComparer.Ordinal);
                values[value.Property] = value.Value;
            }
        }
        public bool TryOriginal(VisualTarget target, VisualProperty property, out float value)
        {
            return originalRules.TryGetValue(RuleKey("Type", target.TypeKey), out var values) && values.TryGetValue(property.Id, out value)
                || Original.TryGet("Group", target.Group, property.Id, out value);
        }
        private void Rebuild()
        {
            restoreSavedRingValues?.Invoke();
            applySavedRingValues = restoreSavedRingValues = null;
            rules.Clear(); applied.Clear();
            var baselineKeys = new HashSet<string>();
            var profile = Mode == VisualComparisonMode.Original ? Original : Mode == VisualComparisonMode.Custom ? Custom : null;
            if (profile != null) Index(profile, rules);
            foreach (var target in Targets)
            {
                if (!target.IsAlive) continue;
                rules.TryGetValue(RuleKey("Instance", target.Key), out var instanceRules);
                rules.TryGetValue(RuleKey("Type", target.TypeKey), out var typeRules);
                rules.TryGetValue(RuleKey("Group", target.Group), out var groupRules);
                originalRules.TryGetValue(RuleKey("Type", target.TypeKey), out var baselineType);
                foreach (var property in target.Properties)
                {
                    if (baselineType != null && baselineType.ContainsKey(property.Id))
                        baselineKeys.Add(target.TypeKey + "\n" + property.Id);
                    float value = 0;
                    bool has = instanceRules != null && instanceRules.TryGetValue(property.Id, out value) ||
                        typeRules != null && typeRules.TryGetValue(property.Id, out value) ||
                        groupRules != null && groupRules.TryGetValue(property.Id, out value);
                    if (has)
                    {
                        value = property.Sanitize(value); property.Touched = true;
                        applied.Add((target, property, value)); property.Write(value);
                    }
                    else if (property.Touched) { property.RestoreCurrent(); property.Touched = false; }
                }
            }
            var savedRings = OrbitalVisualAdapter.BindSavedRingValues(profile, Original);
            applySavedRingValues = savedRings.apply; restoreSavedRingValues = savedRings.restore;
            applySavedRingValues?.Invoke();
            OriginalMatchedCount = baselineKeys.Count + savedRings.originalMatches;
            OriginalUnmatchedCount = Original.Values.Count - OriginalMatchedCount;
            Revision++; Changed?.Invoke();
        }
        public void SetMode(VisualComparisonMode mode)
        {
            // Restore captured values before changing the material/config source.
            foreach (var target in Targets)
            {
                if (!target.IsAlive) continue;
                foreach (var property in target.Properties)
                    if (property.Touched) { property.RestoreCurrent(); property.Touched = false; }
                target.SetComparison?.Invoke(mode);
            }
            Mode = mode; Rebuild();
        }
        public void BeginEdit()
        {
            undo.Add(Custom.Copy()); if (undo.Count > 64) undo.RemoveAt(0); redo.Clear();
            if (Mode != VisualComparisonMode.Custom) SetMode(VisualComparisonMode.Custom);
        }
        public void Edit(string scope, VisualTarget target, VisualProperty property, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Введите конечное число.");
            // A deliberate edit of the wider scope must be immediately visible on its members.
            // Undo keeps the narrower settings that the user replaces with this action.
            if (scope == "Group" || scope == "Type")
            {
                var members = Targets.Where(t => scope == "Group" ? t.Group == target.Group : t.TypeKey == target.TypeKey).ToArray();
                var types = new HashSet<string>(members.Select(t => t.TypeKey));
                var instances = new HashSet<string>(members.Select(t => t.Key));
                Custom.Values.RemoveAll(v => v.Property == property.Id &&
                    (v.Scope == "Instance" && instances.Contains(v.Target) || scope == "Group" && v.Scope == "Type" && types.Contains(v.Target)));
            }
            Custom.Set(scope, ScopeKey(scope, target), property.Id, property.Sanitize(value)); Rebuild();
        }
        public static string ScopeKey(string scope, VisualTarget target) => scope == "Instance" ? target.Key : scope == "Group" ? target.Group : target.TypeKey;
        public void Reset(string scope, VisualTarget target = null, string property = null, string category = null)
        {
            BeginEdit();
            Custom.Values.RemoveAll(v => (target == null || v.Scope == scope && v.Target == ScopeKey(scope, target)) &&
                (property == null || v.Property == property) &&
                (category == null || target.Properties.Any(p => p.Category == category && p.Id == v.Property)));
            Rebuild();
        }
        public void Undo()
        {
            if (!CanUndo) return;
            redo.Add(Custom.Copy()); Custom = undo[^1]; undo.RemoveAt(undo.Count - 1); SetMode(VisualComparisonMode.Custom);
        }
        public void Redo()
        {
            if (!CanRedo) return;
            undo.Add(Custom.Copy()); Custom = redo[^1]; redo.RemoveAt(redo.Count - 1); SetMode(VisualComparisonMode.Custom);
        }
        public void Load(VisualProfile profile)
        {
            profile.Validate(); BeginEdit(); Custom = profile.Copy(); SetMode(VisualComparisonMode.Custom);
        }
        public string Export(string name)
        {
            var profile = Custom.ForProduction(); profile.Name = name; profile.BaselineRevision = Original.BaselineRevision;
            return JsonUtility.ToJson(profile, true);
        }
        private void OnDestroy()
        {
            restoreSavedRingValues?.Invoke();
            EnemyHealth.Spawned -= EnemySpawned; SceneManager.sceneLoaded -= SceneLoaded;
            foreach (var target in Targets)
            {
                if (!target.IsAlive) { target.Release?.Invoke(); continue; }
                foreach (var property in target.Properties) if (property.Touched) property.RestoreCurrent();
                target.Release?.Invoke();
            }
            if (Instance == this) Instance = null;
        }
    }
}
