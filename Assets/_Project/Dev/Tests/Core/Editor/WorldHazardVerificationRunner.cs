#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class WorldHazardVerificationRunner
{
    private const string Root = "Artifacts/GeneratedQA/WorldHazards";
    private const string RunningKey = "WorldHazardTests.Running";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";
    static WorldHazardVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AnyRunKey, false) || !File.Exists(Root + "/run-tests.request")) return;
        try { File.Delete(Root + "/run-tests.request"); }
        catch (IOException) { return; } // The requesting process may still hold the file open.
        Run();
    }

    [MenuItem("Tools/Subject42/Verify World Hazards")]
    public static void Run()
    {
        if (SessionState.GetBool(AnyRunKey, false)) return;
        SessionState.SetBool(AnyRunKey, true);
        SessionState.SetBool(RunningKey, true);
        try { ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(
            new Filter { testMode = TestMode.EditMode, testNames = new[] { "WorldHazardTests", "WorldHazardPlayTests" } })); }
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
            Directory.CreateDirectory(Root);
            TestRunnerApi.SaveResultToFile(result, Root + "/results.xml");
            if (result.PassCount + result.FailCount == 0)
            {
                const string message = "FAIL: the request produced no passing or failing test cases. Check filter names and results.xml for skipped/inconclusive cases.";
                File.WriteAllText(Root + "/run-error.txt", message);
                Debug.LogError(message);
            }
            else if (File.Exists(Root + "/run-error.txt")) File.Delete(Root + "/run-error.txt");
        }
        public void OnError(string message)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root + "/run-error.txt", message);
        }
    }
}
#endif
