#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class TutorialVerificationRunner
{
    private const string Output = "Artifacts/GeneratedQA/Tutorial";
    static TutorialVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += CheckRequest;
    }
    private static void CheckRequest()
    {
        string request = Output + "/run-tests.request";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            SessionState.GetBool("TutorialVerification.Running", false) || !File.Exists(request)) return;
        string filter = File.ReadAllText(request).Trim();
        File.Delete(request);
        var selection = new Filter { testMode = TestMode.EditMode };
        if (filter == "Core") selection.categoryNames = new[] { "Core" };
        else selection.testNames = new[] { string.IsNullOrEmpty(filter) ? "Subject42TutorialTests" : filter };
        ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(selection));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { SessionState.SetBool("TutorialVerification.Running", true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.SetBool("TutorialVerification.Running", false);
            Directory.CreateDirectory(Output);
            TestRunnerApi.SaveResultToFile(result, Output + "/results.xml");
        }
    }
}
#endif
