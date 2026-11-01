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
using Object = UnityEngine.Object;

// One opt-in bounded Play Mode check for the lab UI. No builds or production scene changes.
[InitializeOnLoad]
public static class EnemyOrbitalManualSpawnPreview
{
    const string Output = "Artifacts/GeneratedQA/ManualEnemySpawn", Key = "EnemyOrbital.ManualSpawnPreview";
    static IEnumerator routine;
    static readonly List<string> report = new();
    static double next, deadline;
    static int errors;
    static EnemyOrbitalManualSpawnPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { routine = Check(); report.Clear(); errors = 0; deadline = EditorApplication.timeSinceStartup + 25; }
            if (state == PlayModeStateChange.EnteredEditMode) { SessionState.EraseBool(Key); routine = null; }
        };
        Application.logMessageReceived += (message, stack, type) =>
        { if (routine != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) { errors++; report.Add("UNITY ERROR: " + message); } };
    }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Check Manual Enemy Spawn")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        string path = Output + "/editor.request";
        if (File.Exists(path) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            string command;
            try { command = File.ReadAllText(path).Trim(); } catch (IOException) { return; }
            if (command == "check" && !EditorApplication.isPlayingOrWillChangePlaymode) { File.Delete(path); Open(); }
            else if (command == "stop") { File.Delete(path); EditorApplication.ExitPlaymode(); }
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("25-second manual-spawn preview deadline");
            if (!routine.MoveNext()) { Finish(errors == 0 ? "PASS: bounded Play Mode check; zero runtime errors" : "FAIL: runtime errors"); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .01);
        }
        catch (Exception error) { Finish("FAIL: " + error); }
    }
    static void Finish(string result) { routine = null; report.Add(result); File.WriteAllLines(Output + "/result.txt", report); }
    static void Require(bool condition, string label)
    { if (!condition) throw new Exception(label); report.Add("PASS: " + label); }
    static IEnumerator Check()
    {
        EnemyOrbitalLabController lab;
        while ((lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>()) == null || !lab.Ready || !lab.SandboxReady) yield return .02f;
        lab.AutomaticInterceptions = false; lab.CancelInterception(); lab.SetSegmentedOrbit(true);
        while (!lab.Ready || !lab.Segmented.Bound || !lab.SegmentedOrbit) yield return .02f;
        lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        lab.ClearDensityEnemies(); lab.SetAutomaticEnemySpawning(false); lab.SetFrozen(true); lab.SetPlayerInvincible(true);
        lab.Station.enabled = false; lab.SetPaused(false);
        var types = lab.ManualEnemyTypes.ToArray();
        int flanker = types.Single(t => lab.ManualEnemyName(t) == "Обходчик"), breacher = types.Single(t => lab.ManualEnemyName(t) == "Проломщик");
        Require(types.Length >= 9, "automatic unique catalog includes " + types.Length + " connected combat prefabs");
        foreach (int type in types) Require(lab.SpawnManualEnemies(type, 1) == 1, "immediate +1: " + lab.ManualEnemyName(type));
        Require(lab.SpawnManualEnemies(flanker, 5) == 5 && lab.SpawnManualEnemies(breacher, 10) == 10, "+5/+10 exceed production tactical caps");
        int manual = types.Length + 15;
        Require(lab.ManualAliveEnemies == manual && lab.AliveEnemies == manual, "counter includes every manually spawned enemy");
        lab.SetAutomaticEnemySpawning(true); yield return .2f;
        Require(lab.AliveEnemies == manual, "automatic density target zero preserves manual actors");
        lab.SetTargetEnemyDensity(5); yield return .15f;
        Require(lab.ManualAliveEnemies == manual && lab.AliveEnemies == manual, "lower target does not remove manual actors");
        Physics2D.SyncTransforms();
        var actors = lab.ManualEnemies.Where(e => e != null).ToArray();
        bool overlaps = false;
        for (int i = 0; i < actors.Length; i++) for (int j = i + 1; j < actors.Length; j++)
            foreach (var a in actors[i].GetComponentsInChildren<Collider2D>()) foreach (var b in actors[j].GetComponentsInChildren<Collider2D>())
            {
                if (!a.enabled || !b.enabled) continue;
                var separation = a.Distance(b);
                if (separation.isValid && separation.distance < -.001f) overlaps = true;
            }
        Require(!overlaps, "manual actors have non-overlapping collision shapes");
        var victim = actors.First(e => e.name.Contains("Flanker"));
        victim.TakeDamage(victim.MaxHealth + 1, victim.transform.position); yield return .08f;
        manual--;
        Require(lab.ManualAliveEnemies == manual, "existing damage/death path updates manual live counter");
        lab.DensitySpawnRate = 60; lab.SetTargetEnemyDensity(manual + 5);
        while (lab.AliveEnemies < manual + 5) yield return .02f;
        Require(lab.ManualAliveEnemies == manual && lab.AliveEnemies == manual + 5, "mixed automatic spawn fills remaining density");
        lab.SetTargetEnemyDensity(0); yield return .25f;
        Require(lab.AliveEnemies == manual && lab.ManualAliveEnemies == manual, "lowering to zero retires automatic actors only");
        lab.SetAutomaticEnemySpawning(false); lab.SetTargetEnemyDensity(100); yield return .15f;
        Require(lab.AliveEnemies == manual, "automatic spawn can stop while density setting is retained");
        lab.ClearDensityEnemies();
        Require(lab.AliveEnemies == 0 && lab.ManualAliveEnemies == 0, "clear removes both manual and automatic enemies");
        foreach (int type in types) lab.SpawnManualEnemies(type, 1);
        lab.SpawnManualEnemies(flanker, 5);
        lab.SetPaused(true); lab.SetFrozen(false); lab.Station.enabled = true;
        var view = typeof(EnemyOrbitalLabController).GetField("segmentedView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
        view.GetType().GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 1);
        yield return .1f;
        Require(Screen.width == 1600 && Screen.height == 900, "panel shown in Game View 1600×900");
        ScreenCapture.CaptureScreenshot(Output + "/manual-spawn-panel.png"); yield return .15f;
        Require(lab.Station.ValidateState(out _), "existing orbital construction remains valid; left on review pause");
    }
}
#endif
