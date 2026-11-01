#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Explicit requests only. Four focused checks, no test-suite runner or build.
[InitializeOnLoad]
public static class TacticalEnemyNightPreview
{
    public const string Output = "Artifacts/GeneratedQA/TacticalEnemiesNight";
    const string Running = "Subject42.TacticalNight.Running";
    const string Demo = "Subject42.TacticalNight.Demo";
    static int demoStage;
    static double demoDeadline, captureAt;
    static TacticalEnemyNightPreview()
    {
        EditorApplication.update += Poll;
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(Demo); };
    }
    static void Poll()
    {
        if (SessionState.GetBool(Demo, false) && EditorApplication.isPlaying) TickDemo();
        string request = Output + "/editor.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command;
        try { command = File.ReadAllText(request).Trim(); }
        catch (IOException) { return; } // The request writer may still hold its short-lived file handle.
        bool runnerActive = (bool)typeof(TestRunnerApi).GetMethod("IsRunActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
        if (command == "status")
        {
            File.Delete(request);
            File.WriteAllText(Output + "/status.txt", $"playing={EditorApplication.isPlayingOrWillChangePlaymode}, runner={runnerActive}, sharedFlag={SessionState.GetBool("Subject42.Verification.AnyRunActive", false)}, own={SessionState.GetString(Running, "")}");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || runnerActive) return;
        File.Delete(request);
        if (command == "checks") Checks();
        else if (command == "lab" || command == "production")
        {
            SessionState.SetString(Running, command);
            string test = command == "lab" ? "TacticalEnemyNightLabTests.FlankRotationRamDodgeAndDenseCombat" :
                "TacticalEnemyMvpSmokeTests.OrdinaryMvpVariantsFlankCommitRecoverAndKeepTheirCaps";
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { test } }));
        }
        else if (command == "demo")
        {
            typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            SessionState.SetBool(Demo, true); demoStage = 0; demoDeadline = EditorApplication.timeSinceStartup + 30;
            EnemyOrbitalLabAuthoring.PlaySegmentedManual();
        }
    }
    static void TickDemo()
    {
        var lab = UnityEngine.Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        if (demoDeadline == 0) demoDeadline = EditorApplication.timeSinceStartup + 30;
        if (EditorApplication.timeSinceStartup > demoDeadline)
        { lab?.SetPaused(true); SessionState.EraseBool(Demo); File.WriteAllText(Output + "/demo.txt", "Preview setup timed out; Unity left open."); return; }
        if (lab == null || !lab.Ready || !lab.SandboxReady || !lab.SegmentedOrbit || !lab.Segmented.Bound || SessionState.GetBool("EnemyOrbitalLab.OpenSegmented", false)) return;
        if (demoStage == 0)
        {
            lab.SetPaused(true); lab.Segmented.LoadPreset("Front Arc");
            lab.AutomaticInterceptions = false; lab.CancelInterception(); lab.SetEnemiesInvincible(true); lab.SetPlayerInvincible(true);
            lab.SetTargetEnemyDensity(50); lab.DensitySpawnRate = 40; lab.SetPaused(false); demoStage = 1;
        }
        else if (demoStage == 1 && lab.AliveEnemies == 50 && lab.LabEnemies.Any(e => e != null && e.TryGetComponent<EnemyChaseMovement>(out var m) &&
            m.IsCommittedRamExperiment && typeof(EnemyChaseMovement).GetField("ramPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(m).ToString() == "Preparing"))
        {
            Time.timeScale = 0;
            ScreenCapture.CaptureScreenshot(Output + "/lab-flank-and-ram.png");
            captureAt = EditorApplication.timeSinceStartup + .25; demoStage = 2;
        }
        else if (demoStage == 2 && EditorApplication.timeSinceStartup >= captureAt)
        {
            lab.SetPaused(true); lab.SetEnemiesInvincible(false);
            SessionState.EraseBool(Demo); File.WriteAllText(Output + "/demo.txt", "READY: EnemyOrbitalLab, 50 mixed live enemies, Manual Front Arc, lasers OFF, paused; P/Tab resumes. Enemy invulnerability OFF, hero training ON.");
        }
    }
    [MenuItem("Tools/Subject42/Dev/Enemies/Check Tactical Night Changes")]
    public static void Checks()
    {
        Directory.CreateDirectory(Output);
        var report = new System.Collections.Generic.List<string>();
        var tests = new TacticalEnemyMvpTests();
        Run("smooth flank exit", tests.FlankPoseBlendsOutAtMeleeDistance);
        Run("threatening ram range", tests.BreacherTelegraphReachesItsApproachRange);
        Run("new approach before next ram", tests.BreacherDoesNotImmediatelyRepeatAgainstNearbyPlayer);
        Run("sector availability and exact caps", tests.ProductionProfilesAddBothVariantsAndKeepExistingEnemies);
        File.WriteAllLines(Output + "/checks.txt", report);
        void Run(string name, Action action)
        {
            try { action(); report.Add("PASS: " + name); }
            catch (Exception error) { report.Add("FAIL: " + name + "\n" + error.Message); }
            finally { tests.DestroyActors(); }
        }
    }
    sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { if (SessionState.GetString(Running, "") != "") SessionState.SetBool("Subject42.Verification.AnyRunActive", true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string command = SessionState.GetString(Running, ""); if (command == "") return;
            TestRunnerApi.SaveResultToFile(result, Output + "/" + command + ".xml");
            SessionState.EraseString(Running); SessionState.EraseBool("Subject42.Verification.AnyRunActive");
        }
        public void OnError(string message)
        {
            string command = SessionState.GetString(Running, ""); if (command == "") return;
            File.WriteAllText(Output + "/" + command + "-error.txt", message);
            SessionState.EraseString(Running); SessionState.EraseBool("Subject42.Verification.AnyRunActive");
        }
    }
}
#endif
