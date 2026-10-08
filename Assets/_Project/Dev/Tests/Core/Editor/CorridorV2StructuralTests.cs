#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CorridorStructuralTests
{
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    [UnityTest] public IEnumerator ExistingTerritorySurvivesStartAndCancel()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        Assert.That(owner.SpawnNormalAnomaly(), Is.True);
        var zone = owner.SpawnedSites[0].AnomalyZone;
        var lab = owner.GetComponent<CorridorLab>();
        typeof(CorridorLab).GetField("crowd", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, false);
        lab.StartCorridor(); yield return null;
        Assert.That(zone != null && zone.gameObject.activeInHierarchy, Is.True,
            "Starting Corridor must preserve independent anomaly territory.");
        Vector2 position = owner.Player.GetComponent<Rigidbody2D>().position;
        owner.ClearEvents(); yield return null;
        Assert.That(zone != null && zone.FocusArea.enabled, Is.True);
        Assert.That(Vector2.Distance(owner.Player.GetComponent<Rigidbody2D>().position, position), Is.LessThan(.1f),
            "Corridor cleanup must not teleport the player.");
    }
    [UnityTest] public IEnumerator IndependentTerritoryCommandDoesNotStartOrMoveCorridor()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        var lab = owner.GetComponent<CorridorLab>();
        typeof(CorridorLab).GetField("crowd", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, false);
        var position = owner.Player.position;
        lab.SpawnIndependentTerritory(); yield return null;
        Assert.That(lab.Current, Is.Null, "F9 spawns an independent world territory, without starting Corridor.");
        Assert.That(owner.Player.position, Is.EqualTo(position));
        var site = owner.SpawnedSites.Single();
        Assert.That(site.transform.parent, Is.Null);
        site.transform.position = new Vector3(100, 100, 0);
        Physics2D.SyncTransforms();
        lab.Tuning.routePreset = CorridorRouteMode.Straight;
        var expected = new CorridorRoute(lab.Tuning);
        lab.StartCorridor(); yield return null;
        CollectionAssert.AreEqual(expected.Vertices, lab.Current.Route.Vertices,
            "Corridor placement must not depend on anomaly locations.");
        owner.ClearEvents(); yield return null;
        Assert.That(site != null && site.AnomalyZone.FocusArea.enabled, Is.True);
    }
    [Test] public void CheckpointPresentationHasDistinctStates()
    {
        var kit = AssetDatabase.LoadAssetAtPath<CorridorKit>("Assets/_Project/Data/WorldEvents/Corridor/CorridorKit.asset");
        var gate = Object.Instantiate(kit.gate);
        try
        {
            var view = gate.GetComponent<CorridorNodeView>();
            Assert.That(view, Is.Not.Null);
            var visuals = gate.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(visuals, Is.Not.Empty);
            float Brightness() => visuals.Where(visual=>visual.enabled&&visual.gameObject.activeInHierarchy)
                .Sum(visual=>visual.color.maxColorComponent*visual.color.a);
            view.SetState(1);
            float activeBrightness = Brightness();
            Assert.That(activeBrightness, Is.GreaterThan(0));
            view.SetState(2);
            float completedBrightness = Brightness();
            Assert.That(completedBrightness, Is.GreaterThan(0));
            Assert.That(activeBrightness, Is.GreaterThan(completedBrightness));
            view.SetState(0);
            Assert.That(Brightness(), Is.LessThan(completedBrightness));
            view.SetState(1);
            Assert.That(Brightness(), Is.EqualTo(activeBrightness).Within(.0001f), "Reactivation restores the active presentation.");
        }
        finally { Object.DestroyImmediate(gate); }
    }
    [Test] public void KitContainsAuthoredVisualsAndPhysics()
    {
        var kit = AssetDatabase.LoadAssetAtPath<CorridorKit>("Assets/_Project/Data/WorldEvents/Corridor/CorridorKit.asset");
        Assert.That(kit, Is.Not.Null);
        foreach (var prefab in new[] { kit.straight, kit.corner, kit.cap, kit.gate, kit.exit, kit.collapse, kit.reclaimed, kit.hud })
        {
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(prefab), Is.True);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length + prefab.GetComponentsInChildren<Canvas>(true).Length, Is.GreaterThan(0));
        }
        Assert.That(kit.straight.GetComponentsInChildren<Collider2D>(true).All(c => !c.isTrigger), Is.True);
        Assert.That(kit.hud.GetComponent<CorridorHudView>(), Is.Not.Null);
    }
    [UnityTest, Timeout(60000)] public IEnumerator GatePresentationStatesAcrossPresetsAndRotations()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseGatePresentation();
    }
    private static IEnumerator ExerciseGatePresentation()
    {
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        var lab = owner.GetComponent<CorridorLab>();
        typeof(CorridorLab).GetField("crowd", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, false);
        typeof(WorldSystemsLabController).GetField("panelVisible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, false);
        var body = owner.Player.GetComponent<Rigidbody2D>();
        owner.Player.GetComponent<CharacterMovement2D>().MovementIntent = () => Vector2.zero;
        int turns = 0;
        foreach (CorridorRouteMode preset in System.Enum.GetValues(typeof(CorridorRouteMode)))
        {
            lab.Tuning.routePreset = preset; lab.Tuning.routeSeed = 157;
            typeof(CorridorLab).GetField("turns", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, turns++);
            lab.StartCorridor(); yield return null;
            var game = lab.Current;
            var point = game.Route.Point(game.Route.CheckpointDistance(0), out var forward);
            var gate = game.GetComponentsInChildren<CorridorNodeView>(true)
                .OrderBy(view=>Vector2.Distance(view.transform.position,point)).First();
            var trigger = gate.GetComponentInChildren<BoxCollider2D>();
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(Vector2.Dot(gate.transform.right, forward), Is.GreaterThan(.99f));
            body.position = point - forward * 2f; owner.Player.position = body.position;
            Physics2D.SyncTransforms(); yield return null; yield return null;
            Assert.That(game.State.Completed, Is.Zero);
            yield return CaptureGate("gate-" + preset + "-active");
            body.position = point + forward; owner.Player.position = body.position;
            Physics2D.SyncTransforms(); yield return null; yield return null;
            Assert.That(game.State.Completed, Is.EqualTo(1), "New artwork preserves physical crossing.");
            yield return CaptureGate("gate-" + preset + "-completed");
            owner.ClearEvents(); yield return null;
        }
    }
    private static IEnumerator CaptureGate(string name)
    {
        // The test teleports between sample points; let the normal following camera settle.
        float settle = Time.time + .65f;
        while (Time.time < settle) yield return null;
        Directory.CreateDirectory("Artifacts/GeneratedQA/CorridorV2");
        string path = "Artifacts/GeneratedQA/CorridorV2/" + name + ".png";
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
    }
    [UnityTest, Timeout(60000)] public IEnumerator StasisOverlapPreservesEffectVisualsAndPlayerPosition()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifyStasisOverlap();
    }
    private static IEnumerator VerifyStasisOverlap()
    {
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        Debug.Log("[Corridor QA] Lab load yielded");
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        Assert.That(owner,Is.Not.Null,"Lab controller loaded");
        var lab = owner.GetComponent<CorridorLab>();
        Assert.That(lab,Is.Not.Null,"Lab Awake initialized adapter");
        Debug.Log("[Corridor QA] Lab adapter found");
        typeof(CorridorLab).GetField("crowd", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, false);
        typeof(WorldSystemsLabController).GetField("panelVisible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, false);
        Debug.Log("[Corridor QA] Lab flags set");
        Assert.That(owner.SpawnIndependentStasisTerritory(), Is.True);
        Debug.Log("[Corridor QA] Lab Stasis spawned");
        var site = owner.SpawnedSites.Single();
        Debug.Log($"[Corridor QA] site={site}");
        var zone = site.AnomalyZone;
        Debug.Log($"[Corridor QA] zone={zone}, player={owner.Player}");
        Assert.That(owner.Player,Is.Not.Null,"Lab actor reference");
        var body = owner.Player.GetComponent<Rigidbody2D>();
        var movement = owner.Player.GetComponent<CharacterMovement2D>();
        Debug.Log($"[Corridor QA] Lab actor={owner.Player}, body={body}, movement={movement}, config={lab.Tuning != null}");
        Assert.That(body,Is.Not.Null); Assert.That(movement,Is.Not.Null);
        Assert.That(lab.Tuning,Is.Not.Null,"Lab config initialization");
        lab.Tuning.routePreset = CorridorRouteMode.Straight;
        lab.StartCorridor(); yield return null;
        var game = lab.Current;
        Assert.That(game,Is.Not.Null,typeof(CorridorLab).GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lab)?.ToString());
        Assert.That(game.Route.Contains(zone.FocusArea.bounds.center), Is.True);
        var renderers = zone.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers.Any(r => r.enabled && r.gameObject.activeInHierarchy), Is.True);
        // Reuse this overlap fixture as the compact collision/hazards + Lab smoke.
        Vector2 center = game.Route.Point(6, out var forward); Vector2 side = new(-forward.y,forward.x);
        body.position = center; owner.Player.position = center; movement.MovementIntent = () => side;
        yield return new WaitForSeconds(1.2f);
        Assert.That(Vector2.Dot(body.position-center,side),Is.LessThan(game.Route.HalfWidth),"Player wall block.");
        movement.MovementIntent = null;
        CorridorMigrationTestSupport.Set(movement,"moveInput",side);
        CorridorMigrationTestSupport.Call(movement,"TryStartDash"); yield return new WaitForSeconds(.3f);
        Assert.That(game.Route.Contains(body.position),Is.True,"Dash wall block.");
        movement.MovementIntent = () => Vector2.zero;
        body.position = center; owner.Player.position = center;
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_default.prefab"),center+side*6,Quaternion.identity);
        enemy.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(100000f);
        // Isolate physical wall filtering from the player's automatic attack stun.
        enemy.GetComponent<EnemyMovement>().enabled=false;
        var enemyBody=enemy.GetComponent<Rigidbody2D>();
        // Station time control changes fixedDeltaTime; measure simulated duration rather than step count.
        float enemyUntil=Time.time+1.1f;
        while(Time.time<enemyUntil)
        { enemyBody.MovePosition(enemyBody.position-side*4*Time.fixedDeltaTime); yield return new WaitForFixedUpdate(); }
        Debug.Log($"[Corridor QA] enemy body={enemyBody.position} root={enemy.transform.position} layer={enemy.layer}; contacts="+
            string.Join(",",Physics2D.OverlapCircleAll(enemyBody.position,1f).Select(c=>$"{c.name}/{c.gameObject.layer}/{c.isTrigger}")));
        Assert.That(Vector2.Dot((Vector2)enemy.transform.position-center,side),Is.LessThan(game.Route.HalfWidth-.3f),"Enemy passes wall.");
        Object.Destroy(enemy);
        yield return new WaitForSeconds(.7f); // Existing player hit invulnerability expires before collapse damage.
        CorridorMigrationTestSupport.Move(game,body,game.Route.Vertices[0]);
        for(float d=1;d<game.Route.CheckpointDistance(0)+2;d++) CorridorMigrationTestSupport.Move(game,body,game.Route.Sample(d));
        Assert.That(game.State.Completed,Is.EqualTo(1));
        var health=owner.Player.GetComponent<PlayerHealth>(); float hp=health.CurrentHealth;
        CorridorMigrationTestSupport.Move(game,body,game.Route.Sample(0));
        Debug.Log($"[Corridor QA] pressure={game.UnderPressure} front={game.CollapseDistance} position={body.position} damage={game.Settings.collapseDamage} actor="+
            typeof(CorridorEvent).GetField("player",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game));
        float pressureDeadline=Time.time+1.2f;
        while(health.CurrentHealth>=hp&&Time.time<pressureDeadline) yield return null;
        Assert.That(health.CurrentHealth,Is.LessThan(hp),"Collapse applies actual damage.");
        var shots=(CorridorStrikes)typeof(CorridorEvent).GetField("strikes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        foreach(CorridorStrikePattern pattern in System.Enum.GetValues(typeof(CorridorStrikePattern)))
        {
            shots.Cancel(); int launched=shots.Launched;
            shots.StartPattern(pattern,game.Route.Sample(15)); shots.Tick(.1f,game.Route.Sample(15),true,false,()=>true);
            Assert.That(shots.Launched,Is.GreaterThan(launched),pattern.ToString());
        }
        var ownedWalls=game.Walls.ToArray();
        body.position = zone.FocusArea.bounds.center; owner.Player.position = body.position;
        movement.MovementIntent = () => Vector2.zero;
        Physics2D.SyncTransforms();
        float until = Time.time + .5f; while (Time.time < until) yield return null;
        var speedField = typeof(CharacterMovement2D).GetField("anomalySpeedMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(speedField, Is.Not.Null, "Stasis speed field exists.");
        float multiplier = (float)speedField.GetValue(movement);
        Assert.That(multiplier, Is.LessThan(1f), "Existing Stasis effect acts inside Corridor.");
        yield return CorridorMigrationTestSupport.Capture("lab-smoke");
        Directory.CreateDirectory("Artifacts/GeneratedQA/CorridorV2");
        string path = "Artifacts/GeneratedQA/CorridorV2/prefab-anomaly-overlap.png";
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float screenshotDeadline = Time.realtimeSinceStartup + 10;
        while (!File.Exists(path) && Time.realtimeSinceStartup < screenshotDeadline) yield return null;
        Assert.That(File.Exists(path), Is.True);
        Assert.That(game != null, Is.True, "Corridor must still be active for cancellation check.");
        Assert.That(body != null, Is.True, "Overlap must not destroy player.");
        Vector2 cancelPosition = body.position;
        game.Cancel();
        Assert.That(ownedWalls.All(wall => wall == null || !wall.enabled),Is.True,"Owned collisions immediately disabled.");
        Assert.That(shots.Pending,Is.Zero,"Owned pending hazards cancelled.");
        yield return null;
        Assert.That(game == null, Is.True);
        Assert.That(zone != null && zone.FocusArea.enabled && zone.gameObject.activeInHierarchy, Is.True);
        Assert.That(renderers.All(r => r != null), Is.True);
        Assert.That(Vector2.Distance(body.position, cancelPosition), Is.LessThan(.1f));
        multiplier = (float)speedField.GetValue(movement);
        Assert.That(multiplier, Is.LessThan(1f), "Corridor cancellation doesn't clear Stasis effect.");
        lab.StartCorridor(); yield return null;
        Assert.That(lab.Current, Is.Not.Null, "F5 creates a fresh Corridor event.");
        Assert.That(lab.Current.IsStarted, Is.True); Assert.That(zone != null, Is.True);
        Assert.That(Vector2.Distance(body.position, lab.Current.Route.Vertices[0]), Is.LessThan(.2f), "Explicit F5 restarts at START.");
        var completion=lab.Current;
        Vector2 completedPosition=Vector2.zero;
        completion.Finished += _ => completedPosition=body.position;
        CorridorMigrationTestSupport.Traverse(completion,body); yield return null;
        Assert.That(completion==null,Is.True);
        Assert.That(Vector2.Distance(body.position,completedPosition),Is.LessThan(.2f),"Normal completion leaves player at Exit.");
        Assert.That(zone!=null&&zone.FocusArea.enabled,Is.True,"Completion preserves foreign anomaly.");
        lab.StartCorridor(); yield return null;
        var failed=lab.Current;
        CorridorMigrationTestSupport.Call(failed,"OnPlayerDied"); yield return null;
        Assert.That(failed==null,Is.True);
        Assert.That(zone!=null&&zone.FocusArea.enabled,Is.True,"Failure preserves foreign anomaly.");
        owner.ClearEvents(); yield return null;
        Assert.That(zone != null && zone.FocusArea.enabled, Is.True);
    }
}
public sealed class CorridorOwnershipTests
{
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    private static IEnumerator LoadLab()
    {
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
    }
    [UnityTest] public IEnumerator StartPreservesForeignDebugEvent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return LoadLab();
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        var lab = owner.GetComponent<CorridorLab>();
        typeof(CorridorLab).GetField("crowd", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, false);
        Assert.That(owner.Events.SpawnDebugEventAt(owner.EventPrefabs[0], new Vector3(30, 30, 0), true, out var foreign), Is.True);
        lab.StartCorridor(); yield return null;
        Assert.That(foreign != null, Is.True, "F5 must preserve a foreign Lab debug event.");
        Assert.That(lab.Current != null && lab.Current.IsStarted, Is.True);
        lab.Current.Cancel(); yield return null;
        Assert.That(foreign != null, Is.True, "Corridor cancellation only removes owned content.");
        owner.ClearEvents(); yield return null;
    }
    [UnityTest] public IEnumerator FailedIndependentTerritoryInitializationLeavesNoOrphanZone()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return LoadLab();
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        var lab = owner.GetComponent<CorridorLab>();
        typeof(CorridorLab).GetField("crowd", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, false);
        lab.StartCorridor(); yield return null;
        owner.Events.ConfigureDebugConcurrentEventCapacity(1); // Force a full bootstrap capacity.
        int before = Object.FindObjectsByType<LocalAnomalyZone>(FindObjectsSortMode.None).Length;
        Assert.That(owner.SpawnIndependentStasisTerritory(), Is.False);
        yield return null;
        Assert.That(Object.FindObjectsByType<LocalAnomalyZone>(FindObjectsSortMode.None).Length, Is.EqualTo(before),
            "A rejected world-territory command must clean up its partially initialized zone.");
        Assert.That(owner.SpawnedSites, Is.Empty);
        Assert.That(lab.Current != null && lab.Current.IsStarted, Is.True);
        owner.ClearEvents(); yield return null;
    }
}
#endif
