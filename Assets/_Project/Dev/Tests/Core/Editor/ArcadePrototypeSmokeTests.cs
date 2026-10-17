#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Two focused checks: scoring rules, then the real scene's session lifecycle.
public sealed class ArcadePrototypeSmokeTests
{
    private static readonly string[] Keys = { "BunkerAnomalyPinballBest", "BunkerDicePokerBest" };
    [SetUp] public void Preserve()
    {
        CoreTestSupport.PreservePreferences();
        foreach (string key in Keys)
        {
            SessionState.SetBool(key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetInt(key + ".value", PlayerPrefs.GetInt(key));
            PlayerPrefs.DeleteKey(key);
        }
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        foreach (string key in Keys)
            if (SessionState.GetBool(key + ".exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(key + ".value", 0));
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
    [Test] public void PokerScoringRecognizesHandsAndRejectsBrokenStraights()
    {
        Assert.That(DicePoker.Evaluate(new[] { 2, 2, 4, 5, 6 }, DicePoker.Hand.Pair), Is.EqualTo(29));
        Assert.That(DicePoker.Evaluate(new[] { 2, 2, 4, 4, 6 }, DicePoker.Hand.TwoPairs), Is.EqualTo(48));
        Assert.That(DicePoker.Evaluate(new[] { 3, 3, 3, 5, 6 }, DicePoker.Hand.Three), Is.EqualTo(65));
        Assert.That(DicePoker.Evaluate(new[] { 1, 2, 3, 4, 5 }, DicePoker.Hand.Straight), Is.EqualTo(85));
        Assert.That(DicePoker.Evaluate(new[] { 2, 3, 4, 5, 6 }, DicePoker.Hand.Straight), Is.EqualTo(85));
        Assert.That(DicePoker.Evaluate(new[] { 1, 2, 3, 4, 6 }, DicePoker.Hand.Straight), Is.Zero);
        Assert.That(DicePoker.Evaluate(new[] { 3, 3, 3, 2, 2 }, DicePoker.Hand.FullHouse), Is.EqualTo(100));
        Assert.That(DicePoker.Evaluate(new[] { 4, 4, 4, 4, 1 }, DicePoker.Hand.Four), Is.EqualTo(125));
        Assert.That(DicePoker.Evaluate(new[] { 5, 5, 5, 5, 5 }, DicePoker.Hand.Five), Is.EqualTo(180));
    }
    [UnityTest] public IEnumerator ProductionPinballPlaysAndRestoresBunker()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        yield return PlayCabinets();
    }
    [Test] public void RestingFlippersAllowARealDrain()
    {
        var fixture = new GameObject("Pinball drain fixture"); fixture.SetActive(false);
        try
        {
            var game = fixture.AddComponent<AnomalyPinball>();
            typeof(BunkerMinigame).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, BunkerMinigameState.Running);
            Write(game, "paid", true); Write(game, "lives", 3); Write(game, "nextCoin", 100f);
            Write(game, "leftAngle", -18f); Write(game, "rightAngle", -18f);
            Write(game, "ball", new Vector2(332, 100)); Write(game, "velocity", Vector2.down * 120);
            for (int i = 0; i < 240 && !game.WaitingForLaunch; i++) game.Simulate(1f / 240, false, false);
            Assert.That(game.BallsRemaining, Is.EqualTo(2), "Released flippers must leave a real drain gap.");
        }
        finally { Object.DestroyImmediate(fixture); }
    }
    private static IEnumerator PlayCabinets()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu" &&
            !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true });
        yield return null;
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        var camera = Object.FindFirstObjectByType<CameraFollow>();
        var pinball = Object.FindFirstObjectByType<AnomalyPinball>();
        var poker = Object.FindFirstObjectByType<DicePoker>();
        Assert.That(pinball, Is.Not.Null); Assert.That(poker.enabled || poker.CanStart, Is.False, "Retained poker must be disabled in production.");
        foreach (var station in new[] { pinball.GetComponent<ArcadeGameStation>() })
        {
            var proxy = station.transform.Find("ArcadeInteraction").GetComponent<BunkerInteractableCollider>();
            Assert.That(proxy.gameObject.layer, Is.EqualTo(9));
            Assert.That(proxy.Interactable, Is.SameAs(station));
            Assert.That(station.CanInteract, Is.True);
        }
        bool cameraEnabled = camera.enabled;
        Vector3 cameraPosition = camera.transform.position;
        float zoom = camera.ControlledCamera.orthographicSize;
        var wallet = CurrencyManager.Instance;
        wallet.AddGoldExact(29 - wallet.TotalGold);
        pinball.Open(); pinball.Launch(1);
        Assert.That(pinball.WaitingForLaunch, Is.True, "A session requires 30 real gold.");
        Assert.That(wallet.TotalGold, Is.EqualTo(29));
        pinball.Close();
        wallet.AddGoldExact(1000 - wallet.TotalGold);
        Time.timeScale = .75f;
        pinball.GetComponent<ArcadeGameStation>().Interact();
        Assert.That(pinball.IsOpen && pinball.IsRunning && panels.IsAnyPanelOpen, Is.True);
        var modal = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.name == "Subject42 Arcade");
        Assert.That(modal.isRootCanvas, Is.True, "Arcade scaling must not inherit the bunker canvas size.");
        Canvas.ForceUpdateCanvases();
        Assert.That(modal.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(1180));
        Assert.That(modal.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(760));
        Assert.That(Time.timeScale, Is.Zero); Assert.That(camera.IsPresentationSuspended, Is.True);
        Assert.That(modal.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("SUBJECT") || t.text.Contains("ПИНБОЛ") || t.text.Contains("КОМБО") || t.text.Contains("ЦЕЛЬ")), Is.False);
        var field = modal.transform.Find("Play area/Pinball field").GetComponent<RectTransform>();
        Assert.That(field.rect.width * field.localScale.x, Is.GreaterThan(1000));
        Capture("pinball-ready.png");
        pinball.Launch(1);
        Assert.That(wallet.TotalGold, Is.EqualTo(970));
        pinball.Launch(1); Assert.That(wallet.TotalGold, Is.EqualTo(970), "Only the first ball pays the entry fee.");
        yield return null;
        Assert.That(modal.GetComponentsInChildren<Image>().Where(i => i.name == "Pixel disc").All(i => i.sprite != null), Is.True,
            "Quick reopen must not reuse sprites scheduled for destruction by the previous panel.");
        Capture("pinball.png");
        Assert.That((bool)Read(pinball, "coinActive"), Is.True);
        Write(pinball, "ball", (Vector2)Read(pinball, "coinPosition")); Write(pinball, "velocity", Vector2.zero);
        pinball.Simulate(1f / 240, false, false);
        Assert.That(wallet.TotalGold, Is.EqualTo(980)); Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(980));
        pinball.Simulate(1f / 240, false, false); Assert.That(wallet.TotalGold, Is.EqualTo(980), "The same coin cannot pay twice.");
        typeof(AnomalyPinball).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pinball, null);
        Capture("pinball-reward.png");
        Write(pinball, "waiting", true);
        for (int i = 0; i < 490; i++) pinball.Simulate(.01f, false, false);
        Assert.That((bool)Read(pinball, "coinActive"), Is.False, "Respawn must wait five seconds.");
        for (int i = 0; i < 20; i++) pinball.Simulate(.01f, false, false);
        Assert.That((bool)Read(pinball, "coinActive"), Is.True);
        for (int i = 0; i < 1010; i++) pinball.Simulate(.01f, false, false);
        Assert.That((bool)Read(pinball, "coinActive"), Is.False, "Uncollected coins expire after ten seconds.");
        Assert.That(wallet.TotalGold, Is.EqualTo(980), "Expiry never grants a reward.");
        for (int i = 0; i < 510; i++) pinball.Simulate(.01f, false, false);
        Assert.That((bool)Read(pinball, "coinActive"), Is.True);
        Write(pinball, "waiting", false);
        var zones = (Vector2[])Read(pinball, "zones");
        for (int i = 0; i < 2; i++)
        {
            Write(pinball, "ball", zones[i] + Vector2.right * 28); Write(pinball, "velocity", Vector2.zero);
            pinball.Simulate(1f / 240, false, false);
            Assert.That(pinball.BallVelocity.x * (i == 0 ? -1 : 1), Is.GreaterThan(2.5f), "Anomalies must noticeably pull/push, with the correct sign.");
        }
        var portals = (Vector2[])Read(pinball, "portals");
        for (int i = 0; i < 2; i++)
        {
            Write(pinball, "portalCooldown", 0f); Write(pinball, "ball", portals[i]); Write(pinball, "velocity", new Vector2(80, -150));
            pinball.Simulate(1f / 240, false, false);
            Assert.That(Vector2.Distance(pinball.BallPosition, portals[1 - i]), Is.LessThan(40));
            Assert.That(pinball.BallVelocity.magnitude, Is.GreaterThanOrEqualTo(249));
            pinball.Simulate(1f / 240, false, false);
            Assert.That(Vector2.Distance(pinball.BallPosition, portals[1 - i]), Is.LessThan(45), "The exit must not immediately teleport back.");
        }
        int beforeBumper = pinball.Score;
        Write(pinball, "ball", ((Vector2[])Read(pinball, "bumpers"))[0] + Vector2.up * 25);
        Write(pinball, "velocity", Vector2.down * 100);
        pinball.Simulate(1f / 240, false, false);
        Assert.That(pinball.Score, Is.GreaterThan(beforeBumper)); Assert.That(pinball.BallVelocity.y, Is.GreaterThan(0));
        Assert.That(wallet.TotalGold, Is.EqualTo(980), "Only gold targets award gold; normal bumpers award points.");
        Write(pinball, "ball", new Vector2(332, -20)); Write(pinball, "velocity", Vector2.down * 50);
        pinball.Simulate(1f / 240, false, false);
        Assert.That(pinball.BallsRemaining, Is.EqualTo(2));
        pinball.Launch(1); Assert.That(wallet.TotalGold, Is.EqualTo(980), "The next ball is included in the session.");
        Vector2 launch = pinball.BallPosition;
        for (int i = 0; i < 240; i++) pinball.Simulate(1f / 240, false, false);
        Assert.That(Vector2.Distance(pinball.BallPosition, launch), Is.GreaterThan(20));
        float maxHeight = pinball.BallPosition.y;
        for (int i = 0; i < 240 * 90 && pinball.IsRunning; i++)
        {
            if (pinball.WaitingForLaunch) pinball.Launch(.8f);
            var b = pinball.BallPosition;
            pinball.Simulate(1f / 240, b.x < 332 && b.y < 110, b.x >= 332 && b.y < 110);
            maxHeight = Mathf.Max(maxHeight, b.y);
            Assert.That(float.IsNaN(pinball.BallPosition.x), Is.False);
        }
        File.WriteAllText("Artifacts/GeneratedQA/Arcade/physics.txt", $"90s simulation: score={pinball.Score}, balls={pinball.BallsRemaining}, height={maxHeight}, state={pinball.State}, position={pinball.BallPosition}");
        Assert.That(maxHeight, Is.GreaterThan(350));
        Assert.That(pinball.Score, Is.GreaterThan(0), "A launched ball must reach the scoring field.");
        while (pinball.IsRunning)
        {
            if (pinball.WaitingForLaunch) pinball.Launch(1);
            Write(pinball, "ball", new Vector2(332, -20));
            pinball.Simulate(1f / 240, false, false);
        }
        Assert.That(pinball.BestScore, Is.EqualTo(pinball.Score));
        int beforeReplay = wallet.TotalGold;
        pinball.Replay(); Assert.That(pinball.Score, Is.Zero); Assert.That(pinball.BallsRemaining, Is.EqualTo(3));
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeReplay));
        pinball.Launch(1); Assert.That(wallet.TotalGold, Is.EqualTo(beforeReplay - 30), "A new session pays a fresh entry fee.");
        Assert.That(panels.TryCloseForEscape(), Is.True);
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeReplay - 30)); Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(wallet.TotalGold));
        Assert.That(Time.timeScale, Is.EqualTo(.75f)); Assert.That(camera.enabled, Is.EqualTo(cameraEnabled));
        Assert.That(camera.transform.position, Is.EqualTo(cameraPosition)); Assert.That(camera.ControlledCamera.orthographicSize, Is.EqualTo(zoom));
        Assert.That(panels.IsAnyPanelOpen, Is.False);
        Time.timeScale = 1;
    }
    private static object Read(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Write(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    internal static void Capture(string name, Canvas target = null, int width = 1180, int height = 760)
    {
        var canvas = target != null ? target : Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.name == "Subject42 Arcade");
        var go = new GameObject("Arcade capture camera", typeof(Camera));
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = 1 << 5;
        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.enabled = false;
        canvas.scaleFactor = 1;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1;
        // UI layer is needed for this isolated render, not for the overlay itself.
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
        Directory.CreateDirectory("Artifacts/GeneratedQA/Arcade");
        File.WriteAllBytes("Artifacts/GeneratedQA/Arcade/" + name, texture.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        scaler.enabled = true;
        cam.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(texture); Object.Destroy(go);
    }
}
#endif
