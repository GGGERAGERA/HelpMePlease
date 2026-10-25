using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

public static class ComputeBufferLifecycleSmoke
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type GpuType = typeof(SpriteSkin).Assembly.GetType("UnityEngine.U2D.Animation.GpuDeformationSystem");
    private static readonly FieldInfo Fallback = GpuType.GetField("s_FallbackBuffer", Static);
    private static ComputeBuffer ReadBuffer() => (ComputeBuffer)Fallback.GetValue(null);

    public static IEnumerator VerifyCurrentRun(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        var trace = new StringBuilder($"Editor={Application.isEditor} Development={Debug.isDebugBuild}\n");
        var retained = ReadBuffer();
        Require(retained != null && retained.IsValid(), "Expected the package's startup fallback buffer.");
        Record("MVP first");
        void AuditQuit()
        {
            bool released = ReadBuffer() == null && !retained.IsValid();
            File.WriteAllText(directory + "/shutdown-audit.txt", $"released={released} fallbackNull={ReadBuffer() == null} retainedValid={retained.IsValid()}\n");
            if (!released) File.WriteAllText(directory + "/status.txt", "FAIL: fallback still valid at shutdown");
            Application.quitting -= AuditQuit;
        }
        Application.quitting += AuditQuit;
        yield return Capture(directory, "01-production-before-transition");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Require(retained.IsValid(), "Normal GC must not dispose a live rendering resource.");
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        Record("Bunker");
        Require(ReferenceEquals(retained, ReadBuffer()) && retained.IsValid(), "Scene change must preserve the live fallback.");
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        Record("MVP second");
        Require(ReferenceEquals(retained, ReadBuffer()) && retained.IsValid(), "Returning to production must retain the same valid resource.");
        yield return Capture(directory, "02-production-after-transition");
        trace.AppendLine("Live resource and scene transitions PASS; shutdown audit pending");
        File.WriteAllText(directory + "/runtime-checks.txt", trace.ToString());

        void Record(string stage)
        {
            var managerType = typeof(SpriteSkin).Assembly.GetType("UnityEngine.U2D.Animation.DeformationManager");
            var manager = managerType.GetField("s_Instance", Static).GetValue(null);
            object[] systems = manager != null ? (object[])managerType.GetField("m_DeformationSystems",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager) : Array.Empty<object>();
            int gpuOwners = systems.Count(s => s != null && s.GetType() == GpuType);
            int skins = Object.FindObjectsByType<SpriteSkin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            trace.AppendLine($"{stage}: bufferValid={retained.IsValid()} count={retained.count} stride={retained.stride} manager={manager != null} gpuOwners={gpuOwners} sceneSpriteSkins={skins}");
        }
    }

    private static IEnumerator Await(Func<bool> ready)
    {
        float end = Time.realtimeSinceStartup + 30f;
        while (!ready() && Time.realtimeSinceStartup < end) yield return null;
        Require(ready(), "Production transition timed out.");
    }
    private static IEnumerator Capture(string directory, string name)
    {
        string path = Path.Combine(directory, name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float end = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
        Require(File.Exists(path), "Production Game View capture missing.");
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
