#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class CompactHudVerificationRunner
{
    static CompactHudVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += () =>
        {
            const string request = "Artifacts/GeneratedQA/CompactHud/run.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var filter = File.ReadAllText(request).Trim();
            File.Delete(request);
            if (filter.Length == 0) Run();
            else ScriptableObject.CreateInstance<TestRunnerApi>().Execute(
                new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { filter } }));
        };
    }

    [MenuItem("Tools/Subject42/Verify Compact HUD")]
    public static void Run() => ScriptableObject.CreateInstance<TestRunnerApi>().Execute(
        new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "CompactHudTests", "CompactHudPlayTests" } }));

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory("Artifacts/GeneratedQA/CompactHud");
            TestRunnerApi.SaveResultToFile(result, "Artifacts/GeneratedQA/CompactHud/results.xml");
        }
    }
}
#endif
