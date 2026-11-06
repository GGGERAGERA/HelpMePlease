#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CartoonFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Subject42.Dev.VisualEffects
{
    // Editor-only, scene-owned preview. Catalog paths are authored; only a user selection loads content.
    public sealed partial class EffectLabController : MonoBehaviour
    {
        [SerializeField] private EffectLabCatalog catalog;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform inactiveStaging;
        [SerializeField] private Transform effectAnchor;
        [SerializeField] private Transform callerAnchor;
        [SerializeField] private TMP_Text effectCaption;
        [SerializeField] private TMP_Text callerCaption;
        private readonly List<int> filtered = new();
        private readonly List<GameObject> owned = new();
        private readonly List<ProductionPortalVisual> portals = new();
        private readonly List<CorridorNodeView> corridors = new();
        private readonly List<FalseSignalPointView> signals = new();
        private readonly List<(Transform root, Vector3 center)> movingTrails = new();
        private GameObject effectInstance, callerInstance;
        private int selected, callerIndex;
        private float age, nextReplay, previewScale = 1f, replayInterval = 4f;
        private string search = "", error = "";
        private bool loop = true, view3D;
        private int category, scope;
        private Vector2 listScroll;
        public EffectLabCatalog Catalog => catalog;
        public GameObject EffectInstance => effectInstance;
        public GameObject CallerInstance => callerInstance;
        public int CurrentCatalogIndex { get; private set; } = -1;
        public bool Loop { get => loop; set => loop = value; }
        public int FilteredCount => filtered.Count;
        public string SearchQuery { get => search; set { search = value; Refilter(); } }
        public int LiveParticleCount => owned.Where(o => o != null)
            .SelectMany(o => o.GetComponentsInChildren<ParticleSystem>()).Sum(p => p.particleCount);
        public EffectLabCatalog.Entry Current => CurrentCatalogIndex >= 0 ? catalog.Effects[CurrentCatalogIndex] : null;

        private void Start()
        {
            inactiveStaging.gameObject.SetActive(false);
            Refilter();
        }

        private void Update()
        {
            age += Time.deltaTime;
            foreach (var portal in portals)
                if (portal != null) portal.Render(age, 0f, Mathf.Max(0f, .18f - Mathf.Repeat(age, 2f)));
            foreach (var corridor in corridors) if (corridor != null) corridor.Tick(Time.deltaTime);
            foreach (var signal in signals)
                if (signal != null) signal.Render(FalseSignalPointState.Scanning, true, Mathf.Repeat(age, 3f), 3f, signal.transform.position + Vector3.right);
            foreach (var trail in movingTrails)
                if (trail.root != null) trail.root.position = trail.center + new Vector3(Mathf.Sin(age * 2.5f) * 1.5f, Mathf.Sin(age * 5f) * .55f, 0f);
            if (loop && Current != null && Time.unscaledTime >= nextReplay) Replay();
        }

        public void SelectEffect(int index)
        {
            if (index < 0 || index >= catalog.Effects.Length) throw new ArgumentOutOfRangeException(nameof(index));
            CurrentCatalogIndex = index;
            selected = Mathf.Max(0, filtered.IndexOf(index));
            callerIndex = Mathf.Max(0, Array.FindIndex(Current.Callers, c => c.PrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)));
            previewScale = 1f;
            Replay();
        }

        public void Replay()
        {
            StopPreview();
            error = "";
            if (Current == null) return;
            try
            {
                age = 0f;
                effectInstance = CreateEffect(Current, effectAnchor, false);
                var callers = Current.Callers;
                if (callers.Length > 0)
                {
                    var caller = callers[callerIndex % callers.Length];
                    var source = caller.PrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                        ? AssetDatabase.LoadAssetAtPath<GameObject>(caller.PrefabPath) : null;
                    if (source != null)
                    {
                        callerInstance = Clone(source, callerAnchor, true, EffectLabDemo.Asset);
                        FitCaller(callerInstance);
                        var origin = new GameObject("Selected effect at source");
                        origin.transform.SetParent(callerAnchor, false);
                        origin.transform.position = CallerCenter(callerInstance);
                        owned.Add(origin);
                        CreateEffect(Current, origin.transform, true);
                    }
                    else CreateEffect(Current, callerAnchor, true);
                    callerCaption.text = "SOURCE VISUAL · " + (source != null
                        ? System.IO.Path.GetFileNameWithoutExtension(caller.PrefabPath) : caller.ComponentType);
                }
                else if (Current.TransformIndices.Length > 0)
                {
                    var container = AssetDatabase.LoadAssetAtPath<GameObject>(Current.SourceAssetPath);
                    callerInstance = Clone(container, callerAnchor, true, EffectLabDemo.Asset);
                    FitCaller(callerInstance);
                    var origin = new GameObject("Embedded effect at owner");
                    origin.transform.SetParent(callerAnchor, false);
                    origin.transform.position = CallerCenter(callerInstance);
                    owned.Add(origin); CreateEffect(Current, origin.transform, true);
                    callerCaption.text = "OWNER VISUAL · " + container.name;
                }
                else callerCaption.text = "SOURCE · " + (Current.Demo == EffectLabDemo.Asset ? "no serialized caller" : "presentation API");
                effectCaption.text = Current.Demo == EffectLabDemo.Asset ? "FX · " + System.IO.Path.GetFileNameWithoutExtension(Current.SourceAssetPath) : "FX · " + Current.Demo;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
            nextReplay = Time.unscaledTime + replayInterval;
        }

        public void StopPreview()
        {
            portals.Clear(); corridors.Clear(); signals.Clear(); movingTrails.Clear();
            foreach (var instance in owned) if (instance != null) DestroyImmediate(instance);
            owned.Clear();
            effectInstance = callerInstance = null;
        }

        private void OnDisable() => StopPreview();

        private GameObject CreateEffect(EffectLabCatalog.Entry entry, Transform anchor, bool paired)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.SourceAssetPath);
            if (prefab == null) throw new InvalidOperationException("Missing catalog source: " + entry.SourceAssetPath);
            Transform source = prefab.transform;
            foreach (int child in entry.TransformIndices) source = source.GetChild(child);
            var instance = Clone(source.gameObject, anchor, false, entry.Demo);
            instance.transform.localScale *= previewScale;
            switch (entry.Demo)
            {
                case EffectLabDemo.Portal:
                    var portal = instance.GetComponent<ProductionPortalVisual>();
                    portal.Initialize(new Color(.12f, .8f, 1f), 0f); portals.Add(portal); break;
                case EffectLabDemo.EnemyFlash: instance.GetComponentInChildren<EnemyWhiteFlash>(true).Flash(); break;
                case EffectLabDemo.PlayerFlash: instance.GetComponentInChildren<PlayerWhiteFlash>(true).Flash(); break;
                case EffectLabDemo.Relay: instance.GetComponent<OrbitalRelaySpawnFeedback>().Play(); break;
                case EffectLabDemo.Corridor:
                    var corridor = instance.GetComponent<CorridorNodeView>();
                    corridor.SetState(1); corridor.Pulse(); corridors.Add(corridor); break;
                case EffectLabDemo.FalseSignal: signals.Add(instance.GetComponent<FalseSignalPointView>()); break;
            }
            // Presentation demos own their emitters/geometry. Only raw assets need a manual trigger.
            if (entry.Demo != EffectLabDemo.Asset) return instance;
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            var subEmitters = new HashSet<ParticleSystem>();
            foreach (var particle in particles)
                if (particle.subEmitters.enabled)
                    for (int i = 0; i < particle.subEmitters.subEmittersCount; i++)
                        subEmitters.Add(particle.subEmitters.GetSubEmitterSystem(i));
            foreach (var particle in particles)
                if (!subEmitters.Contains(particle) && particle.gameObject.activeInHierarchy) particle.Play(false);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null) { animator.Rebind(); animator.Update(0f); }
            if (instance.GetComponentsInChildren<TrailRenderer>(true).Length > 0)
                movingTrails.Add((instance.transform, instance.transform.position));
            foreach (var line in instance.GetComponentsInChildren<LineRenderer>(true))
            {
                // A line asset has no endpoints until its gameplay owner supplies them.
                if (line.positionCount < 2 || (line.GetPosition(0) - line.GetPosition(line.positionCount - 1)).sqrMagnitude < .001f)
                {
                    line.useWorldSpace = false; line.positionCount = 5;
                    for (int i = 0; i < 5; i++) line.SetPosition(i, new Vector3(-1.5f + i * .75f, i % 2 == 0 ? 0 : .35f, 0));
                }
                else if (line.useWorldSpace)
                {
                    var positions = new Vector3[line.positionCount]; line.GetPositions(positions);
                    for (int i = 0; i < positions.Length; i++) positions[i] = instance.transform.TransformPoint(positions[i]);
                    line.SetPositions(positions);
                }
                line.enabled = true;
            }
            return instance;
        }

        private GameObject Clone(GameObject source, Transform parent, bool caller, EffectLabDemo demo)
        {
            // Parenting in the Instantiate call keeps gameplay Awake/OnEnable from running.
            var instance = Instantiate(source, inactiveStaging, false);
            instance.name = source.name + (caller ? " · source visual" : " · FX preview");
            instance.SetActive(false);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
            {
                if (KeepVisual(behaviour, caller, demo)) continue;
                DestroyImmediate(behaviour);
            }
            foreach (var fx in instance.GetComponentsInChildren<CFXR_Effect>(true))
            {
                fx.clearBehavior = CFXR_Effect.ClearBehavior.None;
                if (fx.cameraShake != null) fx.cameraShake.enabled = false;
            }
            foreach (var fade in instance.GetComponentsInChildren<EpicToonFX.ETFXLightFade>(true)) fade.killAfterLife = false;
            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) { audio.mute = true; audio.playOnAwake = false; audio.enabled = false; }
            foreach (var listener in instance.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(listener);
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) DestroyImmediate(camera);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) { animator.fireEvents = false; animator.applyRootMotion = false; }
            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = previewCamera; }
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main; main.stopAction = ParticleSystemStopAction.None;
                if (caller) main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            foreach (var trail in instance.GetComponentsInChildren<TrailRenderer>(true)) { trail.autodestruct = false; trail.Clear(); if (caller) trail.emitting = false; }
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.SetActive(true);
            owned.Add(instance);
            return instance;
        }

        private static bool KeepVisual(MonoBehaviour component, bool caller, EffectLabDemo demo)
        {
            if (component is Light2D || component is UniversalAdditionalLightData) return true;
            if (component is TMP_Text || component is TMP_SubMesh || component is TMP_SubMeshUI ||
                component is Graphic || component is Mask || component is RectMask2D) return true;
            if (caller) return false;
            if (component is CFXR_Effect || component is CFXR_ParticleText || component is EpicToonFX.ETFXRotation || component is EpicToonFX.ETFXLightFade) return true;
            return demo switch
            {
                EffectLabDemo.Portal => component is ProductionPortalVisual,
                EffectLabDemo.EnemyFlash => component is EnemyWhiteFlash,
                EffectLabDemo.PlayerFlash => component is PlayerWhiteFlash,
                EffectLabDemo.Relay => component is OrbitalRelaySpawnFeedback,
                EffectLabDemo.Corridor => component is CorridorNodeView,
                EffectLabDemo.FalseSignal => component is FalseSignalPointView,
                EffectLabDemo.CatHeart => component is CatHeartFx,
                EffectLabDemo.Shockwave => component is ShockwaveFx,
                _ => false
            };
        }

        private static Bounds RenderBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(r => r is not ParticleSystemRenderer).ToArray();
            var bounds = new Bounds(instance.transform.position, Vector3.one);
            if (renderers.Length == 0) return bounds;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void FitCaller(GameObject instance)
        {
            var bounds = RenderBounds(instance);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, .1f);
            instance.transform.localScale *= 3.2f / size;
            bounds = RenderBounds(instance);
            instance.transform.position -= bounds.center - instance.transform.parent.position;
        }

        private static Vector3 CallerCenter(GameObject instance) => RenderBounds(instance).center;

        private void Refilter()
        {
            filtered.Clear();
            for (int i = 0; i < catalog.Effects.Length; i++)
            {
                var entry = catalog.Effects[i];
                if (scope == 1 && !entry.InProduction || scope == 2 && entry.InProduction) continue;
                if (category > 0 && (int)entry.Kind != category - 1) continue;
                string haystack = entry.DisplayName + " " + entry.SourceAssetPath + " " + entry.Library + " " +
                    string.Join(" ", entry.Callers.Select(c => c.PrefabPath + " " + c.ComponentType));
                if (search.Length > 0 && haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                filtered.Add(i);
            }
            selected = 0; listScroll = Vector2.zero;
            if (filtered.Contains(CurrentCatalogIndex)) selected = filtered.IndexOf(CurrentCatalogIndex);
            else if (filtered.Count > 0) SelectEffect(filtered[0]);
            else { CurrentCatalogIndex = -1; StopPreview(); }
        }

        public void FitEffect()
        {
            if (effectInstance == null) return;
            var renderers = effectInstance.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            previewScale = Mathf.Clamp(previewScale * 4.5f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .1f), .005f, 20f);
            Replay();
        }
    }
}
#endif
