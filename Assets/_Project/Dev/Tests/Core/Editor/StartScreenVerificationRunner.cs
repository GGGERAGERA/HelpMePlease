#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class StartScreenVerificationRunner
{
    static StartScreenVerificationRunner()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results());
        EditorApplication.delayCall += () =>
        {
            const string request = "Artifacts/StartScreen/run-tests.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            Run();
        };
    }

    [MenuItem("Tools/Subject42/Verify Start Screen")]
    public static void Run()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { "StartScreenPresentationTests" } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory("Artifacts/StartScreen");
            TestRunnerApi.SaveResultToFile(result, "Artifacts/StartScreen/results.xml");
        }
    }
}
#endif
