#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class RewardColorVerificationRunner
{
    static RewardColorVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.delayCall += () =>
        {
            const string request = "Artifacts/GeneratedQA/RewardColors/run.request";
            if (!File.Exists(request)) return;
            var filter = File.ReadAllText(request).Trim();
            File.Delete(request);
            if (filter.Length == 0) Run();
            else ScriptableObject.CreateInstance<TestRunnerApi>().Execute(
                new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { filter } }));
        };
    }

    [MenuItem("Tools/Subject42/Verify Reward Colors")]
    public static void Run() => ScriptableObject.CreateInstance<TestRunnerApi>().Execute(
        new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "RewardStylePresentationTests", "Subject42RewardProgressionTests",
                "ProductionAnomalyRewardFlowTests" } }));

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory("Artifacts/GeneratedQA/RewardColors");
            TestRunnerApi.SaveResultToFile(result, "Artifacts/GeneratedQA/RewardColors/results.xml");
        }
    }
}
#endif
