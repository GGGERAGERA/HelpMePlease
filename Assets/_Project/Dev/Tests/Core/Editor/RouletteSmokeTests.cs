#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RouletteSmokeTests
{
    [Test] public void ThreeColoursHaveExactSectorOddsAndGrossReturns()
    {
        Assert.That(RouletteSession.Wheel.Length, Is.EqualTo(37));
        Assert.That(RouletteSession.Wheel.Distinct().Count(), Is.EqualTo(37));
        Assert.That(RouletteSession.Wheel.Count(n => RouletteSession.ColourOf(n) == RouletteSession.Red), Is.EqualTo(18));
        Assert.That(RouletteSession.Wheel.Count(n => RouletteSession.ColourOf(n) == RouletteSession.Black), Is.EqualTo(18));
        Assert.That(RouletteSession.Wheel.Count(n => RouletteSession.ColourOf(n) == RouletteSession.Green), Is.EqualTo(1));
        for (int n = 0; n <= 36; n++)
        {
            for (int colour = 0; colour < 3; colour++)
                Assert.That(RouletteSession.ReturnMultiplier(colour, n), Is.EqualTo(RouletteSession.ColourOf(n) == colour ? colour == 2 ? 35 : 2 : 0));
            Assert.That(RouletteSession.ReturnMultiplier(37, n), Is.Zero, "Classic bets are no longer accepted.");
            int index = Array.IndexOf(RouletteSession.Wheel, n);
            foreach (float rotation in new[] { 0f, -1234.5f, 1876.2f })
                Assert.That(RouletteWheelGraphic.NumberAt(rotation, rotation + RouletteWheelGraphic.PocketAngle(index)), Is.EqualTo(n));
        }
    }
    [Test] public void GoldDebitsPayoutsRecoveryAndClassicMigration()
    {
        var fixture = new GameObject("Wallet accounting fixture");
        try
        {
            var wallet = fixture.AddComponent<CurrencyManager>(); wallet.AddGoldExact(1000 - wallet.TotalGold);
            var table = new RouletteSession(wallet);
            Assert.That(table.StartSpin(3, 25, 17) || table.StartSpin(0, 0, 1) || table.StartSpin(0, 1001, 1), Is.False);
            Assert.That(table.StartSpin(RouletteSession.Red, 25, 1), Is.True);
            Assert.That(wallet.TotalGold, Is.EqualTo(975)); Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(975));
            Assert.That(table.StartSpin(0, 25, 1) || table.Resolve(2), Is.False);
            Assert.That(table.Resolve(1), Is.True);
            Assert.That(table.LastReturn, Is.EqualTo(50)); Assert.That(table.LastDelta, Is.EqualTo(25));
            Assert.That(wallet.TotalGold, Is.EqualTo(1025)); Assert.That(table.Resolve(1), Is.False);
            table.StartSpin(RouletteSession.Black, 25, 2); table.Resolve(2);
            Assert.That(table.LastReturn, Is.EqualTo(50)); Assert.That(wallet.TotalGold, Is.EqualTo(1050));
            table.StartSpin(RouletteSession.Red, 25, 0); table.Resolve(0);
            Assert.That(table.LastReturn, Is.Zero); Assert.That(wallet.TotalGold, Is.EqualTo(1025));
            table.StartSpin(RouletteSession.Green, 10, 0);
            typeof(CurrencyManager).GetMethod("LoadGold", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wallet, null);
            var recovered = new RouletteSession(wallet);
            Assert.That(recovered.LastReturn, Is.EqualTo(350)); Assert.That(wallet.TotalGold, Is.EqualTo(1365));
            table.Close(); recovered.Close(); new RouletteSession(wallet).Close();
            Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(1365), "Recovery and stale instances cannot pay twice.");
            foreach (bool spinning in new[] { false, true })
            {
                var bets = new int[46]; bets[0] = 10;
                wallet.TryChangeGoldExact(-10, () => PlayerPrefs.SetString(RouletteSession.PendingKey,
                    JsonUtility.ToJson(new ClassicRound { version = 1, id = Guid.NewGuid().ToString("N"), bets = bets, stake = 10, spinning = spinning, outcome = 0 })));
                new RouletteSession(wallet).Close();
                Assert.That(wallet.TotalGold, Is.EqualTo(spinning ? 1715 : 1365));
            }
            wallet.AddGoldExact(-wallet.TotalGold);
            Assert.That(new RouletteSession(wallet).StartSpin(0, 1, 1), Is.False);
            Assert.That(wallet.TotalGold, Is.Zero, "Opening the table never creates a separate bankroll.");
        }
        finally { Object.DestroyImmediate(fixture); }
    }
    [Serializable] private sealed class ClassicRound { public int version, stake, outcome; public string id; public int[] bets; public bool spinning; }
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
        Assert.That(Object.FindObjectsByType<DicePoker>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single().enabled, Is.False);
        var proxy = game.transform.Find("ArcadeInteraction").GetComponent<BunkerInteractableCollider>();
        Assert.That(proxy.gameObject.layer, Is.EqualTo(9)); Assert.That(proxy.Interactable, Is.SameAs(game.GetComponent<ArcadeGameStation>()));
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>(); var camera = Object.FindFirstObjectByType<CameraFollow>();
        var zoomField = typeof(CameraFollow).GetField("userZoomOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        zoomField.SetValue(camera, .75f);
        typeof(CameraFollow).GetField("smoothedUserZoomOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, .75f);
        camera.ControlledCamera.orthographicSize += .75f;
        bool cameraEnabled = camera.enabled; Vector3 cameraPosition = camera.transform.position; float zoom = camera.ControlledCamera.orthographicSize;
        var wallet = CurrencyManager.Instance; wallet.AddGoldExact(1000 - wallet.TotalGold);
        Time.timeScale = .75f; proxy.Interactable.Interact();
        Assert.That(game.IsOpen && game.IsRunning && panels.IsAnyPanelOpen, Is.True); Assert.That(Time.timeScale, Is.Zero);
        Assert.That(game.Session.Balance, Is.EqualTo(1000)); Assert.That(game.SelectedAmount, Is.EqualTo(25));
        yield return new WaitForSecondsRealtime(.6f);
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.name == "Subject42 Arcade");
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(canvas.sortingLayerName, Is.EqualTo("UI"));
        Assert.That(game.transform.Find("ArcadeLabel").gameObject.activeSelf, Is.False);
        Assert.That(camera.ControlledCamera.orthographicSize, Is.LessThan(zoom));
        var wheel = canvas.GetComponentInChildren<RouletteWheelGraphic>();
        Assert.That(wheel.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(100)); Assert.That(wheel.canvasRenderer.cull, Is.False);
        Assert.That(canvas.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Colour ")), Is.EqualTo(3));
        Assert.That(canvas.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("SUBJECT") || t.text.Contains("ЕВРОПЕЙСКАЯ") || t.text.Contains("ЧЁТНОЕ")), Is.False);
        var redButton = canvas.GetComponentsInChildren<Button>().Single(b => b.name == "Colour 0");
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera.ControlledCamera, redButton.transform.position) };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.First().gameObject, Is.EqualTo(redButton.gameObject), "The world-space colour buttons must receive real pointer hits.");
        redButton.onClick.Invoke();
        var amount = canvas.GetComponentInChildren<TMPro.TMP_InputField>(); amount.onEndEdit.Invoke("10");
        Assert.That(game.SelectedAmount, Is.EqualTo(10)); Assert.That(wallet.TotalGold, Is.EqualTo(1000), "Choosing a wager does not debit it.");
        Capture("roulette-ready.png", camera.ControlledCamera);
        game.Spin(); Assert.That(redButton.interactable, Is.False); game.SelectColour(2); game.SetAmount(100);
        Assert.That(game.SelectedAmount, Is.EqualTo(10)); Assert.That(game.SelectedColour, Is.EqualTo(0));
        yield return CoreTestSupport.Await(() => !game.Session.Spinning);
        Assert.That(game.DisplayedBallNumber, Is.EqualTo(game.Session.LastNumber));
        int expected = 990 + 10 * RouletteSession.ReturnMultiplier(0, game.Session.LastNumber);
        Assert.That(wallet.TotalGold, Is.EqualTo(expected)); game.SkipSpin(); Assert.That(wallet.TotalGold, Is.EqualTo(expected));
        Capture("roulette-result.png", camera.ControlledCamera);
        game.Spin(); Assert.That(game.Session.Spinning, Is.True); game.SkipSpin();
        Assert.That(game.DisplayedBallNumber, Is.EqualTo(game.Session.LastNumber));
        int beforeReopen = wallet.TotalGold;
        Assert.That(panels.TryCloseForEscape(), Is.True);
        Assert.That(camera.transform.position, Is.EqualTo(cameraPosition)); Assert.That(camera.ControlledCamera.orthographicSize, Is.EqualTo(zoom));
        Assert.That(game.transform.Find("ArcadeLabel").gameObject.activeSelf, Is.True);
        game.Open(); Assert.That(game.Session.Balance, Is.EqualTo(beforeReopen));
        int zeroSeed = 0;
        while (new System.Random(zeroSeed).Next(37) != 0) zeroSeed++;
        typeof(EuropeanRoulette).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, new System.Random(zeroSeed));
        game.SelectColour(RouletteSession.Green); game.SetAmount(10); game.Spin();
        Assert.That(panels.TryCloseForEscape(), Is.True);
        Assert.That(game.IsOpen || panels.IsAnyPanelOpen, Is.False); Assert.That(camera.enabled, Is.EqualTo(cameraEnabled));
        Assert.That(camera.IsPresentationSuspended, Is.False); Assert.That((float)zoomField.GetValue(camera), Is.EqualTo(.75f));
        Assert.That(Time.timeScale, Is.EqualTo(.75f)); Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
        Assert.That(camera.ControlledCamera.orthographicSize, Is.EqualTo(zoom));
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeReopen + 340), "Green pays 35 times including the stake, even on exit.");
        game.Open(); Assert.That(game.Session.Balance, Is.EqualTo(beforeReopen + 340));
        ProductionSceneComposition.Active.PrepareForTransition();
        Assert.That(game.IsOpen || panels.IsAnyPanelOpen, Is.False); Assert.That(Time.timeScale, Is.EqualTo(.75f));
        Time.timeScale = 1;
    }
    private static void Capture(string name, Camera cam)
    {
        var rt = new RenderTexture(1280, 800, 24); var old = cam.targetTexture; var active = RenderTexture.active;
        cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
        Directory.CreateDirectory("Artifacts/GeneratedQA/Arcade"); File.WriteAllBytes("Artifacts/GeneratedQA/Arcade/" + name, texture.EncodeToPNG());
        cam.targetTexture = old; RenderTexture.active = active; rt.Release(); Object.Destroy(rt); Object.Destroy(texture);
    }
}
#endif
