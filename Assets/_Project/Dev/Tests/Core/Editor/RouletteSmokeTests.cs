#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RouletteSmokeTests
{
    [Test] public void EuropeanLayoutAndEverySupportedPayout()
    {
        int[] clockwise = { 0,32,15,19,4,21,2,25,17,34,6,27,13,36,11,30,8,23,10,5,24,16,33,1,20,14,31,9,22,18,29,7,28,12,35,3,26 };
        int[] reds = { 1,3,5,7,9,12,14,16,18,19,21,23,25,27,30,32,34,36 };
        Assert.That(RouletteSession.Wheel, Is.EqualTo(clockwise));
        for (int n = 0; n <= 36; n++)
        {
            Assert.That(RouletteSession.IsRed(n), Is.EqualTo(reds.Contains(n)));
            for (int bet = 0; bet <= 36; bet++) Assert.That(RouletteSession.ReturnMultiplier(bet, n), Is.EqualTo(n == bet ? 36 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(37, n), Is.EqualTo(reds.Contains(n) ? 2 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(38, n), Is.EqualTo(n > 0 && !reds.Contains(n) ? 2 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(39, n), Is.EqualTo(n > 0 && n % 2 == 0 ? 2 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(40, n), Is.EqualTo(n % 2 == 1 ? 2 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(41, n), Is.EqualTo(n > 0 && n <= 18 ? 2 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(42, n), Is.EqualTo(n >= 19 ? 2 : 0));
            for (int dozen = 0; dozen < 3; dozen++)
                Assert.That(RouletteSession.ReturnMultiplier(43 + dozen, n), Is.EqualTo(n > dozen * 12 && n <= (dozen + 1) * 12 ? 3 : 0));
            int index = System.Array.IndexOf(clockwise, n);
            foreach (float rotation in new[] { 0f, -1234.5f, 1876.2f })
                Assert.That(RouletteWheelGraphic.NumberAt(rotation, rotation + RouletteWheelGraphic.PocketAngle(index)), Is.EqualTo(n));
        }
    }
    [Test] public void WalletReservationsSettlementAndRestartRecovery()
    {
        var fixture = new GameObject("Wallet accounting fixture");
        try
        {
            var wallet = fixture.AddComponent<CurrencyManager>(); wallet.AddGoldExact(1000);
            var table = new RouletteSession(wallet);
            Assert.That(table.StartSpin(17) || table.Repeat() || table.Place(99, 5) || table.Place(1, 7), Is.False);
            table.Place(17, 10); table.Place(38, 25); table.Place(40, 5); table.Place(44, 10);
            Assert.That(wallet.TotalGold, Is.EqualTo(950)); Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(950));
            table.Clear(); Assert.That(wallet.TotalGold, Is.EqualTo(1000));
            table.Place(17, 10); table.Place(38, 25); table.Place(40, 5); table.Place(44, 10);
            table.StartSpin(17);
            Assert.That(table.Clear() || table.Place(0, 5) || table.Repeat() || table.StartSpin(0) || table.Resolve(18), Is.False);
            table.Resolve(17);
            Assert.That(table.LastReturn, Is.EqualTo(450)); Assert.That(table.LastDelta, Is.EqualTo(400));
            Assert.That(wallet.TotalGold, Is.EqualTo(1400)); Assert.That(table.Resolve(17), Is.False);
            table.Place(0, 100); table.Repeat();
            Assert.That(table.Stake, Is.EqualTo(50)); Assert.That(wallet.TotalGold, Is.EqualTo(1350)); Assert.That(table.Bet(0), Is.Zero);
            table.StartSpin(0); table.Resolve(0); Assert.That(table.LastReturn, Is.Zero);
            table.Close(); table.Close(); Assert.That(wallet.TotalGold, Is.EqualTo(1350));

            table.Place(1, 25); // Unspun reservations are refunded after reload, once.
            typeof(CurrencyManager).GetMethod("LoadGold", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wallet, null);
            var recovered = new RouletteSession(wallet);
            Assert.That(wallet.TotalGold, Is.EqualTo(1350)); Assert.That(table.Clear(), Is.False);
            recovered.Place(0, 10); recovered.StartSpin(0);
            typeof(CurrencyManager).GetMethod("LoadGold", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wallet, null);
            var settled = new RouletteSession(wallet);
            Assert.That(wallet.TotalGold, Is.EqualTo(1700)); Assert.That(recovered.Resolve(0), Is.False);
            settled.Close(); Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(1700));
            wallet.AddGoldExact(-wallet.TotalGold);
            var empty = new RouletteSession(wallet);
            Assert.That(empty.Balance, Is.Zero); Assert.That(empty.Place(0, 5), Is.False);
            empty.Close(); Assert.That(new RouletteSession(wallet).Balance, Is.Zero);
        }
        finally { Object.DestroyImmediate(fixture); }
    }
    [SetUp] public void Preserve()
    {
        CoreTestSupport.PreservePreferences();
        SessionState.SetBool("RouletteQA.PendingExists", PlayerPrefs.HasKey(RouletteSession.PendingKey));
        SessionState.SetString("RouletteQA.Pending", PlayerPrefs.GetString(RouletteSession.PendingKey, ""));
        PlayerPrefs.DeleteKey(RouletteSession.PendingKey); PlayerPrefs.Save();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        if (SessionState.GetBool("RouletteQA.PendingExists", false)) PlayerPrefs.SetString(RouletteSession.PendingKey, SessionState.GetString("RouletteQA.Pending", ""));
        else PlayerPrefs.DeleteKey(RouletteSession.PendingKey);
        PlayerPrefs.Save();
    }
    [UnityTest] public IEnumerator RedTablePlaysAnimatesRepeatsAndRestoresBunker()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        yield return Play();
    }
    private static IEnumerator Play()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu" &&
            !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true });
        yield return null;
        var game = Object.FindFirstObjectByType<EuropeanRoulette>(); Assert.That(game.name, Is.EqualTo("p_RouletteTable1"));
        Assert.That(Object.FindFirstObjectByType<DicePoker>().CanStart, Is.False);
        Assert.That(Object.FindObjectsByType<DicePoker>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single().enabled, Is.False);
        var proxy = game.transform.Find("ArcadeInteraction").GetComponent<BunkerInteractableCollider>();
        Assert.That(proxy.gameObject.layer, Is.EqualTo(9)); Assert.That(proxy.Interactable, Is.SameAs(game.GetComponent<ArcadeGameStation>()));
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>(); var camera = Object.FindFirstObjectByType<CameraFollow>();
        var zoomField = typeof(CameraFollow).GetField("userZoomOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        zoomField.SetValue(camera, .75f);
        typeof(CameraFollow).GetField("smoothedUserZoomOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, .75f);
        camera.ControlledCamera.orthographicSize += .75f;
        bool cameraEnabled = camera.enabled; Vector3 cameraPosition = camera.transform.position; float zoom = camera.ControlledCamera.orthographicSize;
        var wallet = CurrencyManager.Instance;
        var debug = Object.FindFirstObjectByType<Subject42DebugMenu>();
        typeof(Subject42DebugMenu).GetMethod("OpenMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(debug, null);
        var debugRoot = (GameObject)typeof(Subject42DebugMenu).GetField("menuRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(debug);
        var debugLabels = debugRoot.GetComponentsInChildren<TMPro.TMP_Text>();
        Assert.That(debugLabels.Any(t => t.text == "Бункер"), Is.True);
        Assert.That(debugLabels.Any(t => t.text.Contains("Съёмка") || t.text.Contains("SUBJECT")), Is.False);
        int beforeGrant = wallet.TotalGold;
        debugRoot.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "+100").onClick.Invoke();
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeGrant + 100));
        ArcadePrototypeSmokeTests.Capture("bunker-debug.png", debugRoot.GetComponent<Canvas>(), 1920, 1080);
        typeof(Subject42DebugMenu).GetMethod("CloseMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(debug, null);
        wallet.AddGoldExact(1000 - wallet.TotalGold);
        Time.timeScale = .75f; proxy.Interactable.Interact();
        Assert.That(game.IsOpen && game.IsRunning && panels.IsAnyPanelOpen, Is.True); Assert.That(Time.timeScale, Is.Zero);
        Assert.That(game.Session.Balance, Is.EqualTo(1000)); Assert.That(game.SelectedChip, Is.EqualTo(25));
        ArcadePrototypeSmokeTests.Capture("roulette-ready.png");
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.name == "Subject42 Arcade");
        var wheel = canvas.GetComponentInChildren<RouletteWheelGraphic>();
        Assert.That(canvas.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("SUBJECT") || t.text.Contains("ЕВРОПЕЙСКАЯ") || t.text.Contains("ПОСЛЕДНИЕ 10")), Is.False);
        var wheelMesh = wheel.canvasRenderer.GetMesh();
        Assert.That(wheelMesh.vertexCount, Is.GreaterThan(100), "The numbered wheel ring must render actual sector geometry.");
        Assert.That(wheel.canvasRenderer.cull, Is.False);
        var redButton = canvas.GetComponentsInChildren<Button>().Single(b => b.name == "Bet 37");
        redButton.onClick.Invoke(); game.SelectChip(10);
        canvas.GetComponentsInChildren<Button>().Single(b => b.name == "Bet 17").onClick.Invoke();
        Assert.That(game.Session.Stake, Is.EqualTo(35)); Assert.That(wallet.TotalGold, Is.EqualTo(965));
        ArcadePrototypeSmokeTests.Capture("roulette-bets.png");
        game.Spin(); Assert.That(redButton.interactable, Is.False); game.ClearBets(); game.PlaceBet(0);
        Assert.That(game.Session.Stake, Is.EqualTo(35));
        game.SkipSpin();
        Assert.That(game.DisplayedBallNumber, Is.EqualTo(game.Session.LastNumber));
        long returned = 25 * RouletteSession.ReturnMultiplier(37, game.Session.LastNumber) + 10 * RouletteSession.ReturnMultiplier(17, game.Session.LastNumber);
        Assert.That(wallet.TotalGold, Is.EqualTo(965 + returned));
        game.SkipSpin(); Assert.That(wallet.TotalGold, Is.EqualTo(965 + returned));
        canvas.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text is "Ещё раз" or "Again").onClick.Invoke();
        Assert.That(game.Session.Spinning, Is.True); Assert.That(game.Session.Stake, Is.EqualTo(35));
        yield return CoreTestSupport.Await(() => !game.Session.Spinning);
        Assert.That(game.DisplayedBallNumber, Is.EqualTo(game.Session.LastNumber));
        ArcadePrototypeSmokeTests.Capture("roulette-result.png");
        int beforeReopen = wallet.TotalGold;
        game.Close(); game.Open();
        Assert.That(game.Session.Balance, Is.EqualTo(beforeReopen));
        game.SelectChip(5); game.PlaceBet(0); game.Close();
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeReopen), "Unspun stakes must be refunded on exit.");
        game.Open();
        int zeroSeed = 0;
        while (new System.Random(zeroSeed).Next(37) != 0) zeroSeed++;
        typeof(EuropeanRoulette).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, new System.Random(zeroSeed));
        game.SelectChip(10);
        game.PlaceBet(0); game.Spin();
        Assert.That(panels.TryCloseForEscape(), Is.True);
        Assert.That(game.IsOpen || panels.IsAnyPanelOpen, Is.False); Assert.That(camera.enabled, Is.EqualTo(cameraEnabled));
        Assert.That(camera.IsPresentationSuspended, Is.False); Assert.That((float)zoomField.GetValue(camera), Is.EqualTo(.75f));
        Assert.That(Time.timeScale, Is.EqualTo(.75f)); Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
        Assert.That(camera.ControlledCamera.orthographicSize, Is.EqualTo(zoom));
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeReopen + 350), "Exiting a locked winning spin must settle it once.");
        game.Open(); Assert.That(game.Session.Balance, Is.EqualTo(beforeReopen + 350));
        ProductionSceneComposition.Active.PrepareForTransition();
        Assert.That(game.IsOpen || panels.IsAnyPanelOpen, Is.False); Assert.That(Time.timeScale, Is.EqualTo(.75f));
        Time.timeScale = 1;
    }
}
#endif
