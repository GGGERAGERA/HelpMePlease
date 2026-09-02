using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Subject42.Prototype.OrbitalCombatLab.Integration
{
    public enum OrbitalIntegrationPreset { Start, Mid, Final }
    public enum OrbitalCompatibilityProfile
    {
        BaseWorld,
        Darkness,
        Rain,
        Cold,
        Golden,
        AnomalyArc,
        AnomalyGravity,
        AnomalyBeam
    }

    /// <summary>
    /// Sandbox-only bridge. It reads production runtime objects, but never writes
    /// progression, saves, level-up state, rules, anomalies or scene content.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrbitalIntegrationSandboxAdapter : MonoBehaviour
    {
        [SerializeField] private OrbitalCombatLabController lab;
        [SerializeField] private CharacterMovement2D productionPlayer;
        [SerializeField] private Camera productionCamera;
        [SerializeField] private bool integrationEnabled = true;
        [SerializeField] private bool showDebugUi = true;
        [SerializeField] private OrbitalIntegrationPreset preset = OrbitalIntegrationPreset.Start;
        [SerializeField] private OrbitalCompatibilityProfile profile = OrbitalCompatibilityProfile.BaseWorld;

        private float ringAlpha = .78f;
        private float linkBrightness = 1f;
        private float coreBrightness = 1f;
        private float arcBrightness = 1f;
        private float effectsSaturation = 1f;
        private bool compensateEnvironment = true;
        private bool cameraCaptured;
        private int temporaryCameraMode;
        private Vector3 productionCameraPosition;
        private float productionCameraSize;
        private Rect panel = new(Screen.width - 410f, 16f, 394f, 690f);
        private GUIStyle header;
        private GUIStyle small;
        private bool qaMatrixRunning;
#if UNITY_EDITOR
        private WorldRuleData[] qaWorldRules;
#endif

        public void ConfigureEditor(OrbitalCombatLabController controller,
            CharacterMovement2D player, Camera camera)
        {
            lab = controller;
            productionPlayer = player;
            productionCamera = camera;
        }

        private void Awake()
        {
            if (productionPlayer == null)
                productionPlayer = FindFirstObjectByType<CharacterMovement2D>(FindObjectsInactive.Include);
            if (productionCamera == null) productionCamera = Camera.main;
            if (lab == null) lab = GetComponent<OrbitalCombatLabController>();
#if UNITY_EDITOR
            string[] ruleGuids = UnityEditor.AssetDatabase.FindAssets("t:WorldRuleData",
                new[] { "Assets/_Project/Scriptable Objects/WorldRules" });
            qaWorldRules = new WorldRuleData[ruleGuids.Length];
            for (int i = 0; i < ruleGuids.Length; i++)
                qaWorldRules[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldRuleData>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(ruleGuids[i]));
#endif
        }

        private void OnEnable()
        {
            Camera.onPreCull += HandleCameraPreCull;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        private void Start()
        {
            FindAndBindProductionPlayer();
            CaptureCamera();
            ApplyPreset(preset);
            ApplyProfile(profile);
            SetIntegrationEnabled(integrationEnabled);
        }

        private void Update()
        {
            if (productionPlayer == null || lab == null || !lab.HasIntegrationPlayer)
                FindAndBindProductionPlayer();
            if (Input.GetKeyDown(KeyCode.F1)) showDebugUi = !showDebugUi;
            if (Input.GetKeyDown(KeyCode.F2)) ApplyPreset(OrbitalIntegrationPreset.Start);
            if (Input.GetKeyDown(KeyCode.F3)) ApplyPreset(OrbitalIntegrationPreset.Mid);
            if (Input.GetKeyDown(KeyCode.F4)) ApplyPreset(OrbitalIntegrationPreset.Final);
            if (Input.GetKeyDown(KeyCode.F5)) SetCameraMode(true);
            if (Input.GetKeyDown(KeyCode.F6)) RestoreProductionCamera();
            if (Input.GetKeyDown(KeyCode.F8)) CaptureQaScreenshot();
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F9) && !qaMatrixRunning) StartCoroutine(RunQaMatrix());
#endif
            if (Input.GetKeyDown(KeyCode.Tab) && integrationEnabled)
                SetCameraMode(true);
            if (Input.GetKeyUp(KeyCode.Tab)) RestoreProductionCamera();
        }

        private void LateUpdate()
        {
            ApplyTemporaryCameraFrame();
        }

        private void ApplyTemporaryCameraFrame()
        {
            if (temporaryCameraMode == 0 || productionCamera == null || lab == null) return;
            float full = Mathf.Max(4.8f, lab.OuterRingRadius + 1.35f);
            float size = temporaryCameraMode == 2 ? full : Mathf.Min(full, 13f);
            productionCamera.orthographicSize = size;
            Vector2 center = lab.PlayerPosition;
            productionCamera.transform.position = new Vector3(center.x, center.y,
                productionCamera.transform.position.z);
        }

        private void OnDisable()
        {
            Camera.onPreCull -= HandleCameraPreCull;
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RestoreProductionCamera();
        }

        private void HandleCameraPreCull(Camera renderingCamera)
        {
            if (renderingCamera == productionCamera) ApplyTemporaryCameraFrame();
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera == productionCamera) ApplyTemporaryCameraFrame();
        }

        private void OnGUI()
        {
            if (!showDebugUi) return;
            EnsureStyles();
            panel = GUI.Window(GetInstanceID(), panel, DrawWindow, "ORBITAL INTEGRATION SANDBOX");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("ISOLATED COPY · GOLDEN PROGRESSION OFF", header);
            GUILayout.Label("F1 UI · F2/F3/F4 PRESETS · F5 FULL · F6 CAMERA · F8 SHOT · F9 MATRIX", small);
            GUILayout.Space(5f);

            bool nextEnabled = GUILayout.Toggle(integrationEnabled, " ORBITAL INTEGRATION ENABLED");
            if (nextEnabled != integrationEnabled) SetIntegrationEnabled(nextEnabled);

            GUILayout.Label("STATION PRESET", header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("START")) ApplyPreset(OrbitalIntegrationPreset.Start);
            if (GUILayout.Button("MID")) ApplyPreset(OrbitalIntegrationPreset.Mid);
            if (GUILayout.Button("FINAL")) ApplyPreset(OrbitalIntegrationPreset.Final);
            GUILayout.EndHorizontal();

            GUILayout.Label("COMPATIBILITY PROFILE", header);
            DrawProfileRow(OrbitalCompatibilityProfile.BaseWorld, "BASE", OrbitalCompatibilityProfile.Darkness, "DARKNESS");
            DrawProfileRow(OrbitalCompatibilityProfile.Rain, "RAIN", OrbitalCompatibilityProfile.Cold, "COLD");
            DrawProfileRow(OrbitalCompatibilityProfile.Golden, "GOLDEN", OrbitalCompatibilityProfile.AnomalyArc, "ARC");
            DrawProfileRow(OrbitalCompatibilityProfile.AnomalyGravity, "GRAVITY", OrbitalCompatibilityProfile.AnomalyBeam, "BEAM");

            compensateEnvironment = GUILayout.Toggle(compensateEnvironment, " Environment compensation");
            Slider("Ring alpha", ref ringAlpha, .15f, 1f);
            Slider("Link brightness", ref linkBrightness, .35f, 2f);
            Slider("Core brightness", ref coreBrightness, .35f, 2f);
            Slider("Arc brightness", ref arcBrightness, .35f, 2f);
            Slider("Effects saturation", ref effectsSaturation, .15f, 1.5f);
            if (GUILayout.Button("APPLY VISUAL TUNING")) ApplyProfile(profile);

            GUILayout.Label("CAMERA (TEMPORARY)", header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PRODUCTION")) RestoreProductionCamera();
            if (GUILayout.Button("COMBAT FOCUS")) SetCameraMode(false);
            if (GUILayout.Button("FULL STATION")) SetCameraMode(true);
            GUILayout.EndHorizontal();

            GUILayout.Label("LIVE PRODUCTION CONNECTION", header);
            GUILayout.Label(BuildDiagnostics(), small);
            GUILayout.Space(2f);
            if (GUILayout.Button("CAPTURE QA SCREENSHOT (F8)")) CaptureQaScreenshot();
#if UNITY_EDITOR
            GUI.enabled = !qaMatrixRunning;
            if (GUILayout.Button(qaMatrixRunning ? "QA MATRIX RUNNING…" : "RUN REQUIRED QA MATRIX (F9)"))
                StartCoroutine(RunQaMatrix());
            GUI.enabled = true;
#endif
            GUI.DragWindow(new Rect(0f, 0f, panel.width, 24f));
        }

        private void DrawProfileRow(OrbitalCompatibilityProfile left, string leftLabel,
            OrbitalCompatibilityProfile right, string rightLabel)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(leftLabel)) ApplyProfile(left);
            if (GUILayout.Button(rightLabel)) ApplyProfile(right);
            GUILayout.EndHorizontal();
        }

        private void Slider(string label, ref float value, float minimum, float maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.00}", GUILayout.Width(156f));
            value = GUILayout.HorizontalSlider(value, minimum, maximum);
            GUILayout.EndHorizontal();
        }

        private void ApplyPreset(OrbitalIntegrationPreset next)
        {
            if (lab == null) return;
            preset = next;
            switch (preset)
            {
                case OrbitalIntegrationPreset.Start: lab.ApplyIntegrationStart(); break;
                case OrbitalIntegrationPreset.Mid: lab.ApplyIntegrationMid(); break;
                case OrbitalIntegrationPreset.Final: lab.ApplyIntegrationFinal(); break;
            }
            ApplyProfile(profile);
        }

        private void ApplyProfile(OrbitalCompatibilityProfile next)
        {
            if (lab == null) return;
            profile = next;
            Color ring = new(.16f, .9f, 1f, 1f);
            Color link = new(1f, .08f, .84f, 1f);
            float environmentBoost = compensateEnvironment ? 1f : .82f;

            switch (profile)
            {
                case OrbitalCompatibilityProfile.Darkness:
                    ring = new Color(.22f, 1f, 1f); link = new Color(1f, .18f, .92f); environmentBoost = 1.32f; break;
                case OrbitalCompatibilityProfile.Rain:
                    ring = new Color(.18f, 1f, .82f); link = new Color(1f, .12f, .72f); environmentBoost = 1.12f; break;
                case OrbitalCompatibilityProfile.Cold:
                    ring = new Color(.12f, .8f, 1f); link = new Color(1f, .24f, .82f); environmentBoost = 1.16f; break;
                case OrbitalCompatibilityProfile.Golden:
                    ring = new Color(.12f, 1f, .92f); link = new Color(.94f, .2f, 1f); environmentBoost = 1.18f; break;
                case OrbitalCompatibilityProfile.AnomalyArc:
                    ring = new Color(.12f, 1f, .76f); link = new Color(1f, .12f, .64f); environmentBoost = 1.16f; break;
                case OrbitalCompatibilityProfile.AnomalyGravity:
                    ring = new Color(.18f, .94f, 1f); link = new Color(1f, .12f, .5f); environmentBoost = 1.22f; break;
                case OrbitalCompatibilityProfile.AnomalyBeam:
                    ring = new Color(.08f, 1f, .86f); link = new Color(.88f, .14f, 1f); environmentBoost = 1.2f; break;
            }

            ring = Saturate(ring, effectsSaturation);
            link = Saturate(link, effectsSaturation) * (linkBrightness * environmentBoost);
            ring.a = 1f;
            link.a = 1f;
            lab.RingAlpha = Mathf.Clamp01(ringAlpha * environmentBoost);
            lab.LinkAlpha = Mathf.Clamp01(environmentBoost);
            lab.Links.LineColor = link;
            lab.Core.PulseBrightness = coreBrightness * environmentBoost;
            lab.ArcSystem.VisualBrightness = arcBrightness * environmentBoost;
            lab.WeaponVisuals.EffectIntensity = Mathf.Clamp(.55f * environmentBoost, .2f, 1.2f);
            for (int i = 0; i < lab.RingCount; i++) lab.Rings[i].Settings.Color = ring;
        }

        private void SetIntegrationEnabled(bool enabled)
        {
            integrationEnabled = enabled;
            lab?.SetIntegrationPresentationActive(enabled);
            if (!enabled) RestoreProductionCamera();
        }

        private void FindAndBindProductionPlayer()
        {
            if (productionPlayer == null)
                productionPlayer = FindFirstObjectByType<CharacterMovement2D>(FindObjectsInactive.Include);
            if (productionPlayer != null) lab?.BindIntegrationPlayer(productionPlayer.transform);
        }

        private void SetCameraMode(bool fullStation)
        {
            if (lab == null || productionCamera == null) return;
            CaptureCamera();
            lab.IntegrationCameraOverride = true;
            temporaryCameraMode = fullStation ? 2 : 1;
            lab.CameraRig.Mode = fullStation ? OrbitalCameraMode.FullStation : OrbitalCameraMode.CombatFocus;
            lab.CameraRig.Snap(lab.PlayerPosition, lab.OuterRingRadius);
            Debug.Log($"[OrbitalIntegration] Camera override: {(fullStation ? "FULL" : "COMBAT")}; " +
                $"radius={lab.OuterRingRadius:0.0}");
        }

        private void CaptureCamera()
        {
            if (cameraCaptured || productionCamera == null) return;
            productionCameraPosition = productionCamera.transform.position;
            productionCameraSize = productionCamera.orthographicSize;
            cameraCaptured = true;
        }

        private void RestoreProductionCamera()
        {
            if (lab != null) lab.IntegrationCameraOverride = false;
            temporaryCameraMode = 0;
            if (!cameraCaptured || productionCamera == null) return;
            productionCamera.transform.position = productionCameraPosition;
            productionCamera.orthographicSize = productionCameraSize;
            Debug.Log("[OrbitalIntegration] Camera restored to production controller.");
        }

        private string BuildDiagnostics()
        {
            string rule = WorldRuleController.Instance != null && WorldRuleController.Instance.ActiveRule != null
                ? WorldRuleController.Instance.ActiveRule.RuleType.ToString().ToUpperInvariant()
                : "NONE";
            string anomaly = LevelAnomalyController.Instance != null && LevelAnomalyController.Instance.ActiveAnomaly != null
                ? LevelAnomalyController.Instance.ActiveAnomaly.name
                : "NONE";
            int enemies = EnemyHealth.ActiveInstances.Count;
            int breakables = WorldBreakable.ActiveInstances.Count;
            int sites = ProductionAnomalySite.ActiveSites.Count;
            int exits = ProductionSectorExit.ActiveExits.Count;
            return
                $"Player: {(productionPlayer != null ? productionPlayer.name : "MISSING")}\n" +
                $"Enemies: {enemies} · Breakables: {breakables} · Exits: {exits}\n" +
                $"World rule: {rule} · Anomaly: {anomaly} · Sites: {sites}\n" +
                $"Preset: {preset.ToString().ToUpperInvariant()} · Rings: {lab?.RingCount ?? 0} · Objects: {lab?.MountedCount ?? 0}\n" +
                $"Station radius: {lab?.OuterRingRadius ?? 0f:0.0} · Camera size: {(productionCamera != null ? productionCamera.orthographicSize : 0f):0.0} · Temp camera: {temporaryCameraMode}\n" +
                $"Orbital attacks: shots {lab?.Stats.Shots ?? 0} · arc hits {lab?.Stats.ArcHits ?? 0} · link hits {lab?.Stats.LinkHits ?? 0}\n" +
                $"Profile: {profile.ToString().ToUpperInvariant()} · Golden flow: OFF\n" +
                "Writes: orbital runtime visuals only; production state untouched";
        }

        private void CaptureQaScreenshot()
            => CaptureQaScreenshot($"{preset}_{profile}");

        private void CaptureQaScreenshot(string label)
        {
            string folder = Path.Combine(Application.dataPath,
                "_Project/Prototype/OrbitalCombatLab/Integration/QA");
            Directory.CreateDirectory(folder);
            string safe = label.Replace(' ', '_').Replace('/', '-');
            string file = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safe}.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, file));
            Debug.Log($"[OrbitalIntegration] QA screenshot queued: {file}");
        }

#if UNITY_EDITOR
        private IEnumerator RunQaMatrix()
        {
            if (qaMatrixRunning || WorldRuleController.Instance == null) yield break;
            qaMatrixRunning = true;
            OrbitalIntegrationPreset savedPreset = preset;
            OrbitalCompatibilityProfile savedProfile = profile;
            WorldRuleData savedRule = WorldRuleController.Instance.ActiveRule;
            (OrbitalIntegrationPreset preset, WorldRuleType rule, OrbitalCompatibilityProfile profile)[] matrix =
            {
                (OrbitalIntegrationPreset.Start, WorldRuleType.None, OrbitalCompatibilityProfile.BaseWorld),
                (OrbitalIntegrationPreset.Start, WorldRuleType.Darkness, OrbitalCompatibilityProfile.Darkness),
                (OrbitalIntegrationPreset.Mid, WorldRuleType.None, OrbitalCompatibilityProfile.BaseWorld),
                (OrbitalIntegrationPreset.Mid, WorldRuleType.Darkness, OrbitalCompatibilityProfile.Darkness),
                (OrbitalIntegrationPreset.Mid, WorldRuleType.Rain, OrbitalCompatibilityProfile.Rain),
                (OrbitalIntegrationPreset.Mid, WorldRuleType.Snow, OrbitalCompatibilityProfile.Cold),
                (OrbitalIntegrationPreset.Mid, WorldRuleType.Golden, OrbitalCompatibilityProfile.Golden),
                (OrbitalIntegrationPreset.Final, WorldRuleType.None, OrbitalCompatibilityProfile.BaseWorld),
                (OrbitalIntegrationPreset.Final, WorldRuleType.Darkness, OrbitalCompatibilityProfile.Darkness),
                (OrbitalIntegrationPreset.Final, WorldRuleType.Rain, OrbitalCompatibilityProfile.Rain),
                (OrbitalIntegrationPreset.Final, WorldRuleType.Snow, OrbitalCompatibilityProfile.Cold),
                (OrbitalIntegrationPreset.Final, WorldRuleType.Golden, OrbitalCompatibilityProfile.Golden)
            };

            for (int i = 0; i < matrix.Length; i++)
            {
                ApplyPreset(matrix[i].preset);
                ApplyQaWorldRule(matrix[i].rule);
                ApplyProfile(matrix[i].profile);
                yield return new WaitForSecondsRealtime(.75f);
                CaptureQaScreenshot($"MATRIX_{matrix[i].preset}_{matrix[i].rule}");
                yield return new WaitForSecondsRealtime(.25f);
            }

            ApplyQaWorldRule(WorldRuleType.None);
            ApplyPreset(OrbitalIntegrationPreset.Final);
            OrbitalCompatibilityProfile[] anomalies =
            {
                OrbitalCompatibilityProfile.AnomalyGravity,
                OrbitalCompatibilityProfile.AnomalyArc,
                OrbitalCompatibilityProfile.AnomalyBeam
            };
            foreach (OrbitalCompatibilityProfile anomalyProfile in anomalies)
            {
                ApplyProfile(anomalyProfile);
                yield return new WaitForSecondsRealtime(.6f);
                CaptureQaScreenshot($"MATRIX_Final_{anomalyProfile}");
                yield return new WaitForSecondsRealtime(.25f);
            }

            if (savedRule != null) WorldRuleController.Instance.Apply(savedRule);
            else WorldRuleController.Instance.Clear();
            ApplyPreset(savedPreset);
            ApplyProfile(savedProfile);
            qaMatrixRunning = false;
            Debug.Log("[OrbitalIntegration] Required QA matrix completed; original runtime world rule restored.");
        }

        private void ApplyQaWorldRule(WorldRuleType type)
        {
            if (WorldRuleController.Instance == null) return;
            if (type == WorldRuleType.None)
            {
                WorldRuleController.Instance.Clear();
                return;
            }
            WorldRuleData data = Array.Find(qaWorldRules, value => value != null && value.RuleType == type);
            if (data != null) WorldRuleController.Instance.Apply(data);
            else Debug.LogWarning($"[OrbitalIntegration] QA rule asset missing: {type}");
        }
#endif

        private static Color Saturate(Color color, float saturation)
        {
            float gray = color.grayscale;
            return new Color(
                Mathf.Lerp(gray, color.r, saturation),
                Mathf.Lerp(gray, color.g, saturation),
                Mathf.Lerp(gray, color.b, saturation), color.a);
        }

        private void EnsureStyles()
        {
            header ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        }
    }
}
