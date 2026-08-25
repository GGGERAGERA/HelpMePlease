using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

public sealed class CombatFeelTestDummyTests
{
    private GameObject enemy;

    [TearDown]
    public void TearDown()
    {
        if (enemy != null)
            Object.DestroyImmediate(enemy);
    }

    [Test]
    public void MarkerIsDebugOwnedAndSuppressesProductionRewards()
    {
        CombatFeelTestDummy marker = CreateDummy(out _);

        Assert.That(marker.IsDebugOwned, Is.True);
        Assert.That(marker.SuppressesProductionRewards, Is.True);
        Assert.That(marker.Invulnerable, Is.True);
    }

    [Test]
    public void InvulnerableHitKeepsHealthButStillRaisesDamageFeedback()
    {
        CombatFeelTestDummy marker = CreateDummy(out EnemyHealth health);
        int feedbackCount = 0;
        health.OnDamageTaken = new UnityEvent();
        health.OnDamageTaken.AddListener(() => feedbackCount++);

        float before = health.CurrentHealth;
        health.TakeDamage(before * 2f, enemy.transform.position, true);

        Assert.That(marker.Invulnerable, Is.True);
        Assert.That(health.IsDead, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(before));
        Assert.That(feedbackCount, Is.EqualTo(1));
    }

    [Test]
    public void SimulatedLethalFeedbackDoesNotKillOrChangeHealth()
    {
        CreateDummy(out EnemyHealth health);
        health.OnDamageTaken = new UnityEvent();
        int feedbackCount = 0;
        health.OnDamageTaken.AddListener(() => feedbackCount++);
        float before = health.CurrentHealth;

        health.DebugSimulateFeedback(
            before * 3f, enemy.transform.position, true, true);

        Assert.That(health.IsDead, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(before));
        Assert.That(feedbackCount, Is.EqualTo(1));
    }

    [Test]
    public void ProductionCatalogResolvesEachFeelDummyArchetypeDirectly()
    {
        GameObject owner = new("Spawner");
        GameObject basic = new("Basic Production Prefab");
        GameObject elite = new("Elite Production Prefab");
        GameObject shooter = new("Shooter Production Prefab");
        try
        {
            EnemySpawner spawner = owner.AddComponent<EnemySpawner>();
            spawner.ConfigureFeelTestPrefabCatalog(basic, elite, shooter);

            Assert.That(spawner.FindDebugEnemyPrefab(
                EnemySpawner.DebugEnemyArchetype.Basic), Is.SameAs(basic));
            Assert.That(spawner.FindDebugEnemyPrefab(
                EnemySpawner.DebugEnemyArchetype.Elite), Is.SameAs(elite));
            Assert.That(spawner.FindDebugEnemyPrefab(
                EnemySpawner.DebugEnemyArchetype.Shooter), Is.SameAs(shooter));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(basic);
            Object.DestroyImmediate(elite);
            Object.DestroyImmediate(shooter);
        }
    }

    [Test]
    public void ControllerCanBeReboundWhenSpawnerAppearsAfterInitialization()
    {
        GameObject controllerObject = new("Controller");
        GameObject spawnerObject = new("Late Spawner");
        GameObject basic = new("Basic Production Prefab");
        try
        {
            CombatFeelTestDummyController controller =
                controllerObject.AddComponent<CombatFeelTestDummyController>();
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            spawner.ConfigureFeelTestPrefabCatalog(basic, null, null);

            controller.Configure(null);
            Assert.That(controller.CanSpawn(CombatFeelDummyArchetype.Basic),
                Is.False);

            controller.Configure(spawner);
            Assert.That(controller.CanSpawn(CombatFeelDummyArchetype.Basic),
                Is.True);
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(spawnerObject);
            Object.DestroyImmediate(basic);
        }
    }

    [Test]
    public void DebugSpawnReusesInitializationWithoutWaveRegistration()
    {
        GameObject spawnerObject = new("Spawner");
        GameObject productionPrefab = new("Production Enemy Prefab");
        GameObject spawned = null;
        try
        {
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            productionPrefab.AddComponent<EnemyHealth>();

            spawned = spawner.SpawnDebugEnemyAt(
                productionPrefab, new Vector3(3f, 4f, 0f));

            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned.GetComponent<EnemyHealth>(), Is.Not.Null);
            Assert.That(spawner.DebugTrackedEnemyCount, Is.Zero);
        }
        finally
        {
            if (spawned != null)
                Object.DestroyImmediate(spawned);
            Object.DestroyImmediate(productionPrefab);
            Object.DestroyImmediate(spawnerObject);
        }
    }

    private CombatFeelTestDummy CreateDummy(out EnemyHealth health)
    {
        enemy = new GameObject("Feel Dummy Test");
        health = enemy.AddComponent<EnemyHealth>();
        CombatFeelTestDummy marker = enemy.AddComponent<CombatFeelTestDummy>();
        marker.Initialize(null);
        return marker;
    }
}
