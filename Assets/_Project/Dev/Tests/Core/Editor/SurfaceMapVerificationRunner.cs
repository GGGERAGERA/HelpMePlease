#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class SurfaceMapVerificationRunner
{
    private const string Output = "Artifacts/GeneratedQA/SurfaceMap";
    private const string RunningKey = "SurfaceMapVerification.Running";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";
    static SurfaceMapVerificationRunner() { ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results()); EditorApplication.update += Poll; }
    private static void Poll()
    {
        string request = Output + "/run-tests.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AnyRunKey, false)) return;
        string stamp = File.GetLastWriteTimeUtc(request).Ticks.ToString();
        if (SessionState.GetString("SurfaceMap.Tests.Request", "") != stamp)
        {
            SessionState.SetString("SurfaceMap.Tests.Request", stamp);
            SessionState.SetFloat("SurfaceMap.Tests.RefreshAt", (float)EditorApplication.timeSinceStartup);
            AssetDatabase.Refresh();
            return;
        }
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat("SurfaceMap.Tests.RefreshAt", 0f) < 3f) return;
        string requested = File.ReadAllText(request).Trim();
        bool markers = requested == "markers";
        File.Delete(request);
        if (requested == "refresh-only") return;
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        SessionState.SetBool(AnyRunKey, true);
        SessionState.SetBool(RunningKey, true);
        try { api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = requested == "operator-ux" ? new[] { "MissionProductionFlowTests", "MissionServiceTests.OperatorArrowIsVisibleInEditModeAndUsesStandardPrefab" } : requested == "missions" ? new[] { "MissionServiceTests", "MissionProductionFlowTests" } : requested == "map-ux" ? new[] { "SurfaceMapUxTests" } : requested == "bunker-fixes" ? new[] { "BunkerInteractionFixTests" } : requested == "sector-flow" ? new[] { "SurfaceSectorTransitionTests" } : markers ? new[] { "SurfaceMarkerTests", "SurfaceMarkerFlowTests" } : new[] { "SurfaceMapTests", "SurfaceMapProgressionTests", "SurfaceRunIntegrationTests", "ProductionBunkerFlowTests" } })); }
        catch { SessionState.EraseBool(AnyRunKey); SessionState.EraseBool(RunningKey); throw; }
    }
    private sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { SessionState.SetBool(AnyRunKey, true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Output);
            TestRunnerApi.SaveResultToFile(result, Output + "/results.xml");
            if (result.PassCount + result.FailCount == 0)
            {
                const string message = "FAIL: the request produced no passing or failing test cases. Check filter names and results.xml for skipped/inconclusive cases.";
                File.WriteAllText(Output + "/run-error.txt", message);
                Debug.LogError(message);
            }
            else if (File.Exists(Output + "/run-error.txt")) File.Delete(Output + "/run-error.txt");
        }
        public void OnError(string message)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/run-error.txt", message);
        }
    }
}
#endif
