#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
[InitializeOnLoad]
public static class CorridorMigrationVerification
{
    private const string Output="Artifacts/GeneratedQA/CorridorMigration/";
    static CorridorMigrationVerification()
    { ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results()); EditorApplication.update+=Poll; }
    private static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
        string request=Output+"verify.request";
        if(File.Exists(request))
        {
            string filter;
            try { filter=File.ReadAllText(request).Trim(); File.Delete(request); } catch(IOException) { return; }
            SessionState.SetBool("CorridorMigration.Running",true);
            var names=filter=="all" ? new[]{"CorridorRoutePlacementTests","CorridorGameplayTests",
                "CorridorStructuralTests.StasisOverlapPreservesEffectVisualsAndPlayerPosition",
                "CorridorSiteLifecycleTests","CorridorProductionIntegrationTests"} : filter.Split(';');
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,testNames=names}));
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
    private sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if(!SessionState.GetBool("CorridorMigration.Running",false)) return;
            SessionState.SetBool("CorridorMigration.Running",false);
            TestRunnerApi.SaveResultToFile(result,Output+"results.xml");
        }
    }
}
#endif
