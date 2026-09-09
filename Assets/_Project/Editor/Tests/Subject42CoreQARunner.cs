#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
[InitializeOnLoad]
public static class Subject42CoreQARunner
{
    private const string Root = "Artifacts/GeneratedQA/CorePulse/";
    private const string Rewards = "Artifacts/GeneratedQA/RewardProgression/";
    private static string output { get => SessionState.GetString("Subject42.QAOutput", null); set => SessionState.SetString("Subject42.QAOutput", value); }
    static Subject42CoreQARunner() { EditorApplication.update += Poll; ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results()); }
    private static void Poll()
    {
        if (File.Exists(Root + "author.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Root + "author.request");
            Subject42CoreAuthoring.Author();
            File.WriteAllText(Root + "author.result", "OK");
            return;
        }
        string requestRoot = File.Exists(Rewards + "run.request") ? Rewards : Root;
        if (!File.Exists(requestRoot + "run.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        output = requestRoot;
        string filter = File.ReadAllText(requestRoot + "run.request");
        File.Delete(requestRoot + "run.request");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();

        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = filter.Split(';') }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result) { if (output != null) TestRunnerApi.SaveResultToFile(result, output + "results.xml"); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { if (output != null) File.AppendAllText(output + "progress.txt", result.Test.FullName + " " + result.TestStatus + " " + result.Message + "\n"); }
    }
}
#endif
