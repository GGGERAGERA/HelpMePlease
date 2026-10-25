using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit QA only. Reuses production actors, assets and presentation clocks.
public static class EnvironmentPaceSmoke
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

    public static IEnumerator VerifyCurrentRun(string directory)
    {
        yield return OrbitalHierarchySmoke.VerifyCurrentRun(directory + "/Combat");
        Directory.CreateDirectory(directory);
        var failures = new List<string>();
        var trace = new StringBuilder();
        trace.AppendLine($"Editor={Application.isEditor} Development={Debug.isDebugBuild} Resolution={Screen.width}x{Screen.height}");
        Time.timeScale = 0f;
        var props = GameObject.Find("Sector visual props");
        var sprites = props.GetComponentsInChildren<SpriteRenderer>();
        Check(sprites.Length > 0 && sprites.All(s => s.color.a <= .8f), "Visual scatter must blend into the ground instead of drawing opaque black outlines.");
        var material = sprites.First().sharedMaterial;
        Check(material.HasProperty("_EnvironmentSaturation") && material.GetFloat("_EnvironmentSaturation") < .9f,
            "Verify the actual production environment material, not a development-only lab override.");
        trace.AppendLine($"Props={sprites.Length} alphaMax={sprites.Max(s => s.color.a):F3} material={material.name} saturation={material.GetFloat("_EnvironmentSaturation"):F3}");
        var sector = Object.FindFirstObjectByType<ProductionExplorationSectorController>();
        var scatter = Read<object>(sector, "propScatter");
        var authoredProfile = (PropScatterProfile)scatter.GetType().GetProperty("Profile").GetValue(scatter);
        int initialCount = sector.PropCount;
        sector.RegenerateProps();
        var regenerated = GameObject.Find("Sector visual props").GetComponentsInChildren<SpriteRenderer>();
        var positions = regenerated.Select(s => s.transform.position).ToArray();
        var colors = regenerated.Select(s => s.color).ToArray();
        sector.RegenerateProps();
        var repeated = GameObject.Find("Sector visual props").GetComponentsInChildren<SpriteRenderer>();
        Check(sector.PropCount == initialCount && positions.SequenceEqual(repeated.Select(s => s.transform.position)) &&
            colors.SequenceEqual(repeated.Select(s => s.color)), "Regeneration must preserve the deterministic scatter and must not compound alpha.");
        trace.AppendLine($"Scatter regeneration count={sector.PropCount}/{authoredProfile.totalCount}, stable placement/color PASS");
        yield return null;
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        var camera = follow.ControlledCamera;
        camera.orthographicSize = 7f;
        File.WriteAllLines(directory + "/world-visuals.txt", Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(s => s.GetComponentInParent<Canvas>() == null && s.bounds.size.magnitude > 3f &&
                camera.WorldToViewportPoint(s.bounds.center).x is > -.2f and < 1.2f &&
                camera.WorldToViewportPoint(s.bounds.center).y is > -.2f and < 1.2f)
            .Select(s => $"{s.transform.root.name}/{s.name} material={s.sharedMaterial?.name} color={s.color} size={s.bounds.size}"));
        var station = player.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>();
        station.enabled = false;
        Time.timeScale = 0f;
        var owner = Object.FindFirstObjectByType<WorldEventSpawner>();
        var source = owner.EventPrefabs.OfType<CorridorEvent>().First();
        // Instantiate the authored visual subtree only; it has no event, collider or reward logic.
        var sourceView = source.GetComponentInChildren<CorridorStartView>(true);
        var invitation = Object.Instantiate(sourceView);
        invitation.transform.position = player.transform.position + Vector3.right * 4f;
        invitation.transform.rotation = Quaternion.identity;
        var prompt = Read<TMP_Text>(invitation, "prompt");
        var flow = Read<Transform[]>(invitation, "flow");
        var area = Read<SpriteRenderer>(invitation, "area");
        var language = LocalizationService.EnsureExists();
        GameLanguage savedLanguage = language.CurrentLanguage;
        try
        {
            invitation.Configure(Vector2.right);
            TickInvitation(invitation, 0f, false);
            Check(!prompt.gameObject.activeSelf && flow.All(t => !t.gameObject.activeSelf) && area.color.a < .5f,
                "Distant/blocked corridor must keep a quiet landmark without text or moving arrows.");
            yield return Capture(directory, "01-corridor-unavailable");
            foreach (var selected in new[] { GameLanguage.Russian, GameLanguage.English })
            {
                language.SetLanguage(selected);
                invitation.Configure(Vector2.right);
                TickInvitation(invitation, .1f, true);
                Check(prompt.gameObject.activeSelf && flow.All(t => t.gameObject.activeSelf), "Available corridor invitation must remain visible.");
                Check(prompt.text == language.Get("event.corridor.start"), "Existing RU/EN corridor text must be preserved.");
                trace.AppendLine($"Corridor available {selected}: {prompt.text}");
                yield return Capture(directory, "02-corridor-available-" + selected);
            }
            TickInvitation(invitation, .1f, false);
            Check(!prompt.gameObject.activeSelf && flow.All(t => !t.gameObject.activeSelf), "Blocking interaction must hide the invitation again.");
            invitation.Begin(); TickInvitation(invitation, .7f, true);
            Check(!prompt.gameObject.activeSelf && flow.All(t => !t.gameObject.activeSelf) && area.color.a < .01f,
                "Started corridor must complete its existing fade without leaving an invitation.");
        }
        finally { language.SetLanguage(savedLanguage); Object.Destroy(invitation.gameObject); }

        var relay = Object.FindObjectsByType<OrbitalRelayEvent>(FindObjectsSortMode.None).First(e => !e.IsCompleted);
        // Leave the owner enabled: its OnDisable disposes the production event.
        // An unstarted relay does not advance/render its gameplay clock.
        var presentation = relay.GetComponent<OrbitalRelayPresentation>();
        var nodes = Read<OrbitalRelayNode[]>(relay, "nodes");
        var settings = Read<OrbitalRelaySettings>(relay, "settings");
        var feedback = Read<OrbitalRelaySpawnFeedback[]>(presentation, "spawnFeedback");
        presentation.Bind(settings, nodes);
        relay.transform.position = player.transform.position + Vector3.right * 4f;
        nodes[0].transform.position = player.transform.position + new Vector3(3f, 2f);
        nodes[1].transform.position = player.transform.position + new Vector3(5f, -2f);
        var clock = new OrbitalRelayState(settings, nodes.Length, old => old == 0 ? 1 : 0);
        clock.Start();
        presentation.Render(clock.Snapshot);
        Check(ActiveFeedback(feedback).SequenceEqual(new[] { clock.Snapshot.ActiveNodeIndex }), "Relay start must flash only the actual active node.");
        foreach (var pulse in feedback) pulse.Clear();
        presentation.Render(clock.Snapshot);
        Check(!ActiveFeedback(feedback).Any(), "Rendering an unchanged relay state must not restart flashes.");
        clock.Tick(settings.ContactTime, true);
        presentation.Render(clock.Snapshot);
        Check(ActiveFeedback(feedback).SequenceEqual(new[] { clock.Snapshot.ActiveNodeIndex }), "Handoff must point only to the next node.");
        trace.AppendLine($"Relay start/handoff: state={clock.Snapshot.Phase} active={clock.Snapshot.ActiveNodeIndex} feedback={string.Join(",", ActiveFeedback(feedback))}");
        var arena = Read<OrbitalRelayArenaVisual>(presentation, "arenaVisual");
        var arenaBorder = Read<SpriteRenderer>(arena, "arenaBorder");
        var spawnOuter = Read<SpriteRenderer>(arena, "spawnOuter");
        Check(arenaBorder.color.a <= .45f && spawnOuter.color.a <= .1f, "Decorative relay ranges must remain below combat cues.");
        yield return Capture(directory, "03-relay-handoff");
        int completedNode = -1;
        while (clock.Snapshot.Phase == OrbitalRelayPhase.Stabilization)
        {
            completedNode = clock.Snapshot.ActiveNodeIndex;
            foreach (var pulse in feedback) pulse.Clear();
            clock.Tick(settings.ContactTime, true);
            // Match production: phase feedback runs before Render commits previousNode.
            if (clock.Snapshot.Phase == OrbitalRelayPhase.Transition) presentation.PlayTransition();
            presentation.Render(clock.Snapshot);
        }
        Check(clock.Snapshot.Phase == OrbitalRelayPhase.Transition &&
            ActiveFeedback(feedback).SequenceEqual(new[] { completedNode }), "Transition must acknowledge the previous active node only.");
        yield return Capture(directory, "04-relay-transition");
        foreach (var pulse in feedback) pulse.Clear();
        clock.Tick(settings.TransitionDuration, false);
        presentation.Render(clock.Snapshot);
        Check(clock.Snapshot.Phase == OrbitalRelayPhase.Bonus && clock.Snapshot.StabilizationActivations == settings.RequiredActivations &&
            clock.Snapshot.BonusActivations == 0 && ActiveFeedback(feedback).SequenceEqual(new[] { clock.Snapshot.ActiveNodeIndex }),
            "Bonus must begin on exactly its active node with the original progression counters.");
        yield return Capture(directory, "05-relay-bonus");
        presentation.Clear();
        Check(!ActiveFeedback(feedback).Any(), "Relay cleanup must release all presentation feedback.");
        trace.AppendLine($"Relay decorative alpha: arena={arenaBorder.color.a:F3} spawn={spawnOuter.color.a:F3}; settings={JsonUtility.ToJson(Read<OrbitalRelayConfig>(relay, "config"))}");
        trace.AppendLine($"Relay final phase={clock.Snapshot.Phase} stabilization={clock.Snapshot.StabilizationActivations} bonus={clock.Snapshot.BonusActivations} active={clock.Snapshot.ActiveNodeIndex}");
        trace.AppendLine(failures.Count == 0 ? "PASS" : string.Join("\n", failures));
        File.WriteAllText(directory + "/runtime-checks.txt", trace.ToString());
        Time.timeScale = 1f;
        station.enabled = true;
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        void Check(bool value, string message) { if (!value) failures.Add(message); }
    }

    private static IEnumerable<int> ActiveFeedback(OrbitalRelaySpawnFeedback[] feedback) =>
        Enumerable.Range(0, feedback.Length).Where(i => Read<SpriteRenderer>(feedback[i], "marker").enabled);

    private static void TickInvitation(CorridorStartView view, float delta, bool available)
    {
        var tick = typeof(CorridorStartView).GetMethod("Tick");
        tick.Invoke(view, tick.GetParameters().Length == 1 ? new object[] { delta } : new object[] { delta, available });
    }

    private static IEnumerator Capture(string directory, string name)
    {
        string path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float until = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        if (!File.Exists(path)) throw new IOException("Game View capture missing: " + path);
    }
}
