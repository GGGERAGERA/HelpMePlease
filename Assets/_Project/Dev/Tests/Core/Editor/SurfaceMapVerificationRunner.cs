#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class SurfaceMapVerificationRunner
{
    private const string Output = "Artifacts/SurfaceMap";
    static SurfaceMapVerificationRunner() { ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results()); EditorApplication.update += Poll; }
    private static void Poll()
    {
        string request = Output + "/run-tests.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
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
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = requested == "operator-ux" ? new[] { "MissionProductionFlowTests", "MissionServiceTests.OperatorArrowIsVisibleInEditModeAndUsesStandardPrefab" } : requested == "missions" ? new[] { "MissionServiceTests", "MissionProductionFlowTests" } : requested == "map-ux" ? new[] { "SurfaceMapUxTests" } : requested == "bunker-fixes" ? new[] { "BunkerInteractionFixTests" } : requested == "sector-flow" ? new[] { "SurfaceSectorTransitionTests" } : markers ? new[] { "SurfaceMarkerTests", "SurfaceMarkerFlowTests" } : new[] { "SurfaceMapTests", "SurfaceMapProgressionTests", "SurfaceRunIntegrationTests", "ProductionBunkerFlowTests" } }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        { Directory.CreateDirectory(Output); TestRunnerApi.SaveResultToFile(result, Output + "/results.xml"); }
    }
}
#endif
