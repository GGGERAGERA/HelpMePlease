#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
[InitializeOnLoad]
public static class CorridorMigrationVerification
{
    private const string Output="Artifacts/GeneratedQA/CorridorMigration/";
    private const string RunningKey = "CorridorMigration.Running";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";
    static CorridorMigrationVerification()
    { ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results()); EditorApplication.update+=Poll; }
    private static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(AnyRunKey,false)) return;
        string request=Output+"verify.request";
        if(File.Exists(request))
        {
            string filter;
            try { filter=File.ReadAllText(request).Trim(); File.Delete(request); } catch(IOException) { return; }
            var names=filter=="all" ? new[]{"CorridorRoutePlacementTests","CorridorGameplayTests",
                "CorridorStructuralTests.StasisOverlapPreservesEffectVisualsAndPlayerPosition",
                "CorridorSiteLifecycleTests","CorridorProductionIntegrationTests"} : filter.Split(';').Select(name=>name.Trim()).Where(name=>name.Length>0).ToArray();
            if(names.Length==0)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output+"run-error.txt","The request contains no fixture or test names.");
                return;
            }
            SessionState.SetBool(AnyRunKey,true);
            SessionState.SetBool(RunningKey,true);
            try { ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,testNames=names})); }
            catch { SessionState.EraseBool(AnyRunKey); SessionState.EraseBool(RunningKey); throw; }
            return;
        }
        request=Output+"compile.request";
        if(!File.Exists(request)) return;
        try { File.Delete(request); } catch(IOException) { return; }
        try
        {
            var settings=new ScriptCompilationSettings { target=BuildTarget.StandaloneWindows64, group=BuildTargetGroup.Standalone,
                options=ScriptCompilationOptions.None };
            var result=PlayerBuildInterface.CompilePlayerScripts(settings,Output+"PlayerScripts");
            File.WriteAllText(Output+"compile.result",result.assemblies.Count>0 ? "PASS: non-development player scripts compiled" : "FAIL: no assemblies");
        }
        catch(Exception exception) { File.WriteAllText(Output+"compile.result",exception.ToString()); Debug.LogException(exception); }
    }
    private sealed class Results:IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { SessionState.SetBool(AnyRunKey,true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.EraseBool(AnyRunKey);
            if(!SessionState.GetBool(RunningKey,false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Output);
            TestRunnerApi.SaveResultToFile(result,Output+"results.xml");
            if(result.PassCount+result.FailCount==0)
            {
                const string message="FAIL: the request produced no passing or failing test cases. Check filter names and results.xml for skipped/inconclusive cases.";
                File.WriteAllText(Output+"run-error.txt",message);
                Debug.LogError(message);
            }
            else if(File.Exists(Output+"run-error.txt")) File.Delete(Output+"run-error.txt");
        }
        public void OnError(string message)
        {
            SessionState.EraseBool(AnyRunKey);
            if(!SessionState.GetBool(RunningKey,false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"run-error.txt",message);
        }
    }
}
#endif
