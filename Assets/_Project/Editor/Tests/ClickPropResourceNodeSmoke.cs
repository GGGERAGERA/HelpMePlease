#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ClickPropResourceNodeSmoke
{
    private const string Output =
        "Artifacts/GeneratedQA/ClickPropResourceNodeSmoke";
    private readonly Subject42FinalBossFlowTests preferences = new();

    [SetUp]
    public void Preserve() => preferences.PreserveRewardsAndUnlockProgress();

    [UnityTearDown]
    public IEnumerator Cleanup() => preferences.CleanupPlayMode();

    [UnityTest]
    public IEnumerator SectorSpawnsBothClickPropsAndCollectionClosesOnce()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return (IEnumerator)typeof(Subject42FinalBossFlowTests)
            .GetMethod("StartRun", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Gera" });

        RunSmoke();
    }

    private static void RunSmoke()
    {
        ProductionExplorationSectorController sector =
            ProductionExplorationSectorController.ActiveInstance;
        Assert.That(sector, Is.Not.Null);

        ExplorationSectorConfig config = Resources.Load<ExplorationSectorConfig>(
            "ProductionRun/ExplorationSectorConfig");
        Assert.That(config, Is.Not.Null, "production exploration config");
        ResourceNode[] prefabs = config.ClickPropPrefabs;
        Assert.That(prefabs, Is.Not.Null, "ClickProp prefab array must be authored");

        var configuredNames = new string[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            ResourceNode configured = prefabs[i];
            Assert.That(configured == null, Is.False,
                $"ClickProp prefab slot {i} must resolve to ResourceNode");
            configuredNames[i] = configured.gameObject.name;
        }
        configuredNames = configuredNames.Distinct().ToArray();
        Assert.That(configuredNames.Length, Is.GreaterThanOrEqualTo(2),
            "the expandable config contains the initial distinct prefab set");

        var seen = new bool[configuredNames.Length];
        for (int spawn = 0; spawn < 12; spawn++)
        {
            if (seen.All(value => value)) break;
            int count = sector.SpawnResourceNodes();
            Assert.That(count, Is.InRange(5, 8));

            ResourceNode[] nodes = sector.GetComponentsInChildren<ResourceNode>();
            foreach (ResourceNode node in nodes)
            {
                int configuredIndex = System.Array.IndexOf(configuredNames, node.name);
                if (configuredIndex < 0) continue;
                seen[configuredIndex] = true;
                Assert.That(node.transform.Find("VisualOpened").gameObject.activeSelf,
                    Is.True, $"{node.name} starts Open");
                Assert.That(node.transform.Find("VisualClosed").gameObject.activeSelf,
                    Is.False, $"{node.name} starts not Closed");
            }
        }

        Assert.That(seen.All(value => value), Is.True,
            "both authored ClickProps must participate in random spawn");
        ResourceNode chosen = sector.GetComponentsInChildren<ResourceNode>()
            .First(node => System.Array.IndexOf(configuredNames, node.name) >= 0);
        Assert.That(chosen, Is.Not.Null);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position = chosen.transform.position;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.position = chosen.transform.position;
        Physics2D.SyncTransforms();

        int before = CurrencyManager.Instance.TotalGold;
        Assert.That(chosen.TryCollectAt(chosen.transform.position), Is.True);
        int after = CurrencyManager.Instance.TotalGold;
        Assert.That(after, Is.GreaterThan(before), "first click grants gold");
        Assert.That(chosen.gameObject.activeInHierarchy, Is.True,
            "used ClickProp remains in the sector");
        Assert.That(chosen.transform.Find("VisualOpened").gameObject.activeSelf,
            Is.False, "used ClickProp leaves Open state");
        Assert.That(chosen.transform.Find("VisualClosed").gameObject.activeSelf,
            Is.True, "used ClickProp enters Closed state");

        Assert.That(chosen.TryCollectAt(chosen.transform.position), Is.False,
            "second click is rejected");
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(after),
            "second click grants no gold");

        File.WriteAllText(Path.Combine(Output, "checks.txt"),
            $"PASS spawned={string.Join(",", configuredNames.OrderBy(x => x))}; " +
            $"count=5-8; Open->Closed; goldDelta={after - before}; repeatDelta=0.\n");
    }
}
#endif
