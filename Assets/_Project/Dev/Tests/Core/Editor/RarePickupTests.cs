#if UNITY_EDITOR
using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RarePickupTests
{
    [Test]
    public void ExistingPickupArtHasWorkingCollectionComponents()
    {
        foreach (string name in new[] { "p_medKit1", "p_magnet1" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Pickups/" + name + ".prefab");
            Assert.That(prefab.GetComponent("RarePickup"), Is.Not.Null,
                name + " must use the shared experience attraction and collection path.");
            Assert.That(prefab.GetComponentInChildren<SpriteRenderer>().sprite, Is.Not.Null);
        }
        var rewards = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Bootstrap/GameplayRewards.prefab");
        var data = new SerializedObject(rewards.GetComponent<ExperienceManager>());
        Assert.That(data.FindProperty("medKitPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("magnetPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("medKitDropChance").floatValue, Is.EqualTo(.004f));
        Assert.That(data.FindProperty("magnetDropChance").floatValue, Is.EqualTo(.0015f));
        Assert.That(data.FindProperty("magnetDuration").floatValue, Is.EqualTo(30f));
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab").GetComponentInChildren<MagnetHudView>(true);
        Assert.That(hud, Is.Not.Null);
        Assert.That(hud.GetComponentInChildren<Image>().sprite, Is.SameAs(
            Load("p_magnet1").GetComponentInChildren<SpriteRenderer>().sprite));
    }

    private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/_Project/prefabs/Pickups/" + name + ".prefab");
}

public sealed class RarePickupPlayTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Begin() => CoreTestSupport.BeginRun();
    private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/_Project/prefabs/Pickups/" + name + ".prefab");
    private static RarePickup Spawn(string name, Vector3 position) =>
        Object.Instantiate(Load(name), position, Quaternion.identity).GetComponent<RarePickup>();

    [UnityTest]
    public IEnumerator ProductionCollectionDropsAndMagnetLifecycle()
    {
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        foreach (var activeEnemy in EnemyHealth.ActiveInstances.ToArray())
            Object.Destroy(activeEnemy.gameObject);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<CharacterMovement2D>().enabled = false;
        var health = player.GetComponent<PlayerHealth>();
        health.DebugInvulnerable = true;
        health.SetRuntimeHealth(100f, 50f);
        var experience = ExperienceManager.Instance;
        var position = player.transform.position;
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/RarePickups");

        var medKit = Spawn("p_medKit1", position + Vector3.right);
        yield return CoreTestSupport.Await(() => medKit == null);
        Assert.That(health.CurrentHealth, Is.EqualTo(75f));
        var healingPopup = Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)
            .Single(p => p.GetComponent<TMPro.TMP_Text>().text == "+25");
        yield return null; // Let TMP build the new text before capturing its first visible frame.
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/RarePickups/healing.png");
        Vector3 popupStart = healingPopup.transform.position;
        float waitUntil = Time.time + .4f;
        yield return CoreTestSupport.Await(() => Time.time >= waitUntil);
        Color healingColor = healingPopup.GetComponent<TMPro.TMP_Text>().color;
        Assert.That(healingColor.g, Is.GreaterThan(healingColor.r));
        Assert.That(healingColor.a, Is.InRange(.25f, .7f), "Healing fades throughout its lifetime.");
        float risePixels = Camera.main.WorldToScreenPoint(healingPopup.transform.position).y -
            Camera.main.WorldToScreenPoint(popupStart).y;
        Assert.That(risePixels, Is.InRange(12f, 24f));
        waitUntil = Time.time + .5f;
        yield return CoreTestSupport.Await(() => Time.time >= waitUntil);
        Assert.That(healingPopup == null || !healingPopup.gameObject.activeSelf, Is.True);

        health.SetCurrentHealth(90);
        medKit = Spawn("p_medKit1", position);
        yield return CoreTestSupport.Await(() => medKit == null);
        Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        Assert.That(Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)
            .Any(p => p.GetComponent<TMPro.TMP_Text>().text == "+10"), Is.True);

        Vector3 waitingKitPosition = position + Vector3.right;
        var waitingKit = Spawn("p_medKit1", waitingKitPosition);
        waitUntil = Time.time + .15f;
        yield return CoreTestSupport.Await(() => Time.time >= waitUntil);
        Assert.That(waitingKit != null && waitingKit.isActiveAndEnabled, Is.True,
            "Full-health player must leave the kit on the ground.");
        Assert.That(waitingKit.transform.position, Is.EqualTo(waitingKitPosition));
        health.SetCurrentHealth(80);
        yield return CoreTestSupport.Await(() => waitingKit == null);
        Assert.That(health.CurrentHealth, Is.EqualTo(100f));

        // Force independent drops through the real enemy death callback, including XP.
        var tuning = new SerializedObject(experience);
        tuning.FindProperty("medKitDropChance").floatValue = 1f;
        tuning.FindProperty("magnetDropChance").floatValue = 1f;
        tuning.ApplyModifiedPropertiesWithoutUndo();
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab");
        var enemy = Object.Instantiate(enemyPrefab, position + Vector3.right * 12f, Quaternion.identity)
            .GetComponent<EnemyHealth>();
        yield return null;
        Vector3 deathPosition = enemy.transform.position;
        int xpBeforeDeath = ExperiencePickup.DebugActive.Count;
        enemy.TakeDamage(100000f, deathPosition);
        var drops = Object.FindObjectsByType<RarePickup>(FindObjectsSortMode.None)
            .Where(p => Vector3.Distance(p.transform.position, deathPosition) < .01f).ToArray();
        Assert.That(drops.Length, Is.EqualTo(2), "Both independent rolls must be able to succeed.");
        Assert.That(ExperiencePickup.DebugActive.Count, Is.GreaterThan(xpBeforeDeath),
            "Rare loot must retain the ordinary enemy XP drop.");
        foreach (var drop in drops) drop.Despawn();
        tuning.FindProperty("medKitDropChance").floatValue = 0f;
        tuning.FindProperty("magnetDropChance").floatValue = 0f;
        tuning.ApplyModifiedPropertiesWithoutUndo();

        var oldXp = Object.Instantiate(Load("p_Hex1"), position + Vector3.right * 100f,
            Quaternion.identity).GetComponent<ExperiencePickup>();
        yield return null;
        Vector3 oldPosition = oldXp.transform.position;
        Spawn("p_magnet1", position);
        yield return CoreTestSupport.Await(() => experience.IsGlobalMagnetActive);
        Assert.That(oldXp != null, Is.True, "XP must fly to the player before it is awarded.");
        yield return null;
        Assert.That(Vector3.Distance(oldXp.transform.position, position), Is.LessThan(
            Vector3.Distance(oldPosition, position)));
        var newXp = Object.Instantiate(Load("p_Hex1"), position + Vector3.left * 100f,
            Quaternion.identity).GetComponent<ExperiencePickup>();
        yield return null;
        Assert.That(Vector3.Distance(newXp.transform.position, position), Is.LessThan(100f),
            "New XP must inherit active global attraction.");

        Time.timeScale = 0f;
        float pausedTime = experience.MagnetSecondsRemaining;
        Vector3 pausedPosition = newXp.transform.position;
        for (int i = 0; i < 4; i++) yield return null;
        Assert.That(experience.MagnetSecondsRemaining, Is.EqualTo(pausedTime));
        Assert.That(newXp.transform.position, Is.EqualTo(pausedPosition));
        Time.timeScale = 1f;
        Spawn("p_magnet1", position);
        yield return CoreTestSupport.Await(() => experience.MagnetSecondsRemaining > pausedTime + 28f);
        yield return CoreTestSupport.Await(() => oldXp == null && newXp == null);
        var indicator = Object.FindFirstObjectByType<MagnetHudView>();
        Assert.That(indicator.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/RarePickups/magnet-hud.png");
        yield return null;

        // Expiry restores local range, including XP that was still in flight.
        experience.ClearGlobalMagnet();
        tuning.FindProperty("magnetDuration").floatValue = .12f;
        tuning.ApplyModifiedPropertiesWithoutUndo();
        experience.ActivateGlobalMagnet(health);
        Assert.That(experience.MagnetSecondsRemaining, Is.EqualTo(.12f).Within(.001f));
        var distant = Object.Instantiate(Load("p_Hex1"), position + Vector3.up * 100f,
            Quaternion.identity).GetComponent<ExperiencePickup>();
        yield return CoreTestSupport.Await(() => !experience.IsGlobalMagnetActive);
        Assert.That(experience.IsGlobalMagnetActive, Is.False);
        Vector3 afterExpiry = distant.transform.position;
        waitUntil = Time.time + .1f;
        yield return CoreTestSupport.Await(() => Time.time >= waitUntil);
        Assert.That(distant.transform.position, Is.EqualTo(afterExpiry));

        experience.ActivateGlobalMagnet(health);
        health.DebugInvulnerable = false;
        health.TakeDamage(100000f, Vector2.zero);
        Assert.That(experience.MagnetSecondsRemaining, Is.Zero, "Death must clear even on the same frame.");

        yield return CoreTestSupport.LoadBunker();
        Assert.That(ExperienceManager.Instance, Is.Null, "Bunker has no gameplay pickup owner.");
        Assert.That(Object.FindObjectsByType<RarePickup>(FindObjectsSortMode.None), Is.Empty);
    }

    [UnityTest]
    public IEnumerator ActiveEffectClearsWhenSectorIsReleased()
    {
        var experience = ExperienceManager.Instance;
        var health = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        experience.ActivateGlobalMagnet(health);
        Assert.That(experience.IsGlobalMagnetActive, Is.True);
        Assert.That(RunStateManager.Instance.ReleaseCurrentSector(RunStateManager.Instance.RunId), Is.True);
        Assert.That(experience.MagnetSecondsRemaining, Is.Zero);
        Assert.That(experience.CanUseRarePickups(experience.gameObject.scene), Is.False);
        experience.ActivateGlobalMagnet(health);
        Assert.That(experience.MagnetSecondsRemaining, Is.Zero, "Released sector cannot reactivate pickups.");
        yield return CoreTestSupport.LoadBunker();
        Assert.That(ExperienceManager.Instance, Is.Null);
    }

    [UnityTearDown]
    public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
}
#endif
