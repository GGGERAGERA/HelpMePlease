using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PropScatterSmoke
{
    const string Output = "Artifacts/PropScatterSmoke";
    readonly Subject42FinalBossFlowTests preferences = new();
    [SetUp] public void Preserve() => preferences.PreserveRewardsAndUnlockProgress();
    [UnityTearDown] public IEnumerator Cleanup() => preferences.CleanupPlayMode();
    static string[] Layout(ProductionExplorationSectorController sector) =>
        sector.transform.Find("Sector visual props").Cast<Transform>().Select(t =>
            $"{t.name}|{t.position:R}|{t.rotation:R}|{t.localScale:R}").ToArray();

    [UnityTest]
    public IEnumerator ProductionProfileTwoSeeds()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return (IEnumerator)typeof(Subject42FinalBossFlowTests).GetMethod("StartRun",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "Gera" });
        Time.timeScale = 0;
        var sector = ProductionExplorationSectorController.ActiveInstance;
        var profile = Resources.Load<PropScatterProfile>("PropScatterProfile");
        Assert.That(profile, Is.Not.Null);
        Assert.That(sector.PropCount, Is.EqualTo(profile.totalCount));
        string[] initial = Layout(sector);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().SetRuntimeHealth(100, 100);
        var site = ProductionAnomalySite.ActiveSites.First(s => !s.IsSpecial && s.DebugZoneName.Contains("STASIS"));
        player.GetComponent<Rigidbody2D>().position = (Vector2)site.transform.position + new Vector2(6, 4);
        Physics2D.SyncTransforms();
        string rng = JsonUtility.ToJson(UnityEngine.Random.state);
        var gameplay = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.InstanceID).Select(c => c.GetInstanceID()).ToArray();
        sector.RegenerateProps();
        Assert.That(Layout(sector), Is.EqualTo(initial), "Same seed must survive player movement");
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(rng));
        int seed = sector.PropSeed;
        for (int shot = 0; shot < 2; shot++)
        {
            var root = sector.transform.Find("Sector visual props");
            Assert.That(root.GetComponentsInChildren<Collider2D>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<MonoBehaviour>(), Is.Empty);
            var points = root.Cast<Transform>().Select(t => (Vector2)t.position).ToArray();
            foreach (var p in points) Assert.That(GameplayAreaService.Instance.IsInsidePlayableArea(p), Is.True);
            for (int i = 0; i < points.Length; i++)
            for (int j = 0; j < i; j++) Assert.That(Vector2.Distance(points[i], points[j]), Is.GreaterThanOrEqualTo(profile.minDistance - .001f));
            foreach (var e in profile.entries)
            {
                int count = root.Cast<Transform>().Count(t => t.name == e.prefab.name);
                Assert.That(count, Is.InRange(e.minCount, e.maxCount));
            }
            Assert.That(Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.InstanceID).Select(c => c.GetInstanceID()).ToArray(), Is.EqualTo(gameplay));
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            for (int i = 0; i < 75; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Output + $"/seed-{shot + 1}.png");
            for (int i = 0; i < 12; i++) yield return null;
            File.AppendAllText(Output + "/checks.txt", $"PASS seed={sector.PropSeed} count={sector.PropCount}; spacing, bounds, entry limits, no gameplay components.\n");
            if (shot == 0) { sector.ChangePropSeed(1); Assert.That(Layout(sector), Is.Not.EqualTo(initial)); }
        }
        sector.ChangePropSeed(-1);
        Assert.That(sector.PropSeed, Is.EqualTo(seed));
        Assert.That(Layout(sector), Is.EqualTo(initial));
        sector.ClearProps(); Assert.That(sector.PropCount, Is.Zero);
        sector.RegenerateProps(); Assert.That(Layout(sector), Is.EqualTo(initial));
        File.AppendAllText(Output + "/checks.txt", "PASS regenerate/clear/seed +/-; original player spawn preserved; gameplay RNG unchanged.\n");
    }
}
