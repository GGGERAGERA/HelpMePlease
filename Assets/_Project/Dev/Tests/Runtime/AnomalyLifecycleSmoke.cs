using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AnomalyLifecycleSmoke
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static WorldEvent Event(ProductionAnomalySite site) => (WorldEvent)typeof(ProductionAnomalySite)
        .GetField("activeEvent", Private).GetValue(site);

    public static IEnumerator VerifyCurrentRun(string directory)
    {
        Directory.CreateDirectory(directory);
        Object.FindFirstObjectByType<RunFlowController>().StopRunGameplay();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().SetRuntimeHealth(10000f, 10000f);
        var body = player.GetComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        body.linearVelocity = Vector2.zero;
        var site = ProductionAnomalySite.ActiveSites.First(s => !s.IsSpecial && !s.IsCompleted && Event(s) != null);
        var trace = new StringBuilder($"Editor={Application.isEditor} Development={Debug.isDebugBuild} site={site.name}\n");
        Event(site).Cancel();
        float deadline = Time.time + 8f;
        while (Event(site) == null && Time.time < deadline) yield return null;
        Require(Event(site) != null, "Ordinary failed-event retry must remain functional.");
        trace.AppendLine("Ordinary event failure respawns PASS");
        site.gameObject.SetActive(false);
        float until = Time.time + 2.3f;
        while (Time.time < until) yield return null;
        site.gameObject.SetActive(true);
        deadline = Time.time + 8f;
        while (Event(site) == null && Time.time < deadline) yield return null;
        Require(Event(site) != null && Event(site).gameObject.activeInHierarchy, "Temporary disable/resume must retain or respawn the objective.");
        trace.AppendLine("Temporary disable/resume PASS");
        var quitting = typeof(ProductionAnomalySite).GetMethod("OnApplicationQuit", Private);
        quitting?.Invoke(site, null);
        Event(site).Cancel();
        until = Time.time + 2.5f;
        while (Time.time < until) yield return null;
        bool terminalQuiet = Event(site) == null;
        trace.AppendLine("Terminal notification starts no new objective: " + terminalQuiet);
        File.WriteAllText(directory + "/runtime-checks.txt", trace.ToString());
        Require(terminalQuiet, "A terminal application-shutdown notification must not schedule another event.");
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
