#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Subject42.Combat.Telekinetic;
using Subject42.Dev.VisualEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class VisualEffectsLabAuthoring
{
    private const string CombatMenu = "Tools/Subject42/Dev/Visual Effects/";
    private const string CombatSession = "Subject42.VisualEffectsLab.Combat";
    private const string TrialSession = CombatSession + ".Trial";
    private const string PreviousStart = CombatSession + ".PreviousStart";
    private const string CombatOutput = EffectLabCatalog.Output + "/Combat";
    private const string ProjectAssets = "Assets/_Project/";
    private static readonly string[] Modes = { "baseline", "gravity", "links", "both" };
    private enum CombatTrial { Shaders, Floor, FloorCrowd, CenteredVortex }
    private static IEnumerator combatRoutine;
    private static int lastCombatFrame;
    private static double combatDeadline;
    private static Renderer gravityRenderer, linkRenderer;
    private static Material baselineGravity, baselineLinks, previewGravity, previewLinks;
    private static Camera combatCamera;
    private static TelekineticRig combatRig;

    [MenuItem(CombatMenu + "Play combat shader comparison")]
    public static void PlayCombatPreview() => BeginCombatPreview(CombatTrial.Shaders);

    private static void BeginCombatPreview(CombatTrial trial)
    {
        RequireEditMode();
        if (PlayerPrefs.IsSandboxActive || SessionState.GetBool("Subject42.Verification.AnyRunActive", false))
            throw new InvalidOperationException("Finish the other isolated run first.");
        BunkerDebugTools.SetGameView1600();
        Directory.CreateDirectory(CombatOutput);
        SessionState.SetString(PreviousStart, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(CombatSession, true);
        SessionState.SetInt(TrialSession, (int)trial);
        try
        {
            PlayerPrefs.BeginSandbox(true);
            PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
            PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectAssets + "Scenes/MainBuild/MainMenu.unity");
            EditorApplication.EnterPlaymode();
        }
        catch { RestoreCombatSession(); throw; }
    }

    [MenuItem(CombatMenu + "Combat comparison/1 Baseline")] private static void CombatBaseline() => SetCombatShaders(0);
    [MenuItem(CombatMenu + "Combat comparison/2 Gravity only")] private static void CombatGravity() => SetCombatShaders(1);
    [MenuItem(CombatMenu + "Combat comparison/3 Links only")] private static void CombatLinks() => SetCombatShaders(2);
    [MenuItem(CombatMenu + "Combat comparison/4 Both effects")] private static void CombatBoth() => SetCombatShaders(3);
    [MenuItem(CombatMenu + "Combat comparison/Measure GPU")] private static void MeasureCombatPreview()
    {
        RequireCombatReady();
        StartCombatRoutine(MeasureCombat());
    }

    private static void CombatPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(CombatSession, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) StartCombatRoutine(PrepareCombat());
        if (state == PlayModeStateChange.ExitingPlayMode) EndCombatRoutine();
        if (state == PlayModeStateChange.EnteredEditMode) RestoreCombatSession();
    }

    private static void RestoreCombatSession()
    {
        PlayerPrefs.EndSandbox();
        string path = SessionState.GetString(PreviousStart, "");
        EditorSceneManager.playModeStartScene = path.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.EraseString(PreviousStart);
        SessionState.EraseBool(CombatSession);
        SessionState.EraseInt(TrialSession);
    }

    private static void StartCombatRoutine(IEnumerator routine)
    {
        combatRoutine = routine;
        lastCombatFrame = -1;
        combatDeadline = EditorApplication.timeSinceStartup + 60;
    }

    private static void TickCombatPreview()
    {
        if (!EditorApplication.isPlaying || (combatRoutine == null && floorCrowdRoot == null) ||
            lastCombatFrame == Time.frameCount) return;
        lastCombatFrame = Time.frameCount;
        TickFloorCrowdSpirals();
        if (combatRoutine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > combatDeadline) throw new TimeoutException("Combat comparison did not complete.");
            if (!combatRoutine.MoveNext()) { (combatRoutine as IDisposable)?.Dispose(); combatRoutine = null; }
        }
        catch (Exception error)
        {
            EndCombatRoutine();
            File.WriteAllText(CombatOutput + "/error.txt", error.ToString());
            Debug.LogException(error);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void EndCombatRoutine()
    {
        (combatRoutine as IDisposable)?.Dispose();
        combatRoutine = null;
        ReleaseFloorRefraction();
        if (gravityRenderer != null) gravityRenderer.sharedMaterial = baselineGravity;
        if (linkRenderer != null) linkRenderer.sharedMaterial = baselineLinks;
        if (previewGravity != null) Object.DestroyImmediate(previewGravity);
        if (previewLinks != null) Object.DestroyImmediate(previewLinks);
        gravityRenderer = linkRenderer = null;
        previewGravity = previewLinks = null;
    }

    private static IEnumerator PrepareCombat()
    {
        BunkerRunStarter starter;
        while ((starter = Object.FindFirstObjectByType<BunkerRunStarter>()) == null ||
               SceneTransitionOverlay.IsTransitioning || RunSelectionManager.Instance == null) yield return null;
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(ProjectAssets + "Data/Characters/01_Gera.asset"));
        starter.StartRun(starter.transform);

        CharacterSpawner characters;
        while (SceneManager.GetActiveScene().name != "MVP" || ProductionSceneComposition.Active is not { IsReady: true } ||
               SceneTransitionOverlay.IsTransitioning || (characters = Object.FindFirstObjectByType<CharacterSpawner>()) is not
                   { SpawnedPlayer: not null, Station: { IsInitialized: true, Rig: not null } }) yield return null;
        LevelChoiceManager choice;
        while ((choice = Object.FindFirstObjectByType<LevelChoiceManager>()) == null || !choice.IsChoosing) yield return null;
        if (!choice.DebugSelectFirstRule()) throw new InvalidOperationException("The initial MVP route was not selected.");
        while (choice.IsChoosing || Time.timeScale <= 0) yield return null;

        combatRig = characters.Station.Rig;
        var cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
        combatCamera = cameraFollow.ControlledCamera;
        characters.SpawnedPlayer.GetComponent<PlayerHealth>().DebugInvulnerable = true;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        spawner.StopSpawning();
        AddComparisonLinks(combatRig);
        var trial = (CombatTrial)SessionState.GetInt(TrialSession, (int)CombatTrial.Shaders);
        var player = characters.SpawnedPlayer.transform;
        Vector2 playerPosition = player.position;
        bool centered = trial == CombatTrial.CenteredVortex;
        var anomaly = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(ProjectAssets + "Data/Anomalies/LocalAnomaly_Gravity.asset");
        var zoneOffset = trial switch
        {
            CombatTrial.FloorCrowd => CrowdPrimaryOffset,
            CombatTrial.Floor => new Vector2(7.8f, .6f),
            _ => new Vector2(3.3f, .6f)
        };
        Vector2 zoneCenter = centered ? (Vector2)GameplayAreaService.Instance.PlayableArea.bounds.center : playerPosition + zoneOffset;
        var zone = LevelAnomalyController.Instance.SpawnSiteZone(anomaly, zoneCenter,
            centered ? new Vector2(21, 12) : new Vector2(6, 6)) as GravityZone;
        if (zone == null) throw new InvalidOperationException("The gravity zone does not fit in the playable area.");
        if (centered)
        {
            // Start the player away from the core so steering toward the player and the pull can be distinguished.
            var start = zoneCenter + new Vector2(-5, -1.5f);
            player.GetComponent<Rigidbody2D>().position = start;
            player.position = new Vector3(start.x, start.y, player.position.z);
            zone.ConfigureOrbit(0, 0, 0, 0, 0, 0);
        }
        else zone.ConfigureOrbit(7f, 3.5f, 2.5f, 1f, .7f, .35f);
        gravityRenderer = zone.GetComponentInChildren<MeshRenderer>();
        linkRenderer = combatRig.StructureView.PreviewChassisRenderer;
        if (combatRig.StructureView.EdgeCount < 3 || gravityRenderer == null || linkRenderer == null)
            throw new InvalidOperationException("The comparison requires an actual gravity renderer and connected rig.");
        baselineGravity = gravityRenderer.sharedMaterial;
        baselineLinks = linkRenderer.sharedMaterial;
        previewGravity = new Material(RequiredShader("Subject42/Dev/CombatGravityWell"));
        previewLinks = new Material(RequiredShader("Subject42/Dev/CombatLinkPulse"));

        if (centered) PrepareCenteredVortexPresentation();
        ApplyCombatShaders(3);
        float hudReadyAt = Time.time + 1.35f;
        while (Time.time < hudReadyAt) yield return null;
        string[] enemies = { "p_Enemy_classic_start", "p_Enemy_SpiderDark", "p_Enemy_SpiderBlue", "p_Enemy_Flanker" };
        var prefabs = enemies.Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(ProjectAssets + "prefabs/Enemies/" + name + ".prefab")).ToArray();
        for (int ring = 1; ring <= 3; ring++)
            for (int i = 0; i < 12 * ring; i++)
            {
                float angle = (i + ring * .23f) / (12 * ring) * Mathf.PI * 2;
                var origin = centered ? zoneCenter : playerPosition;
                var position = origin + new Vector2(Mathf.Cos(angle) * 3.15f * ring, Mathf.Sin(angle) * 1.9f * ring);
                if (spawner.SpawnDebugEnemyAt(prefabs[(i + ring) % prefabs.Length], position) == null)
                    throw new InvalidOperationException("Failed to populate the comparison crowd.");
            }
        if (!centered)
        {
            float settle = Time.time + .65f;
            while (Time.time < settle) yield return null;
        }
        var comparison = centered ? PreviewCenteredVortex(zone, cameraFollow) : MeasureCombat(trial);
        try { while (comparison.MoveNext()) yield return null; }
        finally { (comparison as IDisposable)?.Dispose(); }
        spawner.ResumeSpawning();
    }

    private static void AddComparisonLinks(TelekineticRig rig)
    {
        rig.SetEditing(true);
        if (!rig.Editing) throw new InvalidOperationException("MVP did not open its constructor.");
        try
        {
            var weapon = rig.Build.Items.First(item => item.IsWeapon);
            if (weapon.Position != Vector2Int.right && !rig.Move(weapon, Vector2Int.right))
                throw new InvalidOperationException("Unable to position the starting weapon for comparison.");
            Place(TelekineticPart.DMG, new Vector2Int(1, 1));
            Place(TelekineticPart.Shotgun, new Vector2Int(2, 0));
            Place(TelekineticPart.FR, new Vector2Int(0, 1));
            Place(TelekineticPart.Pistol, Vector2Int.left);
            Place(TelekineticPart.Pierce, new Vector2Int(-1, 1));
        }
        finally { rig.SetEditing(false); }

        void Place(TelekineticPart part, Vector2Int position)
        {
            rig.Build.Grant(part);
            if (!rig.Place(part, position)) throw new InvalidOperationException($"Unable to install {part} for the comparison.");
        }
    }

    private static void RequireCombatReady()
    {
        if (!EditorApplication.isPlaying || combatRoutine != null || gravityRenderer == null || linkRenderer == null)
            throw new InvalidOperationException("Start the combat shader comparison and wait for its first measurement.");
    }

    private static void SetCombatShaders(int mode)
    {
        RequireCombatReady();
        ApplyFloorRefraction(FloorMode.Off);
        ApplyCombatShaders(mode);
    }
    private static void ApplyCombatShaders(int mode)
    {
        gravityRenderer.sharedMaterial = (mode & 1) != 0 ? previewGravity : baselineGravity;
        linkRenderer.sharedMaterial = (mode & 2) != 0 ? previewLinks : baselineLinks;
    }

    private static IEnumerator MeasureCombat(CombatTrial trial = CombatTrial.Shaders)
    {
        bool measureFloor = trial != CombatTrial.Shaders;
        bool crowd = trial == CombatTrial.FloorCrowd;
        string output = trial switch
        {
            CombatTrial.FloorCrowd => CombatOutput + "/RefractionCrowd",
            CombatTrial.Floor => CombatOutput + "/Refraction",
            _ => CombatOutput
        };
        Directory.CreateDirectory(output);
        string[] modes = crowd ? FloorCrowdModes : measureFloor ? FloorModes : Modes;
        var finalFloorMode = crowd ? FloorMode.Crowd : FloorMode.Warp;
        if (measureFloor) EnsureFloorRefraction();
        if (crowd) EnsureFloorCrowd();
        ApplyFloorRefraction(FloorMode.Off);
        ApplyCombatShaders(3);
        var rows = new List<string> { "pass,mode,gpu_ms" };
        var copyRows = new List<string> { crowd ? "mode,copy_blocks,gpu_ms" : "gpu_ms" };
        var copyRecorder = measureFloor && SystemInfo.supportsGpuRecorder
            ? Recorder.Get("CopyCameraSortingLayer Pass") : null;
        bool canRecordCopy = copyRecorder is { isValid: true };
        bool wasRecordingCopy = canRecordCopy && copyRecorder.enabled;
        int visible = EnemyHealth.ActiveInstances.Count(enemy =>
        {
            Vector3 point = combatCamera.WorldToViewportPoint(enemy.transform.position);
            return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        });
        File.WriteAllText(output + "/setup.txt",
            $"{SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}\n" +
            $"{Screen.width}x{Screen.height}, {combatCamera.name}, active enemies {EnemyHealth.ActiveInstances.Count}, centers in viewport {visible}, rig edges {combatRig.StructureView.EdgeCount}\n" +
            (measureFloor ? "Background sorting layer capture, full resolution, one point sample per distorted pixel.\n" : "") +
            (crowd ? DescribeFloorCrowd() : ""));
        bool oldRecord = ProfilerDriver.enabled, oldGpu = ProfilerDriver.profileGPU, oldEditor = ProfilerDriver.profileEditor;
        float oldTime = Time.timeScale;
        int oldVsync = QualitySettings.vSyncCount, oldTarget = Application.targetFrameRate;
        try
        {
            Time.timeScale = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = measureFloor ? -1 : 60;
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.profileGPU = true;
            ProfilerDriver.enabled = true;
            if (canRecordCopy) copyRecorder.enabled = true;
            int[] order = measureFloor
                ? new[] { 0, 1, 2, 1, 2, 0, 2, 0, 1, 2, 1, 0, 1, 0, 2, 0, 2, 1 }
                : new[] { 0, 1, 2, 3, 0, 3, 2, 1 };
            for (int pass = 0; pass < order.Length; pass++)
            {
                int mode = order[pass];
                if (measureFloor)
                {
                    var floorMode = crowd ? FloorCrowdSamples[mode] : (FloorMode)mode;
                    ApplyFloorRefraction(floorMode);
                    if (crowd) CheckFloorCrowdMode(floorMode);
                }
                else ApplyCombatShaders(mode);
                for (int i = 0; i < (measureFloor ? 14 : 24); i++) yield return null;
                int previous = -1;
                for (int i = 0; i < (measureFloor ? 40 : 56); i++)
                {
                    int index = ProfilerDriver.lastFrameIndex - 4;
                    if (index >= ProfilerDriver.firstFrameIndex && index > previous)
                    {
                        previous = index;
                        using var frame = ProfilerDriver.GetRawFrameDataView(index, 0);
                        if (frame.valid && frame.frameGpuTimeMs > 0 && float.IsFinite(frame.frameGpuTimeMs))
                            rows.Add($"{pass},{modes[mode]},{frame.frameGpuTimeMs.ToString("F4", CultureInfo.InvariantCulture)}");
                        if (canRecordCopy && (crowd || mode != 0))
                        {
                            int blocks = copyRecorder.gpuSampleBlockCount;
                            long duration = copyRecorder.gpuElapsedNanoseconds;
                            string ms = (duration / 1_000_000.0).ToString("F6", CultureInfo.InvariantCulture);
                            if (crowd) copyRows.Add($"{modes[mode]},{blocks},{ms}");
                            else if (blocks == 1 && duration > 0) copyRows.Add(ms);
                        }
                    }
                    yield return null;
                }
                if (pass < modes.Length)
                {
                    ScreenCapture.CaptureScreenshot(output + "/" + modes[mode] + ".png");
                    // CaptureScreenshot reads the rendered frame at its end.
                    yield return null;
                }
            }
            if (measureFloor)
            {
                ApplyFloorRefraction(finalFloorMode);
                var drawMeasurement = MeasureFloorDraw(output, crowd);
                try { while (drawMeasurement.MoveNext()) yield return null; }
                finally { (drawMeasurement as IDisposable)?.Dispose(); }
            }
        }
        finally
        {
            if (canRecordCopy) copyRecorder.enabled = wasRecordingCopy;
            ProfilerDriver.enabled = oldRecord;
            ProfilerDriver.profileGPU = oldGpu;
            ProfilerDriver.profileEditor = oldEditor;
            QualitySettings.vSyncCount = oldVsync;
            Application.targetFrameRate = oldTarget;
            Time.timeScale = oldTime;
        }
        if (measureFloor) File.WriteAllLines(output + "/gpu-copy.csv", copyRows);
        File.WriteAllLines(output + "/gpu.csv", rows);
        ApplyCombatShaders(3);
        if (measureFloor) ApplyFloorRefraction(finalFloorMode);
        float liveUntil = Time.time + .28f;
        while (Time.time < liveUntil) yield return null;
        ScreenCapture.CaptureScreenshot(output + "/live-a.png");
        liveUntil = Time.time + .40f;
        while (Time.time < liveUntil) yield return null;
        ScreenCapture.CaptureScreenshot(output + "/live-b.png");
    }
}
#endif
