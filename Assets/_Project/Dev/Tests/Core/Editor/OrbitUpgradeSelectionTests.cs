#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitUpgradeSelectionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [TestCase(OrbitalRewardKind.RingPower)]
    [TestCase(OrbitalRewardKind.RingSpeed)]
    public void EmptyRingCanReceiveItsOwnUpgrade(OrbitalRewardKind kind)
    {
        var state = OrbitalRunState.CreateDefault(1);
        Assert.That(state.TryAddRing(out var ring, out _), Is.True);
        Assert.That(state.CanTargetRingReward(kind, ring.StableRingId), Is.True,
            "An empty new ring must remain selectable for persistent ring upgrades");
    }

    [UnityTest]
    public IEnumerator TwoRingsRequireChoiceAndClearAuthoredFocus()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        yield return VerifySelection();
    }

    private static IEnumerator VerifySelection()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset");
        var rule = AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_None.asset");
        var anomaly = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Gravity.asset");
        RunStateManager.EnsureExists().BeginNewRun(character, null, stage, rule, anomaly, true);
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => PlayerRuntimeReference.CachedPlayer != null && HUDManager.Instance.IsInformationVisible);
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(100000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var first = station.Rings[0];
        var reward = ScriptableObject.CreateInstance<OrbitalRewardData>();
        reward.RequiresArenaSelection = true;
        reward.RewardKind = OrbitalRewardKind.RingSpeed;
        bool committed = false;
        Assert.That(station.RewardFlow.Begin(reward, () => committed = true, () => {}), Is.True);
        Assert.That(committed, Is.True, "One ring must still apply immediately");
        var added = station.AddRing();
        Assert.That(added, Is.Not.Null);
        var second = station.Rings[1];
        yield return null;
        Time.timeScale = 0f; // The normal reward barrier pauses gameplay.
        foreach (var kind in new[] { OrbitalRewardKind.RingSpeed, OrbitalRewardKind.RingPower })
        {
            reward.RewardKind = kind;
            committed = false;
            int revision = station.State.Revision;
            int firstSpeed = first.State.SpeedUpgradeLevel, firstPower = first.State.PowerUpgradeLevel;
            int secondSpeed = second.State.SpeedUpgradeLevel, secondPower = second.State.PowerUpgradeLevel;
            Assert.That(station.RewardFlow.Begin(reward, () => committed = true, () => {}), Is.True);
            Assert.That(committed, Is.False, "Two rings must wait for the player's choice, including an empty ring");
            Assert.That(station.State.Revision, Is.EqualTo(revision));
            Assert.That(station.Interaction.RingSelectionFocus, Is.True);
            Assert.That(station.Interaction.ScreenEffectVisible, Is.True);
            var firstMarkers = SelectionMarkers(first);
            var secondMarkers = SelectionMarkers(second);
            Assert.That(firstMarkers.gameObject.activeSelf, Is.True);
            Assert.That(secondMarkers.gameObject.activeSelf, Is.True);
            second.Tick(0f);
            var arrow = secondMarkers.GetChild(0).GetComponent<LineRenderer>();
            Color idleColor = arrow.startColor;
            float idleScale = arrow.transform.localScale.x;
            // Inject only the pointer coordinate; use the same resolver and presentation as production.
            typeof(OrbitalRewardFlowController).GetMethod("UpdateRingHover", Private)
                .Invoke(station.RewardFlow, new object[] { second.Geometry.Position(.25f, second.Radius) });
            typeof(OrbitalRewardFlowController).GetMethod("RefreshArenaPresentation", Private).Invoke(station.RewardFlow, null);
            Assert.That(typeof(OrbitalRingRuntime).GetField("interactionHovered", Private).GetValue(second), Is.True);
            Assert.That(typeof(OrbitalRingRuntime).GetField("interactionDimmed", Private).GetValue(first), Is.True);
            second.Tick(0f);
            Assert.That(arrow.startColor, Is.Not.EqualTo(idleColor), "Hover must clearly change marker color");
            Assert.That(arrow.transform.localScale.x, Is.GreaterThan(idleScale));
            typeof(OrbitalRewardFlowController).GetMethod("SelectRingUpgrade", Private).Invoke(station.RewardFlow, null);
            Assert.That(committed, Is.True);
            Assert.That(first.State.SpeedUpgradeLevel, Is.EqualTo(firstSpeed));
            Assert.That(first.State.PowerUpgradeLevel, Is.EqualTo(firstPower));
            Assert.That(second.State.SpeedUpgradeLevel, Is.EqualTo(secondSpeed + (kind == OrbitalRewardKind.RingSpeed ? 1 : 0)));
            Assert.That(second.State.PowerUpgradeLevel, Is.EqualTo(secondPower + (kind == OrbitalRewardKind.RingPower ? 1 : 0)));
            AssertClear(station);
        }
        // Even with only one eligible ring, multiple rings must not silently auto-commit.
        first.State.SpeedUpgradeLevel = OrbitalProgressionConfig.Default.MaxSpeedUpgradeLevel;
        reward.RewardKind = OrbitalRewardKind.RingSpeed;
        committed = false;
        Assert.That(station.RewardFlow.Begin(reward, () => committed = true, () => {}), Is.True);
        Assert.That(committed, Is.False);
        Assert.That(station.RewardFlow.State, Is.EqualTo(OrbitalRewardFlowState.RingSelection));
        Assert.That(SelectionMarkers(first).gameObject.activeSelf, Is.False, "Capped rings must not advertise selection");
        Assert.That(SelectionMarkers(second).gameObject.activeSelf, Is.True);
        station.RewardFlow.CancelForSceneTransition();
        AssertClear(station);
        Object.Destroy(reward);
        Time.timeScale = 1f;
        yield return null;
    }

    private static void AssertClear(OrbitalStationRuntime station)
    {
        Assert.That(station.Interaction.RingSelectionFocus, Is.False);
        Assert.That(station.Interaction.ScreenEffectVisible, Is.False);
        foreach (var ring in station.Rings)
        {
            Assert.That(SelectionMarkers(ring).gameObject.activeSelf, Is.False);
            Assert.That(typeof(OrbitalRingRuntime).GetField("interactionHovered", Private).GetValue(ring), Is.False);
            Assert.That(typeof(OrbitalRingRuntime).GetField("interactionDimmed", Private).GetValue(ring), Is.False);
        }
    }

    private static Transform SelectionMarkers(OrbitalRingRuntime ring)
    {
        var view = (OrbitalRingView)typeof(OrbitalRingRuntime).GetField("view", Private).GetValue(ring);
        var field = typeof(OrbitalRingView).GetField("selectionMarkersRoot", Private);
        Assert.That(field, Is.Not.Null, "Selection marker references must be authored on the ring view");
        var root = (Transform)field.GetValue(view);
        Assert.That(root, Is.Not.Null);
        Assert.That(root.childCount, Is.GreaterThanOrEqualTo(3));
        return root;
    }
}
#endif
