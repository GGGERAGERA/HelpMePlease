#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class WorldHazardVerificationRunner
{
    private const string Root = "Artifacts/WorldHazards";
    static WorldHazardVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Root + "/run-tests.request")) return;
        try { File.Delete(Root + "/run-tests.request"); }
        catch (IOException) { return; } // The requesting process may still hold the file open.
        Run();
    }

    [MenuItem("Tools/Subject42/Verify World Hazards")]
    public static void Run()
    {
        SessionState.SetBool("WorldHazardTests.Running", true);
        ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(
            new Filter { testMode = TestMode.EditMode, testNames = new[] { "WorldHazardTests", "WorldHazardPlayTests" } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool("WorldHazardTests.Running", false)) return;
            SessionState.SetBool("WorldHazardTests.Running", false);
            Directory.CreateDirectory(Root);
            TestRunnerApi.SaveResultToFile(result, Root + "/results.xml");
        }
    }
}
#endif
