#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class RewardColorVerificationRunner
{
    private const string Output = "Artifacts/GeneratedQA/RewardColors";
    private const string RunningKey = "RewardColorVerification.Running";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";
    static RewardColorVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += () =>
        {
            const string request = Output + "/run.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AnyRunKey, false)) return;
            var filter = File.ReadAllText(request).Trim();
            File.Delete(request);
            if (filter.Length == 0) Run();
            else Execute(new Filter { testMode = TestMode.EditMode, testNames = new[] { filter } });
        };
    }

    [MenuItem("Tools/Subject42/Verify Reward Colors")]
    public static void Run() => Execute(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "RewardStylePresentationTests", "Subject42RewardProgressionTests",
                "ProductionAnomalyRewardFlowTests" } });

    private static void Execute(Filter filter)
    {
        if (SessionState.GetBool(AnyRunKey, false)) return;
        SessionState.SetBool(AnyRunKey, true);
        SessionState.SetBool(RunningKey, true);
        try { ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter)); }
        catch { SessionState.EraseBool(AnyRunKey); SessionState.EraseBool(RunningKey); throw; }
    }

    private sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { SessionState.SetBool(AnyRunKey, true); }
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
