#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class OrbitAnomaliesProductionVerification
{
    public const string Output = "Artifacts/GeneratedQA/OrbitAnomaliesProduction";
    const string Key = "Subject42.OrbitAnomaliesProduction.Active", Any = "Subject42.Verification.AnyRunActive";
    const string Mvp = "Assets/_Project/Scenes/MainBuild/MVP.unity";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator routine;
    static readonly List<string> report = new();
    static double deadline, next, readyAfter = EditorApplication.timeSinceStartup + 2;
    static int errors;
    static OrbitAnomaliesProductionVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report.Clear(); errors = 0; deadline = EditorApplication.timeSinceStartup + 55;
                try { BeginRun(); SceneManager.LoadSceneAsync(Mvp); routine = Checks(); }
                catch (Exception e) { report.Add(e.ToString()); Finish(false); }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(Key); SessionState.EraseBool(Any);
                var cleanup = CoreTestSupport.CleanupPlayMode(); while (cleanup.MoveNext()) { }
                EditorSceneManager.OpenScene(Mvp);
            }
        };
        Application.logMessageReceived += (message,stack,type) =>
        {
            if (routine != null && type is LogType.Error or LogType.Exception or LogType.Assert)
            { errors++; report.Add("UNITY ERROR: " + message); }
        };
    }
    [MenuItem("Tools/Subject42/QA/Five orbital anomalies in MVP")]
    public static void Run()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Preserve the edited scene before the bounded MVP scenario.");
        Directory.CreateDirectory(Output); CoreTestSupport.PreservePreferences();
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Key,true); SessionState.SetBool(Any,true); EditorApplication.EnterPlaymode();
    }
    static void BeginRun()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset");
        var rule = AssetDatabase.LoadAssetAtPath<WorldRuleData>(AssetDatabase.GUIDToAssetPath("f1012da158e24a20bb764c93aeee5266"));
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(42031);
        RunStateManager.EnsureExists().BeginNewRun(character,null,stage,rule,
            AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Stasis.asset"),true);
    }
    static void Poll()
    {
        string path = Output + "/editor.request";
        bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Any,false);
        if (busy) readyAfter = EditorApplication.timeSinceStartup + 2;
        if (File.Exists(path) && !busy && EditorApplication.timeSinceStartup >= readyAfter)
        {
            string command = File.ReadAllText(path).Trim(); File.Delete(path);
            try
            {
                if (command == "smoke") Run();
                else
                {
                    if (typeof(OrbitalStationRuntime).GetProperty("OrbitModulesSuppressed") == null)
                        throw new Exception("FAIL: production Segmented Orbit has no temporary module suppression gate.");
                    File.WriteAllText(Output + "/result.txt","PASS: production gate exists");
                }
            }
            catch (Exception e) { File.WriteAllText(Output + "/result.txt",e.ToString()); }
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Bounded MVP scenario timed out.");
            if (!routine.MoveNext()) { Finish(errors == 0); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .02);
        }
        catch (Exception e) { report.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool pass)
    {
        routine = null; report.Add(pass ? "PASS: one bounded MVP scenario; five anomalies; no runtime errors." : "FAIL");
        File.WriteAllLines(Output + "/result.txt",report); EditorApplication.ExitPlaymode();
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); report.Add("PASS: " + message); }
    static void Near(float actual, float expected, string message) => Require(Mathf.Abs(actual - expected) < .003f,message + $" ({actual:0.###} / {expected:0.###})");
    static object Field(object owner, string field) => owner.GetType().GetField(field,Hidden).GetValue(owner);
    static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method,Hidden).Invoke(owner,args);
    static void Tick(OrbitalStationRuntime station, float dt = 0f) => Call(station,"TickOrbitAnomaly",dt);
    static float Cooldown(OrbitalModuleRuntime gun) => (float)typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown",Hidden).GetValue(gun);
    static string Model(OrbitalRunState state) => Regex.Replace(JsonUtility.ToJson(state),"(\"CurrentPhase\"|\"RotationEnergy\"):-?[0-9.Ee+]+","$1:0");
    static Vector2 Neutral(LevelAnomalyController controller)
    {
        Bounds area = GameplayAreaService.Instance.PlayableArea.bounds;
        for (int y = 1; y < 20; y++) for (int x = 1; x < 20; x++)
        {
            Vector2 point = new(Mathf.Lerp(area.min.x,area.max.x,x / 20f),Mathf.Lerp(area.min.y,area.max.y,y / 20f));
            if (controller.FindOrbitZone(point) == null) return point;
        }
        throw new Exception("No neutral point in the MVP fixture.");
    }
    static void Move(OrbitalStationRuntime station, Vector2 point)
    {
        var player = station.Owner.Transform; var body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = point; body.linearVelocity = Vector2.zero; }
        player.position = new Vector3(point.x,point.y,player.position.z);
        var camera = Object.FindFirstObjectByType<CameraFollow>();
        if (camera != null) camera.transform.position = player.position + camera.offset;
        Physics2D.SyncTransforms(); Tick(station);
    }
    static Vector2 Shoot(OrbitalStationRuntime station, OrbitalProjectileModule gun)
    {
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat; adapter.DebugClearProjectiles();
        Require(gun.FireExtraShot(),"shared production FIXED shot emitted");
        var shots = (IList)Field(adapter,"projectiles");
        var active = shots.Cast<object>().Where(s => (bool)s.GetType().GetField("Active").GetValue(s)).ToArray();
        Require(active.Length > 0,"real active production projectile exists");
        var shot = active[active.Length - 1];
        Vector2 direction = (Vector2)shot.GetType().GetField("Direction").GetValue(shot);
        Require(direction.sqrMagnitude > .99f,"sampled active projectile has a unit flight direction");
        adapter.DebugClearProjectiles(); return direction;
    }
    static List<SpriteRenderer> Crosses(OrbitalStationRuntime station) => (List<SpriteRenderer>)Field(station.Interaction,"constructorWorldModuleCrosses");
    static void Render(OrbitalStationRuntime station) => Call(station.Interaction,"UpdateConstructorWorldLinks");
    static IEnumerator Checks()
    {
        while (SceneManager.GetActiveScene().name != "MVP" || Object.FindFirstObjectByType<OrbitalStationRuntime>() is not { IsInitialized:true } || SceneTransitionOverlay.IsTransitioning) yield return .02f;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var controller = LevelAnomalyController.Instance;
        Require(station.UsesSegmentedOrbit && RunStateManager.Instance.IsDevelopmentRun,"actual MVP station in isolated development run");
        Require(Object.FindFirstObjectByType<WorldSystemsLabOrbitExperiment>() == null,"no laboratory manager or A/B panel in MVP");
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) spawner.StopSpawning();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        var player = station.Owner.Transform.GetComponent<PlayerHealth>(); player.DebugInvulnerable = true;
        player.GetComponent<CharacterMovement2D>().MovementIntent = () => Vector2.zero;
        while (station.State.UnlockedSegmentedLevels < 3) Require(station.State.TryAddRing(out _,out _),"unlock an existing production level for fixture");
        while (station.State.SegmentedWeaponLimit < 12) Require(station.State.TryAddMount(station.State.Rings[0].StableRingId,out _),"grant existing weapon capacity for fixture");
        foreach (var ring in station.State.Rings)
        {
            for (int position = 0; position < ring.MountCount; position += 2)
            {
                if (station.State.Modules.Any(g => g.StableRingId == ring.StableRingId && g.MountIndex == position)) continue;
                Require(station.State.TryGrantSegmentedWeapon(OrbitalModuleKind.Pistol),"grant existing production pistol");
                Require(station.State.TryInstallSegmentedWeapon(OrbitalModuleKind.Pistol,ring.StableRingId,position,out _,out _),"install production pistol in O");
            }
            for (int position = 1; position < ring.MountCount; position += 2)
            {
                var kind = (SegmentedLinkKind)((position - 1) / 2);
                Require(station.State.TryGrantSegmentedLink(kind) && station.State.TryInstallSegmentedModule(kind,ring.StableRingId,position,out _),"install existing " + kind + " M");
            }
        }
        Require(station.RebuildRuntimeFromState(),"existing production restore builds the fixture");
        station.enabled = false; // The bounded fixture advances only the same private anomaly tick and shared combat methods.
        var state = station.State; string model = Model(state);
        var mounts = station.Rings.SelectMany(r => r.Mounts).ToDictionary(m => m,m => m.LocalPhase);
        var neutral = Neutral(controller); Move(station,neutral);
        Require(station.ActiveOrbitAnomaly == null,"neutral reference has no orbital anomaly");
        var gun = station.Modules.OfType<OrbitalProjectileModule>().First();
        var fixtures = new List<LocalAnomalyZone>();
        LocalAnomalyZone Enter(string name, LocalAnomalyType expected)
        {
            var data = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_" + name + ".asset");
            var zone = controller.SpawnSiteZone(data,Vector2.zero,new Vector2(6,6));
            Require(zone != null,"existing zone spawned: " + name); fixtures.Add(zone); Move(station,Vector2.zero);
            Require(station.ActiveOrbitAnomaly == expected,"single active orbital anomaly: " + expected); return zone;
        }
        void Exit(string label)
        {
            Move(station,neutral); Render(station);
            Require(station.ActiveOrbitAnomaly == null && !station.OrbitModulesSuppressed,label + ": effects cleared");
            Require(mounts.All(p => p.Key.LocalPhase == p.Value),label + ": exact local geometry restored");
            Require(Model(state) == model,label + ": ID, mounts, upgrades, links, inventory and saved model unchanged");
            Require(station.ValidateState(out string error),label + ": valid production state: " + error);
            foreach (var zone in fixtures) controller.CollapseSiteZone(zone); fixtures.Clear();
        }
        Enter("Stasis",LocalAnomalyType.Stasis); state.RotationEnergy = 100;
        float phase = station.Rings[0].Phase; station.TickManualRotation(1,true,false,true);
        Near(Mathf.DeltaAngle(phase,station.Rings[0].Phase),45,"blue Q/E ×0.5"); Near(station.RotationEnergy,75,"blue ordinary drain"); Shoot(station,gun); Exit("blue exit");
        Enter("Berserk",LocalAnomalyType.Berserk); state.RotationEnergy = 100;
        phase = station.Rings[0].Phase; station.TickManualRotation(1,true,false,true);
        Near(Mathf.DeltaAngle(phase,station.Rings[0].Phase),90,"red ordinary Q/E speed"); Near(station.RotationEnergy,56.25f,"red Q/E drain ×1.75"); Shoot(station,gun); Exit("red exit");
        var glitch = Enter("Glitch",LocalAnomalyType.Glitch);
        var directions = station.Modules.OfType<OrbitalProjectileModule>().ToDictionary(w => w,w => Shoot(station,w));
        var before = directions[gun];
        station.ClearLabFlashes();
        Tick(station,9.05f); Require(station.OrbitGlitchWarning,"purple weapon warning one second before shuffle");
        Require(((IList)Field(station,"flashes")).Count > 0,"warning reuses the existing flash pool");
        ScreenCapture.CaptureScreenshot(Output + "/glitch-warning.png"); yield return .12f;
        var warningLines = ((IDictionary)Field(station,"orbitWarningLines")).Keys.Cast<LineRenderer>().ToArray();
        Require(station.Interaction.OpenConstructor(),"Tab opens during a Glitch warning");
        Require(warningLines.All(line => !line.enabled) && !station.OrbitGlitchWarning,"Tab returns purple warning flashes to the shared pool before pausing");
        station.Interaction.CloseConstructor(); Tick(station,9.05f);
        Require(station.OrbitGlitchWarning,$"Glitch clock restarts after Tab (timeScale={Time.timeScale}, mode={station.InputOwner.Mode}, type={station.ActiveOrbitAnomaly}, clock={Field(station,"orbitGlitchClock")})");
        Vector3 actorPosition = player.transform.position; Call(glitch,"Pulse");
        Require(player.transform.position == actorPosition,"legacy Glitch pulse cannot move the production player");
        var cooldowns = station.Modules.ToDictionary(m => m,m => Cooldown(m));
        Tick(station,.95f); Require(!station.OrbitGlitchWarning,"shuffle occurs at 10 game seconds");
        Require(station.Rings.Any(r => r.Mounts.Any(m => m.LocalPhase != mounts[m])),"Glitch changes runtime mount phases before shooting");
        Require(directions.Any(p => Vector2.Angle(p.Value,Shoot(station,p.Key)) > 1f),"Glitch changes real projectile directions without requiring every random slot to move");
        foreach (var ring in station.Rings)
        {
            var positions = ring.Mounts.Where(m => OrbitalRunState.IsSegmentedWeaponSlot(ring.State.Order,m.MountIndex)).ToArray();
            Require(positions.Select(m => m.LocalPhase).OrderBy(v => v).SequenceEqual(positions.Select(m => mounts[m]).OrderBy(v => v)),"Glitch stays inside O mounts of level " + ring.State.Order);
        }
        Require(cooldowns.All(p => Cooldown(p.Key) == p.Value),"Glitch preserves all firing cooldowns");
        phase = station.Rings[0].Phase; state.RotationEnergy = 100; station.TickManualRotation(.1f,true,false,true); Exit("Glitch exit");
        Near(Mathf.DeltaAngle(phase,station.Rings[0].Phase),9,"Glitch exit retains player's Q/E");
        before = Shoot(station,gun); var interference = Enter("Gravity",LocalAnomalyType.Gravity); Tick(station,.7f);
        Require(interference.Presentation.Title == LocalizationService.EnsureExists().Get("world.anomaly.interference.displayName"),"normal Gravity visual has interference instructions");
        var special = (GravityZone)controller.SpawnSiteZone(AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Gravity.asset"),new Vector2(12,12),new Vector2(6,6));
        special.ConfigureOrbit(7f,3.5f,2.5f,1f,.7f,.35f);
        Require(special.Presentation.Title == LocalizationService.EnsureExists().Get("world.anomaly.gravity.displayName") && controller.FindOrbitZone(new Vector2(12,12)) != special,"special gravity site retains its own gravity instructions and physics mode");
        controller.CollapseSiteZone(special);
        float angle = Vector2.SignedAngle(before,Shoot(station,gun)); Require(Mathf.Abs(angle) > .1f && Mathf.Abs(angle) <= 17.01f,"smooth interference changes real FIXED direction within ±12° + ±5°");
        phase = station.Rings[0].Phase; station.TickManualRotation(.1f,false,true,true); Exit("interference exit");
        Near(Mathf.DeltaAngle(phase,station.Rings[0].Phase),-9,"interference exit retains Q/E");
        Require(station.Modules.Any(m => station.SegmentedDamageFactor(m.StableModuleId) > 1f) && station.Modules.Any(m => station.SegmentedRateFactor(m.StableModuleId) > 1f) && station.Modules.Any(m => station.SegmentedRicochets(m.StableModuleId) > 0),"DMG/FR/RIC bonuses active before suppression");
        Enter("ExplosiveZone",LocalAnomalyType.ExplosiveZone); Render(station);
        Require(station.OrbitModulesSuppressed,"module suppression uses the production gate");
        foreach (var weapon in station.Modules.OfType<OrbitalProjectileModule>())
        {
            Near(station.SegmentedDamageFactor(weapon.StableModuleId),1,"suppressed DMG"); Near(station.SegmentedRateFactor(weapon.StableModuleId),1,"suppressed FR");
            Require(station.SegmentedRicochets(weapon.StableModuleId) == 0,"suppressed RIC");
            int shots = weapon.ShotsFired; Call(station,"SegmentedOrdinaryShot",weapon.StableModuleId);
            Require(weapon.ShotsFired == shots,"suppressed ready 2X never fires"); Shoot(station,weapon);
        }
        Require(Crosses(station).Count(c => c.enabled) == state.SegmentedLinks.Count,"red crosses cover every installed M, including all levels");
        var crossIds = Crosses(station).Select(c => c.GetInstanceID()).ToArray(); Render(station); Render(station);
        Require(crossIds.SequenceEqual(Crosses(station).Select(c => c.GetInstanceID())),"cross objects are pooled across frames");
        Require(((List<LineRenderer>)Field(station.Interaction,"constructorWorldLines")).Where(l => l.enabled).All(l => l.startColor.a <= .21f),"active link lines are dimmed");
        ScreenCapture.CaptureScreenshot(Output + "/suppression-crosses.png"); yield return .12f;
        Exit("suppression exit"); Require(Crosses(station).All(c => !c.enabled),"all crosses disappear on exit");
        Enter("Gravity",LocalAnomalyType.Gravity); Tick(station,1);
        var suppression = controller.SpawnSiteZone(AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_ExplosiveZone.asset"),new Vector2(8,0),new Vector2(6,6)); fixtures.Add(suppression);
        Move(station,new Vector2(8,0)); Require(station.OrbitModulesSuppressed && mounts.All(p => p.Key.LocalPhase == p.Value),"direct zone switch replaces interference with suppression");
        Require(station.Interaction.OpenConstructor(),"Tab uses the existing constructor"); Render(station);
        Require(station.ActiveOrbitAnomaly == null && Crosses(station).All(c => !c.enabled),"Tab immediately restores modules and clears crosses");
        station.Interaction.CloseConstructor(); Exit("constructor exit");
        var removedZone = controller.SpawnSiteZone(AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Gravity.asset"),neutral,new Vector2(2,2));
        Require(removedZone != null,"temporary source-removal fixture fits inside MVP"); Move(station,neutral); Tick(station,1);
        Require(station.ActiveOrbitAnomaly == LocalAnomalyType.Gravity,"source-removal fixture has active distortion");
        Object.DestroyImmediate(removedZone.gameObject); Tick(station);
        Require(station.ActiveOrbitAnomaly == null && mounts.All(p => p.Key.LocalPhase == p.Value),"destroyed Unity zone source cannot leave an orphaned projection");
        Enter("Gravity",LocalAnomalyType.Gravity); Tick(station,1); station.Teardown();
        Require(station.ActiveOrbitAnomaly == null && mounts.All(p => p.Key.LocalPhase == p.Value),"teardown restores geometry and clears effect ownership");
        Require(station.RebuildRuntimeFromState() && station.ActiveOrbitAnomaly == null && !station.OrbitModulesSuppressed,"same-run station restore has no stale anomaly");
        station.enabled = false; Move(station,neutral); Require(Model(station.State) == model,"station restore retains the saved loadout");
        station.enabled = true; Move(station,Vector2.zero); Tick(station,1);
        Require(station.ActiveOrbitAnomaly == LocalAnomalyType.Gravity,"death fixture starts with active interference");
        player.DebugInvulnerable = false; player.SetIncomingDamageMultiplier(1f); player.TakeDamage(player.MaxHealth + 1,player.transform.position);
        yield return .04f;
        Require(player.IsDead && station.ActiveOrbitAnomaly == null && !station.OrbitModulesSuppressed,"actual death clears temporary effects");
        BeginRun(); var restart = SceneManager.LoadSceneAsync(Mvp);
        while (!restart.isDone) yield return .02f;
        while (Object.FindFirstObjectByType<OrbitalStationRuntime>() is not { IsInitialized:true } || SceneTransitionOverlay.IsTransitioning) yield return .02f;
        station = Object.FindFirstObjectByType<OrbitalStationRuntime>(); Move(station,Neutral(LevelAnomalyController.Instance));
        Require(station.UsesSegmentedOrbit && station.ActiveOrbitAnomaly == null && !station.OrbitModulesSuppressed && station.State.Modules.Count == 1,"repeat run starts from the ordinary pistol with no stale distortion/modules");
    }
}
#endif
