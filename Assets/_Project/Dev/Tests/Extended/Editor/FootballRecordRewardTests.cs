using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class FootballRecordRewardTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Keys =
        { "BunkerFootballBestScore", "BunkerFootballDevRewardClaimed", "TOTAL_GOLD" };
    private readonly int?[] saved = new int?[3];
    private GameObject root;
    private CurrencyManager previousCurrency;
    private CurrencyManager currency;
    private FootballMinigame game;

    [SetUp]
    public void SetUp()
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            saved[i] = PlayerPrefs.HasKey(Keys[i]) ? PlayerPrefs.GetInt(Keys[i]) : (int?)null;
            PlayerPrefs.DeleteKey(Keys[i]);
        }
        previousCurrency = CurrencyManager.Instance;
        root = new GameObject("Football reward tests");
        root.SetActive(false);
        currency = root.AddComponent<CurrencyManager>();
        CurrencyManager.Instance = currency;
        Call(currency, "LoadGold");
        currency.AddGoldGainPercent(2f); // Fixed rewards must ignore the income multiplier.
        CreateGame();
    }

    [TearDown]
    public void TearDown()
    {
        CurrencyManager.Instance = previousCurrency;
        Object.DestroyImmediate(root);
        for (int i = 0; i < Keys.Length; i++)
            if (saved[i].HasValue) PlayerPrefs.SetInt(Keys[i], saved[i].Value);
            else PlayerPrefs.DeleteKey(Keys[i]);
        PlayerPrefs.Save();
    }

    [Category("Extended")]
    [TestCase(300, 290, false, 300, 0, false)]
    [TestCase(300, 300, false, 300, 0, false)]
    [TestCase(300, 340, false, 340, 50, false)]
    [TestCase(420, 450, false, 450, 250, true)]
    [TestCase(420, 470, false, 470, 250, true)]
    [TestCase(500, 450, false, 500, 200, true)]
    [TestCase(500, 470, true, 500, 0, true)]
    [TestCase(500, 520, true, 520, 50, true)]
    public void CompletedAttemptAwardsIndependentRecords(
        int best, int score, bool claimed, int expectedBest, int gold, bool expectedClaimed)
    {
        Seed(best, claimed);
        Finish(score);
        Assert.That(game.BestScore, Is.EqualTo(expectedBest));
        Assert.That(currency.TotalGold, Is.EqualTo(gold));
        Assert.That(PlayerPrefs.GetInt(Keys[0]), Is.EqualTo(expectedBest));
        Assert.That(PlayerPrefs.GetInt(Keys[1], 0), Is.EqualTo(expectedClaimed ? 1 : 0));
        Assert.That(PlayerPrefs.GetInt(Keys[2], 0), Is.EqualTo(gold));
        game.CompleteGame();
        game.FailGame();
        Assert.That(currency.TotalGold, Is.EqualTo(gold), "Repeated end callbacks cannot pay twice");
    }

    [Category("Extended")]
    [Test]
    public void NewBestsKeepPayingAcrossReloadAndDevPaysOnlyOnce()
    {
        Seed(300, false);
        Finish(340);
        Assert.That(currency.TotalGold, Is.EqualTo(50));
        Finish(330);
        Assert.That(currency.TotalGold, Is.EqualTo(50));
        Finish(370);
        Assert.That(currency.TotalGold, Is.EqualTo(100));
        Finish(470);
        Assert.That(currency.TotalGold, Is.EqualTo(350));

        Object.DestroyImmediate(game);
        Object.DestroyImmediate(currency);
        currency = root.AddComponent<CurrencyManager>();
        CurrencyManager.Instance = currency;
        Call(currency, "LoadGold");
        CreateGame();
        Assert.That(game.BestScore, Is.EqualTo(470));
        Assert.That(currency.TotalGold, Is.EqualTo(350));
        Finish(460);
        Assert.That(currency.TotalGold, Is.EqualTo(350));
        Finish(480);
        Assert.That(currency.TotalGold, Is.EqualTo(400));
    }

    [Category("Extended")]
    [Test]
    public void FailedAttemptUsesTheSameRecordRewards()
    {
        Seed(420, false);
        Finish(470, true);
        Assert.That(currency.TotalGold, Is.EqualTo(250));
        Assert.That(game.BestScore, Is.EqualTo(470));
    }

    private void Seed(int best, bool claimed)
    {
        PlayerPrefs.SetInt(Keys[0], best);
        PlayerPrefs.SetInt(Keys[1], claimed ? 1 : 0);
        Call(game, "Awake");
    }

    private void CreateGame()
    {
        game = root.AddComponent<FootballMinigame>();
        Set(game, "gates", Array.Empty<FootballGateScoreZone>());
        Set(game, "targetPool", Array.Empty<FootballScoreZone>());
        Call(game, "Awake");
    }

    private void Finish(int score, bool failed = false)
    {
        typeof(BunkerMinigame).GetField("<State>k__BackingField", Private)
            .SetValue(game, BunkerMinigameState.Running);
        // Each fixture represents a fresh attempt; scoring itself is unchanged.
        Set(game, "currentScore", 0);
        game.AddScore(score);
        if (failed) game.FailGame(); else game.CompleteGame();
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, Private).Invoke(target, null);
}
