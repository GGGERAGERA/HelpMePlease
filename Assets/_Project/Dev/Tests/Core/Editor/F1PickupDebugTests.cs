#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class F1PickupDebugTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Begin()
    {
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        return CoreTestSupport.BeginRun();
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    private static object Call(Subject42DebugMenu menu, string method, params object[] args) =>
        typeof(Subject42DebugMenu).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, args);

    [UnityTest]
    public IEnumerator ProductionMenuSpawnsAndClearsOnlyItsOwnPickups()
    {
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        health.DebugInvulnerable = true;
        health.SetRuntimeHealth(101f, 101f);
        player.GetComponent<CharacterMovement2D>().enabled = false;
        Call(menu, "OpenMenu");
        var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic);
        Call(menu, "SelectTab", Enum.ToObject(tabType, 3), true);
        yield return null;
        var root = (GameObject)typeof(Subject42DebugMenu).GetField("menuRoot",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
        foreach (string label in new[] { "Spawn Medkit", "Spawn Magnet", "Set HP to 50%", "Spawn XP × 50", "Clear Test Pickups" })
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(button =>
                button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>().text == label),
                Is.True, "Missing F1 pickup tool: " + label);

        Button FindButton(string label) => root.GetComponentsInChildren<Button>(true).Single(button =>
            button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>().text == label);
        void Click(string label)
        {
            var button = FindButton(label);
            Assert.That(button.interactable, Is.True, label + " is disabled in a live production run.");
            button.onClick.Invoke();
        }

        var lootSource = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab").GetComponent<EnemyHealth>();
        var ordinaryXp = lootSource.SpawnLootPickup(player.transform.position + Vector3.up * 20f);
        var ordinaryKit = ExperienceManager.Instance.DebugSpawnRarePickup(RarePickup.PickupKind.MedKit,
            player.transform.position + Vector3.left * 20f);
        Click("Set HP to 50%");
        Assert.That(health.CurrentHealth, Is.EqualTo(50.5f));
        for (int i = 0; i < 2; i++) { Click("Spawn Medkit"); Click("Spawn Magnet"); }
        var tracked = (List<ExperiencePickup>)typeof(Subject42DebugMenu).GetField("testPickups",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
        Assert.That(tracked.Count, Is.EqualTo(4));
        foreach (var pickup in tracked)
        {
            Assert.That(pickup, Is.TypeOf<RarePickup>());
            Assert.That(Vector2.Distance(pickup.transform.position, player.transform.position), Is.InRange(1.49f, 2.01f));
            Assert.That(GameplayAreaService.Instance.IsInsidePlayableArea(pickup.transform.position, .2f), Is.True);
        }
        Click("Spawn XP × 50");
        Assert.That(tracked.Count, Is.EqualTo(54));
        Assert.That(tracked.Count(p => p.GetType() == typeof(ExperiencePickup)), Is.EqualTo(50));
        Assert.That(tracked.Where(p => p.GetType() == typeof(ExperiencePickup)).All(p =>
            Vector2.Distance(p.transform.position, player.transform.position) >
                player.GetComponent<PlayerPickupRadius>().CurrentRadius), Is.True);
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/RarePickups");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/RarePickups/f1-pickups.png");
        yield return null;
        var spawned = tracked.ToArray();
        Click("Clear Test Pickups");
        Assert.That(tracked, Is.Empty);
        Assert.That(spawned.All(p => p == null || !p.gameObject.activeSelf), Is.True);
        Assert.That(ordinaryXp.activeSelf && ordinaryKit.gameObject.activeSelf, Is.True,
            "Clear Test Pickups must not clear ordinary production loot.");
        Assert.That(FindButton("Clear Test Pickups").interactable, Is.False);
        Click("Spawn Medkit");
        Assert.That(tracked.Count, Is.EqualTo(1), "Spawning must work again after clearing.");
        Call(menu, "CloseMenu");
        yield return CoreTestSupport.Await(() => health.CurrentHealth > 50.5f);
        Assert.That(health.CurrentHealth, Is.EqualTo(75.75f));
        ordinaryXp.GetComponent<ExperiencePickup>().Despawn();
        ordinaryKit.Despawn();
    }
}
#endif
