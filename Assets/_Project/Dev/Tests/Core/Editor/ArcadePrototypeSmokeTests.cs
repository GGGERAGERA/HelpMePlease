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
    [UnityTest] public IEnumerator CursorHighlightsAndOpensPinball()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu" &&
            !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true });
        yield return null;
        var game = Object.FindFirstObjectByType<AnomalyPinball>();
        var cursor = Object.FindFirstObjectByType<BunkerCursorInteractor>();
        var camera = (Camera)Read(cursor, "targetCamera");
        var proxy = game.transform.Find("ArcadeInteraction").GetComponent<BunkerInteractableCollider>();
        var body = game.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Element1");
        Physics2D.SyncTransforms();
        foreach (float height in new[] { body.bounds.center.y, body.bounds.max.y - .2f, body.bounds.min.y + .2f })
        {
            var point = new Vector3(body.bounds.center.x, height);
            camera.transform.position += point - camera.ScreenToWorldPoint(Input.mousePosition);
            camera.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, -10);
            typeof(BunkerCursorInteractor).GetMethod("UpdateHover", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cursor, null);
            Assert.That(Read(cursor, "current"), Is.SameAs(proxy), "The visible cabinet must be selectable at its screen and controls.");
        }
        var hover = game.GetComponent<BunkerHoverOutline>();
        var materials = (Material[])Read(hover, "materials");
        var boundsOutline = (LineRenderer)Read(hover, "boundsOutlineRenderer");
        Assert.That(materials.Any(m => m != null && m.HasProperty("_EnableOutline") && m.GetFloat("_EnableOutline") > 0) ||
            boundsOutline != null && boundsOutline.enabled, Is.True, "Hover must have an actual visible outline.");
        typeof(BunkerCursorInteractor).GetMethod("TryInteract", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cursor, null);
        Assert.That(game.IsOpen && game.IsRunning, Is.True, "Clicking the hovered cabinet must open pinball.");
        Assert.That(Object.FindFirstObjectByType<BunkerPanelManager>().TryCloseForEscape(), Is.True);
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
            Write(game, "ball", new Vector2(260, 100)); Write(game, "velocity", Vector2.down * 120);
            for (int i = 0; i < 240 && !game.WaitingForLaunch; i++) game.Simulate(1f / 240, false, false);
            Assert.That(game.BallsRemaining, Is.EqualTo(2), "Released flippers must leave a real drain gap.");
        }
        finally { Object.DestroyImmediate(fixture); }
    }
    [Test] public void RewardsStopAfterSixCoins()
    {
        var fixture = new GameObject("Pinball reward limit fixture"); fixture.SetActive(false);
        var previousWallet = CurrencyManager.Instance;
        try
        {
            var wallet = fixture.AddComponent<CurrencyManager>(); CurrencyManager.Instance = wallet;
            wallet.AddGoldExact(1000);
            var game = fixture.AddComponent<AnomalyPinball>();
            typeof(BunkerMinigame).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, BunkerMinigameState.Running);
            Write(game, "paid", true); Write(game, "lives", 3);
            var point = ((Vector2[])Read(game, "coinSpots"))[0];
            for (int i = 0; i < 7; i++)
            {
                Write(game, "coinActive", true); Write(game, "coinTime", 10f); Write(game, "coinPosition", point);
                Write(game, "ball", point); Write(game, "velocity", Vector2.zero);
                game.Simulate(1f / 240, false, false);
            }
            Assert.That(wallet.TotalGold, Is.EqualTo(1060), "A paid party may return more than its entry fee, but cannot farm gold indefinitely.");
            Write(game, "waiting", true); Write(game, "coinActive", false); Write(game, "nextCoin", .01f);
            game.Simulate(.02f, false, false);
            Assert.That((bool)Read(game, "coinActive"), Is.False, "The reward budget must also prevent spawning more coins.");
        }
        finally { CurrencyManager.Instance = previousWallet; Object.DestroyImmediate(fixture); }
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
        Assert.That(field.rect.height * field.localScale.y, Is.GreaterThanOrEqualTo(640));
        Assert.That(field.rect.height, Is.GreaterThan(field.rect.width), "The table should use a taller pinball layout without stretching sprites.");
        Assert.That(field.localScale.x, Is.EqualTo(field.localScale.y));
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
        typeof(AnomalyPinball).GetMethod("RefreshStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pinball, null);
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
        ExerciseTableMechanics(pinball, wallet);
        int afterMechanicsGold = wallet.TotalGold;
        Write(pinball, "ball", new Vector2(260, -20)); Write(pinball, "velocity", Vector2.down * 50);
        pinball.Simulate(1f / 240, false, false);
        Assert.That(pinball.BallsRemaining, Is.EqualTo(2));
        pinball.Launch(1); Assert.That(wallet.TotalGold, Is.EqualTo(afterMechanicsGold), "The next ball is included in the session.");
        Vector2 launch = pinball.BallPosition;
        for (int i = 0; i < 240; i++) pinball.Simulate(1f / 240, false, false);
        Assert.That(Vector2.Distance(pinball.BallPosition, launch), Is.GreaterThan(20));
        float maxHeight = pinball.BallPosition.y;
        for (int i = 0; i < 240 * 90 && pinball.IsRunning; i++)
        {
            if (pinball.WaitingForLaunch) pinball.Launch(.8f);
            var b = pinball.BallPosition;
            pinball.Simulate(1f / 240, b.x < 260 && b.y < 125, b.x >= 260 && b.y < 125);
            maxHeight = Mathf.Max(maxHeight, b.y);
            Assert.That(float.IsNaN(pinball.BallPosition.x), Is.False);
        }
        File.WriteAllText("Artifacts/GeneratedQA/Arcade/physics.txt", $"90s simulation: score={pinball.Score}, balls={pinball.BallsRemaining}, height={maxHeight}, state={pinball.State}, position={pinball.BallPosition}");
        Assert.That(maxHeight, Is.GreaterThan(550));
        Assert.That(pinball.Score, Is.GreaterThan(0), "A launched ball must reach the scoring field.");
        while (pinball.IsRunning)
        {
            if (pinball.WaitingForLaunch) pinball.Launch(1);
            Write(pinball, "bonusActive", false);
            Write(pinball, "ball", new Vector2(260, -20));
            pinball.Simulate(1f / 240, false, false);
        }
        Assert.That(pinball.BestScore, Is.EqualTo(pinball.Score));
        Assert.That(wallet.TotalGold, Is.LessThanOrEqualTo(1030), "A session must never award more than sixty gold.");
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
    private static void ExerciseTableMechanics(AnomalyPinball game, CurrencyManager wallet)
    {
        const float step = 1f / 240;
        foreach (float x in new[] { 46f, 470f })
        {
            Write(game, "ball", new Vector2(x, 210)); Write(game, "velocity", Vector2.up * 100);
            game.Simulate(step, false, false);
            Assert.That(game.BallVelocity.y, Is.GreaterThanOrEqualTo(579), "Both side returns must propel an upward shot toward the upper playfield.");
            float height = game.BallPosition.y;
            for (int i = 0; i < 300; i++) { game.Simulate(step, false, false); height = Mathf.Max(height, game.BallPosition.y); }
            Assert.That(height, Is.GreaterThan(580));
        }
        Write(game, "ball", new Vector2(115, 164)); Write(game, "velocity", new Vector2(180, -80));
        int score = game.Score;
        game.Simulate(step, false, false);
        Assert.That(game.Score, Is.GreaterThan(score), "Slingshots must kick and score, rather than being decorative triangles.");
        float angle = (float)typeof(AnomalyPinball).GetProperty("SpinnerAngle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Vector2 axis = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Vector2 normal = new(-axis.y, axis.x);
        Write(game, "ball", new Vector2(260, 300) + axis * 20 + normal * 8); Write(game, "velocity", -normal * 150);
        score = game.Score; game.Simulate(step, false, false);
        Assert.That(game.Score, Is.GreaterThan(score)); Assert.That((float)Read(game, "spinnerFlash"), Is.GreaterThan(0));

        void CompleteBank()
        {
            foreach (var target in (Vector2[])Read(game, "targets"))
            {
                Write(game, "ball", target + Vector2.down * 8); Write(game, "velocity", Vector2.up * 180);
                game.Simulate(step, false, false);
            }
        }
        System.Array.Clear((bool[])Read(game, "targetDown"), 0, 3);
        CompleteBank();
        Assert.That(game.ActiveBallCount, Is.EqualTo(2)); Assert.That(game.MultiballRemaining, Is.GreaterThan(14.9f));
        int beforeBonusCoin = wallet.TotalGold;
        Write(game, "coinActive", true); Write(game, "coinTime", 10f); Write(game, "coinPosition", new Vector2(260, 365));
        Write(game, "ball", new Vector2(260, 150)); Write(game, "velocity", Vector2.zero);
        SetExtra(game, new Vector2(260, 365), Vector2.zero);
        game.Simulate(step, false, false);
        Assert.That(wallet.TotalGold, Is.EqualTo(beforeBonusCoin + 10), "The second ball must collect money through the same exact-once path.");
        var portals = (Vector2[])Read(game, "portals");
        Write(game, "portalCooldown", 0f); Write(game, "ball", portals[0]); Write(game, "velocity", Vector2.down * 300);
        SetExtra(game, portals[1], Vector2.down * 300);
        game.Simulate(step, false, false);
        Assert.That(Vector2.Distance(game.BallPosition, portals[1]), Is.LessThan(40));
        Assert.That(Vector2.Distance(ExtraVector(game, "Position"), portals[0]), Is.LessThan(40), "Portal cooldown must be independent for each ball.");
        Write(game, "leftAngle", -18f);
        var tipMethod = typeof(AnomalyPinball).GetMethod("Tip", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector2 Contact(float flipperAngle)
        {
            Vector2 pivot = new(165, 60), tip = (Vector2)tipMethod.Invoke(game, new object[] { true, flipperAngle });
            Vector2 edge = tip - pivot, up = new Vector2(-edge.y, edge.x).normalized;
            return pivot + edge * .7f + up * 8;
        }
        Write(game, "ball", new Vector2(260, 150)); Write(game, "velocity", Vector2.zero);
        SetExtra(game, Contact(-18), Vector2.down * 180);
        game.Simulate(step, true, false);
        Assert.That(ExtraVector(game, "Velocity").y, Is.GreaterThan(400), "The bonus ball must receive full flipper impulses.");
        typeof(AnomalyPinball).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        typeof(AnomalyPinball).GetMethod("RefreshStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        Capture("pinball-multiball.png");
        Write(game, "waiting", true);
        for (int i = 0; i < 200; i++) game.Simulate(.05f, false, false);
        Assert.That(game.MultiballRemaining, Is.GreaterThan(4.8f));
        for (int i = 0; i < 102; i++) game.Simulate(.05f, false, false);
        Assert.That(game.MultiballRemaining, Is.Zero);
        for (int i = 0; i < 62; i++) game.Simulate(.05f, false, false);
        Assert.That(((bool[])Read(game, "targetDown")).Any(down => down), Is.False, "The target bank must rearm after the bonus.");
        Write(game, "waiting", false);
        CompleteBank();
        int lives = game.BallsRemaining;
        Write(game, "ball", new Vector2(260, -20)); Write(game, "velocity", Vector2.down * 100);
        SetExtra(game, new Vector2(260, 365), Vector2.up * 200);
        game.Simulate(step, false, false);
        Assert.That(game.BallsRemaining, Is.EqualTo(lives)); Assert.That(game.ActiveBallCount, Is.EqualTo(1));
        Assert.That(game.WaitingForLaunch, Is.False, "Losing the first multiball must promote the survivor without charging another life.");
        foreach (float flipperAngle in new[] { -18f, 23f })
        {
            Write(game, "leftAngle", flipperAngle); Write(game, "ball", Contact(flipperAngle)); Write(game, "velocity", Vector2.down * 180);
            game.Simulate(step, true, false);
            Assert.That(game.BallVelocity.y, Is.GreaterThan(400));
            Assert.That(game.BallVelocity.x * (flipperAngle < 0 ? 1 : -1), Is.GreaterThan(40), "Contact timing must allow different aimed shots.");
            Assert.That(game.BallVelocity.magnitude, Is.LessThanOrEqualTo(769));
        }
    }
    private static object Read(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Write(object o, string name, object value)
    {
        string member = name switch { "ball" => "Position", "velocity" => "Velocity", "portalCooldown" => "PortalCooldown", _ => null };
        if (member != null)
        {
            var state = Read(o, "primary"); state.GetType().GetField(member).SetValue(state, value);
        }
        else o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    }
    private static void SetExtra(AnomalyPinball game, Vector2 position, Vector2 velocity)
    {
        var state = Read(game, "secondary");
        state.GetType().GetField("Position").SetValue(state, position); state.GetType().GetField("Velocity").SetValue(state, velocity);
        state.GetType().GetField("PortalCooldown").SetValue(state, 0f);
    }
    private static Vector2 ExtraVector(AnomalyPinball game, string member)
    {
        var state = Read(game, "secondary"); return (Vector2)state.GetType().GetField(member).GetValue(state);
    }
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
