#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
[InitializeOnLoad]
public static class Subject42CoreQARunner
{
    private const string Root = "Artifacts/GeneratedQA/CorePulse/";
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
        if (!File.Exists(Root + "run.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string filter = File.ReadAllText(Root + "run.request");
        File.Delete(Root + "run.request");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();

        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = filter.Split(';') }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result) { TestRunnerApi.SaveResultToFile(result, Root + "results.xml"); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { File.AppendAllText(Root + "progress.txt", result.Test.FullName + " " + result.TestStatus + " " + result.Message + "\n"); }
    }
}
#endif
