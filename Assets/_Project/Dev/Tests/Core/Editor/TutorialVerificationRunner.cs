#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class TutorialVerificationRunner
{
    private const string Output = "Artifacts/GeneratedQA/Tutorial";
    private const string RunningKey = "TutorialVerification.Running";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";
    static TutorialVerificationRunner()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += CheckRequest;
    }
    private static void CheckRequest()
    {
        string request = Output + "/run-tests.request";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            SessionState.GetBool(AnyRunKey, false) || !File.Exists(request)) return;
        string filter = File.ReadAllText(request).Trim();
        File.Delete(request);
        var selection = new Filter { testMode = TestMode.EditMode };
        if (filter == "Core") selection.categoryNames = new[] { "Core" };
        else selection.testNames = string.IsNullOrEmpty(filter) ? new[] { "Subject42TutorialTests" }
            : filter.Split(';').Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();
        if (selection.testNames != null && selection.testNames.Length == 0)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/run-error.txt", "The request contains no fixture or test names.");
            return;
        }
        SessionState.SetBool(AnyRunKey, true);
        SessionState.SetBool(RunningKey, true);
        try { ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(selection)); }
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
