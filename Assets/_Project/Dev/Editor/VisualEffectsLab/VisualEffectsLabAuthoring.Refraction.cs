#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static partial class VisualEffectsLabAuthoring
{
    private enum FloorMode { Off, Copy, Warp, Crowd }
    private static readonly string[] FloorModes = { "spiral", "copy", "warp" };
    private static readonly string[] FloorCrowdModes = { "copy", "warp", "crowd" };
    private static readonly FloorMode[] FloorCrowdSamples = { FloorMode.Copy, FloorMode.Warp, FloorMode.Crowd };
    private static readonly Vector2 CrowdPrimaryOffset = new(3.85f, 0f);
    private static readonly int VisualTimeId = Shader.PropertyToID("_VisualTime");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly List<(MeshRenderer Spiral, MeshRenderer Warp)> floorCrowd = new();
    private static RenderPipelineAsset originalQualityPipeline;
    private static UniversalRenderPipelineAsset floorPipeline;
    private static Renderer2DData floorCaptureRenderer;
    private static FloorProbeFeature floorProbe;
    private static UniversalAdditionalCameraData floorCameraData;
    private static int originalCameraRenderer, floorRendererIndex;
    private static MeshRenderer floorWarp;
    private static Material floorMaterial;
    private static GameObject floorCrowdRoot;
    private static MaterialPropertyBlock crowdSpiralProperties;

    [MenuItem(CombatMenu + "Play combat floor refraction")]
    private static void PlayCombatFloor() => BeginCombatPreview(CombatTrial.Floor);

    [MenuItem(CombatMenu + "Play combat floor refraction (15 vortices)")]
    private static void PlayCombatFloorCrowd() => BeginCombatPreview(CombatTrial.FloorCrowd);

    [MenuItem(CombatMenu + "Play centered gravity vortex")]
    private static void PlayCenteredVortex() => BeginCombatPreview(CombatTrial.CenteredVortex);

    private static void PrepareCenteredVortexPresentation()
    {
        var upgrades = UpgradeManager.Instance;
        var card = upgrades?.DebugCardTemplate;
        var panel = card != null ? card.GetComponentInParent<UpgradePanelView>(true) : null;
        if (panel != null && !panel.gameObject.activeSelf)
        {
            var choices = upgrades.DebugRewardCatalogue.Take(panel.CardViews.Count).ToArray();
            panel.Show(2, choices, null);
            panel.SetConstructorCovered(true);
            Canvas.ForceUpdateCanvases();
            panel.Hide();
        }

        if (AudioService.Instance == null) return;
        using var audioSettings = new SerializedObject(AudioService.Instance);
        if (audioSettings.FindProperty("catalog").objectReferenceValue is not AudioCatalog catalog) return;
        using var catalogSettings = new SerializedObject(catalog);
        var cues = catalogSettings.FindProperty("cues");
        for (int i = 0; i < cues.arraySize; i++)
        {
            var cue = cues.GetArrayElementAtIndex(i);
            var id = (AudioCueId)cue.FindPropertyRelative("id").intValue;
            if (id is not (AudioCueId.XPPickup or AudioCueId.LevelUp)) continue;
            var clips = cue.FindPropertyRelative("clips");
            for (int j = 0; j < clips.arraySize; j++)
                if (clips.GetArrayElementAtIndex(j).objectReferenceValue is AudioClip clip &&
                    clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }
    }

    private static IEnumerator PreviewCenteredVortex(GravityZone zone, CameraFollow cameraFollow)
    {
        Vector2 center = zone.FocusArea.bounds.center;
        if (!cameraFollow.BeginWorldBoundsFocus(zone, center, combatCamera.orthographicSize))
            throw new InvalidOperationException("The gameplay camera could not frame the gravity center.");
        try
        {
            ApplyFloorRefraction(FloorMode.Warp);
            var cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectAssets + "prefabs/Pickups/p_Case1.prefab")
                .GetComponent<WorldBreakable>();
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectAssets + "prefabs/Pickups/p_Hex1.prefab")
                .GetComponent<ExperiencePickup>();
            Object.Instantiate(cratePrefab, center + new Vector2(7, 2), Quaternion.identity);
            Object.Instantiate(pickupPrefab, center + new Vector2(7, -2), Quaternion.identity);
            yield return null;
            yield return null;

            var crates = WorldBreakable.ActiveInstances
                .Where(candidate => candidate.GetComponentInParent<WorldEvent>() == null).ToArray();
            zone.ConfigureDebugVortexPull(crates);
            float followAt = Time.time + .85f;
            while (Time.time < followAt) yield return null;
        }
        finally { cameraFollow.EndWorldBoundsFocus(zone); }
    }

    [MenuItem(CombatMenu + "Combat comparison/5 Floor refraction")]
    private static void ShowCombatFloor()
    {
        RequireCombatReady();
        ApplyCombatShaders(3);
        ApplyFloorRefraction(FloorMode.Warp);
    }

    [MenuItem(CombatMenu + "Combat comparison/6 Floor refraction (15 vortices)")]
    private static void ShowCombatFloorCrowd()
    {
        RequireCombatReady();
        ApplyCombatShaders(3);
        ApplyFloorRefraction(FloorMode.Crowd);
    }

    [MenuItem(CombatMenu + "Combat comparison/Measure floor GPU")]
    private static void MeasureCombatFloor()
    {
        RequireCombatReady();
        StartCombatRoutine(MeasureCombat(CombatTrial.Floor));
    }

    [MenuItem(CombatMenu + "Combat comparison/Measure 15 vortices GPU")]
    private static void MeasureCombatFloorCrowd()
    {
        RequireCombatReady();
        StartCombatRoutine(MeasureCombat(CombatTrial.FloorCrowd));
    }

    private static void EnsureFloorRefraction()
    {
        if (floorPipeline != null) return;
        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset source)
            throw new InvalidOperationException("The floor comparison requires the MVP URP camera.");
        floorCameraData = combatCamera.GetUniversalAdditionalCameraData();
        using var cameraSettings = new SerializedObject(floorCameraData);
        originalCameraRenderer = cameraSettings.FindProperty("m_RendererIndex").intValue;
        using var sourceSettings = new SerializedObject(source);
        int rendererIndex = originalCameraRenderer < 0
            ? sourceSettings.FindProperty("m_DefaultRendererIndex").intValue : originalCameraRenderer;
        if (source.rendererDataList[rendererIndex] is not Renderer2DData sourceRenderer)
            throw new InvalidOperationException("The MVP camera must use a 2D renderer.");

        originalQualityPipeline = QualitySettings.renderPipeline;
        floorCaptureRenderer = Object.Instantiate(sourceRenderer);
        floorCaptureRenderer.hideFlags = HideFlags.HideAndDontSave;
        floorProbe = ScriptableObject.CreateInstance<FloorProbeFeature>();
        floorProbe.hideFlags = HideFlags.HideAndDontSave;
        floorCaptureRenderer.rendererFeatures.Add(floorProbe);
        using (var settings = new SerializedObject(floorCaptureRenderer))
        {
            settings.FindProperty("m_UseCameraSortingLayersTexture").boolValue = true;
            settings.FindProperty("m_CameraSortingLayersTextureBound").intValue = SortingLayer.NameToID("Background");
            settings.FindProperty("m_CameraSortingLayerDownsamplingMethod").enumValueIndex = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        floorPipeline = Object.Instantiate(source);
        floorPipeline.hideFlags = HideFlags.HideAndDontSave;
        using (var settings = new SerializedObject(floorPipeline))
        {
            var renderers = settings.FindProperty("m_RendererDataList");
            floorRendererIndex = renderers.arraySize++;
            renderers.GetArrayElementAtIndex(floorRendererIndex).objectReferenceValue = floorCaptureRenderer;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        floorMaterial = new Material(RequiredShader("Subject42/Dev/CombatFloorRefraction"));
        floorWarp = CreateFloorWarp(gravityRenderer);
        floorWarp.enabled = false;
        QualitySettings.renderPipeline = floorPipeline;
    }

    private static MeshRenderer CreateFloorWarp(Renderer spiral)
    {
        var quad = new GameObject("Gravity floor refraction");
        quad.transform.SetParent(spiral.transform, false);
        quad.AddComponent<MeshFilter>().sharedMesh = spiral.GetComponent<MeshFilter>().sharedMesh;
        var warp = quad.AddComponent<MeshRenderer>();
        warp.sortingLayerName = "Default";
        warp.sortingOrder = short.MinValue;
        warp.shadowCastingMode = ShadowCastingMode.Off;
        warp.receiveShadows = false;
        warp.sharedMaterial = floorMaterial;
        return warp;
    }

    private static void EnsureFloorCrowd()
    {
        if (SessionState.GetInt(TrialSession, 0) == (int)CombatTrial.CenteredVortex)
            throw new InvalidOperationException("Start the 15 vortices trial for the small vortex grid.");
        if (floorCrowdRoot != null) return;
        floorCrowdRoot = new GameObject("Gravity refraction stress visuals");
        floorCrowdRoot.SetActive(false);
        crowdSpiralProperties = new MaterialPropertyBlock();
        crowdSpiralProperties.SetFloat(FadeId, 1f);
        var source = gravityRenderer.transform;
        var mesh = gravityRenderer.GetComponent<MeshFilter>().sharedMesh;
        for (int row = -1; row <= 1; row++)
            for (int col = -2; col <= 2; col++)
            {
                // The real gravity zone occupies the center-right cell.
                Vector2 offset = new Vector2(col * CrowdPrimaryOffset.x, row * 4.45f) - CrowdPrimaryOffset;
                if (offset == Vector2.zero) continue;
                var visual = new GameObject("Gravity spiral");
                visual.transform.SetParent(floorCrowdRoot.transform, false);
                visual.transform.SetPositionAndRotation(source.position + (Vector3)offset, source.rotation);
                visual.transform.localScale = source.lossyScale;
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var spiral = visual.AddComponent<MeshRenderer>();
                spiral.sharedMaterial = previewGravity;
                spiral.sortingLayerID = gravityRenderer.sortingLayerID;
                spiral.sortingOrder = gravityRenderer.sortingOrder;
                spiral.shadowCastingMode = ShadowCastingMode.Off;
                spiral.receiveShadows = false;
                floorCrowd.Add((spiral, CreateFloorWarp(spiral)));
            }
    }

    private static void TickFloorCrowdSpirals()
    {
        if (floorCrowdRoot == null || !floorCrowdRoot.activeInHierarchy) return;
        for (int i = 0; i < floorCrowd.Count; i++)
        {
            crowdSpiralProperties.SetFloat(VisualTimeId, Time.time + (i + 1) * .41f);
            floorCrowd[i].Spiral.SetPropertyBlock(crowdSpiralProperties);
        }
    }

    private static string DescribeFloorCrowd()
    {
        int visible = InViewport(floorWarp) ? 1 : 0;
        foreach (var vortex in floorCrowd)
            if (InViewport(vortex.Warp)) visible++;
        return $"Vortex visuals {floorCrowd.Count + 1}, centers in viewport {visible}, one gameplay gravity zone.\n";

        bool InViewport(MeshRenderer warp)
        {
            Vector3 point = combatCamera.WorldToViewportPoint(warp.transform.position);
            return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }
    }

    private static void CheckFloorCrowdMode(FloorMode mode)
    {
        int expected = mode switch { FloorMode.Crowd => 15, FloorMode.Warp => 1, _ => 0 };
        int active = IsActive(floorWarp) ? 1 : 0;
        foreach (var vortex in floorCrowd)
            if (IsActive(vortex.Warp)) active++;
        if (active != expected)
            throw new InvalidOperationException($"Expected {expected} active floor warps, found {active}.");

        static bool IsActive(MeshRenderer warp) => warp.enabled && warp.gameObject.activeInHierarchy;
    }

    private static void ApplyFloorRefraction(FloorMode mode)
    {
        if (mode == FloorMode.Off && floorPipeline == null) return;
        EnsureFloorRefraction();
        if (mode == FloorMode.Crowd) EnsureFloorCrowd();
        floorCameraData.SetRenderer(mode == FloorMode.Off ? originalCameraRenderer : floorRendererIndex);
        floorWarp.enabled = mode is FloorMode.Warp or FloorMode.Crowd;
        if (floorCrowdRoot != null) floorCrowdRoot.SetActive(mode == FloorMode.Crowd);
    }

    private static IEnumerator MeasureFloorDraw(string output, bool crowd)
    {
        var rows = new List<string> { "draws,gpu_ms" };
        if (SystemInfo.supportsGpuRecorder)
        {
            // Replay the same draw offscreen with URP's captured floor and the live camera.
            // The batch checks the single draw against the GPU timer's resolution.
            var sampler = CustomSampler.Create("Subject42 Floor Refraction Draw", true);
            var recorder = sampler.GetRecorder();
            var matrices = new Matrix4x4[crowd ? floorCrowd.Count + 1 : 1];
            matrices[0] = floorWarp.localToWorldMatrix;
            if (crowd)
                for (int i = 0; i < floorCrowd.Count; i++)
                    matrices[i + 1] = floorCrowd[i].Warp.localToWorldMatrix;
            using var pass = new FloorDrawPass(combatCamera, floorWarp, matrices, sampler);
            try
            {
                recorder.enabled = true;
                floorProbe.Pass = pass;
                foreach (int count in new[] { 1, crowd ? matrices.Length : 24 })
                {
                    pass.Draws = count;
                    for (int i = 0; i < 12; i++) yield return null;
                    for (int i = 0; i < 64; i++)
                    {
                        if (recorder.gpuSampleBlockCount == 1 && recorder.gpuElapsedNanoseconds > 0)
                            rows.Add($"{count},{(recorder.gpuElapsedNanoseconds / 1_000_000.0).ToString("F6", CultureInfo.InvariantCulture)}");
                        yield return null;
                    }
                }
                pass.Draws = matrices.Length;
                yield return null;
                if (pass.Target != null) SaveProbeImage(pass.Target, output + "/draw-check.png");
            }
            finally
            {
                floorProbe.Pass = null;
                recorder.enabled = false;
            }
        }
        File.WriteAllLines(output + "/gpu-draw.csv", rows);
    }

    private sealed class FloorProbeFeature : ScriptableRendererFeature
    {
        public FloorDrawPass Pass { get; set; }

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (Pass != null && data.cameraData.camera == Pass.Camera) renderer.EnqueuePass(Pass);
        }
    }

    private sealed class FloorDrawPass : ScriptableRenderPass, IDisposable
    {
        private static readonly int FloorTextureId = Shader.PropertyToID("_CameraSortingLayerTexture");
        private readonly Mesh mesh;
        private readonly Matrix4x4[] matrices;
        private readonly Material material;
        private readonly CustomSampler sampler;
        private RTHandle target;

        public Camera Camera { get; }
        public int Draws { get; set; }
        public RenderTexture Target => target?.rt;

        public FloorDrawPass(Camera camera, MeshRenderer warp, Matrix4x4[] matrices, CustomSampler sampler)
        {
            Camera = camera;
            mesh = warp.GetComponent<MeshFilter>().sharedMesh;
            this.matrices = matrices;
            material = warp.sharedMaterial;
            this.sampler = sampler;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var descriptor = frame.Get<UniversalCameraData>().cameraTargetDescriptor;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref target, descriptor, name: "Subject42 Floor Refraction Probe");

            var clear = new ImportResourceParams { clearOnFirstUse = true, clearColor = Color.clear };
            using var builder = graph.AddRasterRenderPass<ProbeData>("Floor Refraction Probe", out var data);
            builder.SetRenderAttachment(graph.ImportTexture(target, clear), 0);
            builder.UseGlobalTexture(FloorTextureId);
            builder.AllowPassCulling(false);
            data.Pass = this;
            builder.SetRenderFunc(static (ProbeData sample, RasterGraphContext context) =>
            {
                var pass = sample.Pass;
                context.cmd.BeginSample(pass.sampler);
                for (int i = 0; i < pass.Draws; i++)
                    context.cmd.DrawMesh(pass.mesh, pass.matrices[i % pass.matrices.Length], pass.material, 0, 0);
                context.cmd.EndSample(pass.sampler);
            });
        }

        public void Dispose() => target?.Release();

        private sealed class ProbeData
        {
            public FloorDrawPass Pass;
        }
    }

    private static void SaveProbeImage(RenderTexture target, string path)
    {
        var previous = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
        }
    }

    private static void ReleaseFloorRefraction()
    {
        if (floorCrowdRoot != null) Object.DestroyImmediate(floorCrowdRoot);
        if (floorWarp != null) Object.DestroyImmediate(floorWarp.gameObject);
        if (floorMaterial != null) Object.DestroyImmediate(floorMaterial);
        if (floorCameraData != null) floorCameraData.SetRenderer(originalCameraRenderer);
        if (floorPipeline != null)
        {
            QualitySettings.renderPipeline = originalQualityPipeline;
            Object.DestroyImmediate(floorPipeline);
        }
        if (floorCaptureRenderer != null) Object.DestroyImmediate(floorCaptureRenderer);
        if (floorProbe != null) Object.DestroyImmediate(floorProbe);
        floorWarp = null;
        floorMaterial = null;
        floorPipeline = null;
        floorCameraData = null;
        floorCaptureRenderer = null;
        floorProbe = null;
        floorCrowdRoot = null;
        crowdSpiralProperties = null;
        floorCrowd.Clear();
    }
}
#endif
