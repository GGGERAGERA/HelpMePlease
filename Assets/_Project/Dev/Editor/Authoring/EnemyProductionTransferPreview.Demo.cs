#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public static partial class EnemyProductionTransferPreview
{
    static IEnumerator demo;
    static double nextDemo, demoDeadline;
    static void StartDemo()
    {
        CoreTestSupport.PreservePreferences();
        SessionState.SetBool("EnemyProductionTransfer.Demo", true);
        typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s => s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == RunEndService.BunkerSceneName).path);
        EditorApplication.EnterPlaymode();
    }
    static void DemoState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("EnemyProductionTransfer.Demo", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { demo = ShowProduction(); nextDemo = 0; demoDeadline = EditorApplication.timeSinceStartup + 55; }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            var cleanup = CoreTestSupport.CleanupPlayMode(); while (cleanup.MoveNext()) { }
            SessionState.EraseBool("EnemyProductionTransfer.Demo"); demo = null;
        }
    }
    static void TickDemo()
    {
        if (demo == null || EditorApplication.timeSinceStartup < nextDemo) return;
        try
        {
            if (EditorApplication.timeSinceStartup > demoDeadline) throw new Exception("Bounded production preview timed out");
            if (!demo.MoveNext()) { demo = null; return; }
            nextDemo = EditorApplication.timeSinceStartup + (demo.Current is float seconds ? seconds : .02f);
        }
        catch (Exception error) { File.WriteAllText(Output + "/demo.txt", error.ToString()); demo = null; Time.timeScale = 0; }
    }
    static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static IEnumerator ShowProduction()
    {
        BunkerRunStarter starter;
        while ((starter = Object.FindFirstObjectByType<BunkerRunStarter>()) == null || SceneTransitionOverlay.IsTransitioning) yield return .02f;
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        starter.StartRun(starter.transform);
        while (SceneManager.GetActiveScene().name != "MVP" || Object.FindFirstObjectByType<OrbitalStationRuntime>() is not { IsInitialized: true } || SceneTransitionOverlay.IsTransitioning) yield return .02f;
        var run = RunStateManager.Instance; var prior = run.CurrentSector;
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_03.asset");
        run.SetCurrentSector(new RunSector(3, stage, prior.WorldRule, prior.LocalAnomaly));
        var load = SceneManager.LoadSceneAsync("MVP"); while (!load.isDone) yield return .02f;
        while (Object.FindFirstObjectByType<OrbitalStationRuntime>() is not { IsInitialized: true } || SceneTransitionOverlay.IsTransitioning) yield return .02f;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        spawner.enabled = false; station.enabled = false;
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(enemy.gameObject);
        Invoke(spawner, "RemoveDestroyedEnemies");
        // Retain one of each normal weighted outcome to avoid a long random wait for the review frame.
        var keep = new List<EnemyHealth>();
        string[] wanted = { "p_Enemy_classic", "p_Enemy_SpiderDark", "p_Enemy_SpiderBigGait", "p_Enemy_SpiderBlue", "p_Enemy_Flanker", "p_Enemy_Breacher" };
        string Kind(EnemyHealth e) => e.name.Replace("(Clone)", "");
        for (int cycle = 0; cycle < 200; cycle++)
        {
            Invoke(spawner, "SpawnCycle", 1);
            foreach (var enemy in EnemyHealth.ActiveInstances.Where(e => !keep.Contains(e)).ToArray())
            {
                if (wanted.Contains(Kind(enemy)) && keep.Count(e => Kind(e) == Kind(enemy)) < (Kind(enemy) == "p_Enemy_Breacher" ? 3 : Kind(enemy) == "p_Enemy_Flanker" ? 2 : 1))
                { keep.Add(enemy); enemy.GetComponent<EnemyMovement>().enabled = false; }
                else Object.DestroyImmediate(enemy.gameObject);
            }
            Invoke(spawner, "RemoveDestroyedEnemies");
            if (wanted.All(kind => keep.Any(e => Kind(e) == kind)) && keep.Count(e => Kind(e) == "p_Enemy_Breacher") >= 2) break;
            yield return .01f;
        }
        Require(wanted.All(kind => keep.Any(e => Kind(e) == kind)), "all four mass visuals and both tactical variants spawned by ordinary cycle");
        foreach (var enemy in keep) enemy.GetComponent<EnemyMovement>().enabled = true;
        station.enabled = true; spawner.enabled = true;
        yield return 2.2f;
        player.GetComponent<PlayerHealth>().AddMaxHealth(-1000000f);
        Time.timeScale = 0;
        var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.maximized = true; view.Show(); view.Focus();
        ScreenCapture.CaptureScreenshot(Output + "/production-mixed-1600x900.png");
        yield return .3f;
        EditorApplication.isPaused = true;
        // Unity Pause controls the review frame; ordinary time resumes when the user unpauses.
        Time.timeScale = 1;
        File.WriteAllText(Output + "/demo.txt", "READY: bunker → ordinary MVP → shortened transition to sector 3; normal weighted SpawnCycle mixed crowd; original production weapons active; Game View 1600x900, Unity paused. Preferences restore on Stop. No production data changed for preview.");
    }
    static IEnumerator RefreshCapture()
    {
        Time.timeScale = 0;
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        if (player.MaxHealth > 1000000f) player.AddMaxHealth(-1000000f);
        EditorApplication.isPaused = false;
        ScreenCapture.CaptureScreenshot(Output + "/production-mixed-1600x900.png");
        yield return .3f;
        EditorApplication.isPaused = true; Time.timeScale = 1;
        File.WriteAllText(Output + "/review-state.txt", $"scene={SceneManager.GetActiveScene().name}; playing={EditorApplication.isPlaying}; paused={EditorApplication.isPaused}; resolution={Screen.width}x{Screen.height}; heroHP={player.CurrentHealth}/{player.MaxHealth}; liveEnemies={EnemyHealth.ActiveInstances.Count}");
    }
}
#endif
