#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[Category("Core")]
public sealed class Subject42RewardProgressionTests
{
    [SetUp]
    public void PreservePreferences() => CoreTestSupport.PreservePreferences();

    [UnitySetUp]
    public IEnumerator BeginRun() => CoreTestSupport.BeginRun();

    [UnityTearDown]
    public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator LevelUpDisplaysAndGrantsTheClickedCard()
    {
        UpgradeManager.Instance.ShowLevelUpChoices(2);
        yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();
    }

    [UnityTest]
    public IEnumerator WorldLootChestReelGrantsExactlyTheStoppedReward()
    {
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var manager = UpgradeManager.Instance;
        UpgradeData committed = null;
        int commits = 0;
        manager.DebugRewardCommitted += reward => { committed = reward; commits++; };
        int revision = station.State.Revision;
        var bodyLevels = manager.GetEligibleNormalRewards().OfType<OrbitalRewardData>()
            .Where(reward => reward.BodyUpgrade != null)
            .ToDictionary(reward => reward.BodyUpgrade,
                reward => RunStateManager.Instance.ItemSlots.GetLevel(reward.BodyUpgrade));
        var chest = WorldLootChestSpawner.SpawnChest(Vector2.zero);
        Assert.That(chest, Is.Not.Null);
        chest.Interact();
        Assert.That(chest.State, Is.EqualTo(WorldLootChest.ChestState.Opening));
        chest.NotifyOpeningAnimationComplete();
        var reel = Object.FindFirstObjectByType<WorldLootRewardReel>();
        Assert.That(reel, Is.Not.Null);
        var stop = reel.GetComponentsInChildren<Button>(true).Single();
        yield return CoreTestSupport.Await(() => stop.isActiveAndEnabled && stop.interactable);
        stop.onClick.Invoke();
        yield return CoreTestSupport.Await(() => WorldLootRewardReel.LastStoppedUpgrade != null);
        var stopped = WorldLootRewardReel.LastStoppedUpgrade as OrbitalRewardData;
        Assert.That(stopped, Is.Not.Null);
        Assert.That(WorldLootRewardReel.LastClaimedUpgrade, Is.SameAs(stopped));
        yield return RewardScenarioAssertions.FinishReward();
        Assert.That(committed, Is.SameAs(stopped));
        Assert.That(commits, Is.EqualTo(1));
        RewardScenarioAssertions.AssertGranted(stopped, revision,
            stopped.BodyUpgrade != null ? bodyLevels[stopped.BodyUpgrade] : 0);
        Assert.That(WorldLootRewardReel.IsActive, Is.False);
    }

}

public static class RewardScenarioAssertions
{
    // Shared by the normal anomaly scenario; clicks the authored UI, not a substitute grant.
    public static IEnumerator ClickDisplayedCardAndVerifyGrant()
    {
        var manager = UpgradeManager.Instance;
        Assert.That(manager.DebugCurrentChoices, Has.Count.EqualTo(3));
        var choices = manager.DebugCurrentChoices.Cast<OrbitalRewardData>().ToArray();
        var panel = Object.FindFirstObjectByType<UpgradePanelView>();
        Assert.That(panel, Is.Not.Null);
        var cards = panel.GetComponentsInChildren<UpgradeCardView>()
            .Where(card => card.isActiveAndEnabled).ToArray();
        Assert.That(cards, Has.Length.EqualTo(choices.Length));
        yield return CoreTestSupport.Await(() => cards.All(card => card.GetComponent<Button>().IsInteractable()));
        foreach (var choice in choices)
            Assert.That(cards.Count(card => ShowsTitle(card, choice)), Is.EqualTo(1), choice.upgradeName);
        var selected = choices[0];
        var clickedCard = cards.Single(card => ShowsTitle(card, selected));
        UpgradeData committed = null;
        int commits = 0;
        manager.DebugRewardCommitted += reward => { committed = reward; commits++; };
        int revision = Object.FindFirstObjectByType<OrbitalStationRuntime>().State.Revision;
        int bodyLevel = selected.BodyUpgrade != null
            ? RunStateManager.Instance.ItemSlots.GetLevel(selected.BodyUpgrade) : 0;
        clickedCard.GetComponent<Button>().onClick.Invoke();
        yield return FinishReward();
        Assert.That(committed, Is.SameAs(selected));
        Assert.That(commits, Is.EqualTo(1));
        AssertGranted(selected, revision, bodyLevel);
    }

    private static bool ShowsTitle(UpgradeCardView card, UpgradeData reward) =>
        card.GetComponentsInChildren<TMP_Text>().Any(label =>
        {
            label.ForceMeshUpdate();
            return label.GetParsedText() == LocalizationService.EnsureExists().Get(reward.upgradeName);
        });

    public static IEnumerator FinishReward()
    {
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!UpgradeManager.Instance.IsRewardQueueIdle && Time.realtimeSinceStartup < deadline)
        {
            var state = station.RewardFlow.State;
            if (state is OrbitalRewardFlowState.RingSelection or OrbitalRewardFlowState.ModuleSelection
                or OrbitalRewardFlowState.DirectMountSelection or OrbitalRewardFlowState.SecondLinkPlacement)
                Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
            yield return null;
        }
        Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True, "Reward must finish within 20 seconds.");
    }

    public static void AssertGranted(OrbitalRewardData reward, int revision, int bodyLevel)
    {
        var state = Object.FindFirstObjectByType<OrbitalStationRuntime>().State;
        if (reward.BodyUpgrade != null)
            Assert.That(RunStateManager.Instance.ItemSlots.GetLevel(reward.BodyUpgrade), Is.EqualTo(bodyLevel + 1));
        else
            Assert.That(state.Revision, Is.GreaterThan(revision), "Committed reward must change station state.");
        Assert.That(state.Validate(out string error), Is.True, error);
    }
}
[Category("Core")]
public sealed class CasinoRewardTests
{
    [Test]
    public void CasinoLinkBonusPublishesAValidPairWithoutMutatingSource()
    {
        var original = OrbitalRunState.CreateDefault(42);
        string before = JsonUtility.ToJson(original);
        Assert.That(UpgradeManager.TryGrantPendingCasinoBonus(original,
            OrbitalSlotSymbol.Link, out var granted), Is.True);
        Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
        Assert.That(granted.Modules.Count, Is.EqualTo(original.Modules.Count + 2));
        Assert.That(granted.Modules.Skip(original.Modules.Count)
            .All(module => module.ModuleType == OrbitalModuleKind.LinkNode), Is.True);
        Assert.That(granted.ResolveLinkPairs().Count(), Is.EqualTo(1));
        Assert.That(granted.Validate(out string error), Is.True, error);
    }

}
#endif
