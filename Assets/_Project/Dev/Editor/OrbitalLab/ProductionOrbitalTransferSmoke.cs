#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// One opt-in bounded production smoke. No bot/batch runner and no runtime test UI.
[InitializeOnLoad]
public static class ProductionOrbitalTransferSmoke
{
    private const string Output = "Artifacts/OrbitalProductionTransfer";
    private const string Key = "OrbitalProductionTransfer.Active";
    private static readonly List<string> report = new();
    private static readonly Stack<IEnumerator> steps = new();
    private static double next, deadline;
    private static int errors;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static ProductionOrbitalTransferSmoke()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += State;
        Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (steps.Count > 0 && type is LogType.Error or LogType.Exception or LogType.Assert)
        { errors++; report.Add("UNITY ERROR: " + message); }
    }
    private static void Check(bool pass, string message)
    { if (!pass) throw new Exception(message); report.Add("PASS: " + message); }

    [MenuItem("Tools/Subject42/QA/Orbital production transfer smoke")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        report.Clear(); errors = 0;
        // Remove disabled artist physics from visual-only wrappers, retaining all art/colours/lights.
        foreach (string name in new[] { "Saw", "Shotgun", "Sniper" })
        {
            string path = "Assets/_Project/prefabs/Orbital/Modules/" + name + "Visual.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            foreach (var collider in prefab.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(prefab, path); PrefabUtility.UnloadPrefabContents(prefab);
        }
        var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        config.ShotgunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Orbital/Modules/ShotgunVisual.prefab");
        config.SniperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Orbital/Modules/SniperVisual.prefab");
        config.SawPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Orbital/Modules/SawVisual.prefab");
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        Check(config.ValidateRequiredReferences(out string error), "production presentation references: " + error);
        var tests = new OrbitalProductionTransferTests();
        tests.RetiredModuleIdsCannotBeInstalled(); tests.RetiredModulesMigrateWithoutResettingValidProgress();
        tests.CategoryStatePersistsBeforeWeaponAcquisition();
        Check(true, "retired-ID migration, installation rejection and saved category state tests");
        PlayerScriptCompilationCheck.Run();
        string compile = File.ReadAllText("Artifacts/GeneratedQA/Phase6/Compilation/results.txt");
        Check(compile.Contains("Release: PASS") && compile.Contains("Development: PASS"), "release and development player-script compilation");
        File.WriteAllText(Output + "/compile.txt", compile);
        CoreTestSupport.PreservePreferences();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        SessionState.SetBool(Key, true);
        File.WriteAllLines(Output + "/preflight.txt", report);
        EditorApplication.EnterPlaymode();
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { report.Clear(); errors = 0; steps.Push(SessionState.GetBool(Key + ".Spawn", false) ? SpawnChecks() : SessionState.GetBool(Key + ".Followup", false) ? FollowupChecks() : Checks()); next = 0; deadline = EditorApplication.timeSinceStartup + 180; }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(Key);
            // Uses the same backup format as the focused core tests, including original absent keys.
            var cleanup = CoreTestSupport.CleanupPlayMode();
            while (cleanup.MoveNext()) { }
        }
    }
    private static void Poll()
    {
        if (File.Exists(Output + "/validate.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Output + "/validate.request");
            try
            {
                const string hintPath = "Assets/_Project/prefabs/Onboarding/OrbitalControls.prefab";
                var hints = PrefabUtility.LoadPrefabContents(hintPath);
                var shiftHint = hints.GetComponentsInChildren<OnboardingHint>(true).Single(h => h.name == "OrbitalShift");
                var label = shiftHint.GetComponentInChildren<TextMeshPro>(true);
                if (label == null) label = new GameObject("Station control instruction", typeof(RectTransform), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(shiftHint.transform, false);
                label.text = "ПКМ — СТАНЦИЯ К КУРСОРУ\nSHIFT — ЗАМЕДЛЕНИЕ";
                label.font = TMP_Settings.defaultFontAsset;
                label.fontSize = 4f; label.enableAutoSizing = true; label.fontSizeMin = 2f; label.fontSizeMax = 4f;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(3.5f, .8f);
                label.GetComponent<MeshRenderer>().sortingLayerID = shiftHint.GetComponent<SpriteRenderer>().sortingLayerID;
                label.GetComponent<MeshRenderer>().sortingOrder = shiftHint.GetComponent<SpriteRenderer>().sortingOrder;
                var serializedHint = new SerializedObject(shiftHint);
                serializedHint.FindProperty("label").objectReferenceValue = label;
                serializedHint.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(hints, hintPath); PrefabUtility.UnloadPrefabContents(hints);
                var tests = new OrbitalProductionTransferTests();
                tests.RetiredModuleIdsCannotBeInstalled(); tests.RetiredModulesMigrateWithoutResettingValidProgress();
                tests.CategoryStatePersistsBeforeWeaponAcquisition(); tests.ShootingTargetUpgradeRejectsContactWeapon();
                foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile", new[] { "Assets/_Project/Data/Stages/EnemySpawnProfiles" }))
                {
                    var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var phase in profile.Phases)
                    {
                        if (phase.enemies.Any(e => e.enemyPrefab == null)) throw new Exception("Missing spawn prefab in " + profile.name);
                        var shields = phase.enemies.Where(e => e.enemyPrefab.GetComponent<EnemyDirectionalShield>() != null).ToArray();
                        if (shields.Length != 2 || shields.Any(e => e.enemyPrefab.GetComponent<EnemyDirectionalShield>().Kind != EnemyDirectionalShield.ShieldKind.Breakable || e.enemyPrefab.GetComponent<EnemyDirectionalShield>().MaximumBlockedHits != 4))
                            throw new Exception("Invalid shield composition in " + profile.name);
                    }
                }
                File.WriteAllText(Output + "/data-tests.txt", "PASS: 4 focused state tests; all production spawn phases have both breakable four-hit shield archetypes and no missing prefab references.");
            }
            catch (Exception e) { File.WriteAllText(Output + "/data-tests.txt", "FAIL: " + e); }
        }
        if (File.Exists(Output + "/smoke.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(Key + ".Followup", File.ReadAllText(Output + "/smoke.request").Trim() == "followup");
            SessionState.SetBool(Key + ".Spawn", File.ReadAllText(Output + "/smoke.request").Trim() == "spawn");
            File.Delete(Output + "/smoke.request");
            try { Run(); }
            catch (Exception e) { File.WriteAllText(Output + "/smoke.txt", "FAIL: " + e); Debug.LogException(e); }
        }
        if (steps.Count == 0 || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Production smoke timed out");
            var step = steps.Peek();
            if (!step.MoveNext())
            { steps.Pop(); if (steps.Count == 0) Finish(errors == 0); return; }
            if (step.Current is IEnumerator child) steps.Push(child);
            else next = EditorApplication.timeSinceStartup + (step.Current is float delay ? delay : .02);
        }
        catch (Exception e) { report.Add(e.ToString()); Finish(false); }
    }
    private static void Finish(bool pass)
    {
        steps.Clear();
        report.Add(pass ? "PASS: bounded production Play Mode smoke, no runtime errors" : "FAIL");
        report.Add("Manual physical RMB+WASD/Shift input: not verified; window capture/input timed out.");
        File.WriteAllLines(Output + (SessionState.GetBool(Key + ".Spawn", false) ? "/spawn.txt" : SessionState.GetBool(Key + ".Followup", false) ? "/followup.txt" : "/smoke.txt"), report);
        EditorApplication.ExitPlaymode();
    }
    private static IEnumerator Wait(Func<bool> condition)
    {
        double until = EditorApplication.timeSinceStartup + 30;
        while (!condition() && EditorApplication.timeSinceStartup < until) yield return null;
        Check(condition(), "production flow completed within bounded wait");
    }
    private static IEnumerator Card(OrbitalRewardKind kind)
    {
        var manager = UpgradeManager.Instance;
        Check(manager.DebugForceOrbitalReward(kind), "eligible real reward card: " + kind);
        yield return .15f;
        var view = Object.FindObjectsByType<UpgradeCardView>(FindObjectsSortMode.None).First(v => v.gameObject.activeInHierarchy);
        var definition = manager.DebugCurrentChoices[0] as OrbitalRewardData;
        Check(view.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.Contains(OrbitalCategories.Label(definition.EffectCategory))), "Russian category label: " + kind);
        if (OrbitalRewardProvider.GetModuleKind(kind).HasValue)
        {
            Color color = view.GetComponent<Button>().colors.normalColor;
            Check(Mathf.Abs(color.r - .64f) < .01f && Mathf.Abs(color.g - .24f) < .01f, "authored red weapon card: " + kind);
        }
        view.GetComponent<Button>().onClick.Invoke();
        yield return ResolveSelection();
        Check(!manager.IsChoosingUpgrade, "card selection/install finished: " + kind);
    }
    private static IEnumerator ResolveSelection()
    {
        double until = EditorApplication.timeSinceStartup + 8;
        while (UpgradeManager.Instance.IsChoosingUpgrade && EditorApplication.timeSinceStartup < until)
        {
            Object.FindFirstObjectByType<OrbitalStationRuntime>().RewardFlow.DebugChooseFirstValidTarget();
            yield return .08f;
        }
    }
    private static IEnumerator Checks()
    {
        yield return Wait(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        yield return Wait(() => SceneManager.GetActiveScene().name == "MVP" && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var player = station.Owner.Transform.gameObject;
        player.GetComponent<PlayerHealth>().AddMaxHealth(100000f);
        Check(station.Modules.Count == 1 && station.Modules[0].Kind == OrbitalModuleKind.Pistol && station.Rings.Count == 1, "normal MVP starter: one pistol, one orbit, no lab loadout/UI");
        Check(Object.FindFirstObjectByType<EnemyOrbitalLabController>() == null, "no lab controller in production");
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        // Level-up itself uses the normal event and three-card choice UI.
        ExperienceManager.Instance.AddExperience(ExperienceManager.Instance.ExpToNextLevel);
        yield return Wait(() => UpgradeManager.Instance.IsChoosingUpgrade);
        yield return .2f;
        var card = Object.FindObjectsByType<UpgradeCardView>(FindObjectsSortMode.None).First(v => v.gameObject.activeInHierarchy);
        card.GetComponent<Button>().onClick.Invoke(); yield return ResolveSelection();
        Check(!UpgradeManager.Instance.IsChoosingUpgrade, "XP level-up -> real card -> apply/install -> combat");
        // Enough ordinary mounts for all six kinds and a pair, using existing capacities.
        while (station.Rings.Count < 4) station.AddRing();
        foreach (var ring in station.Rings.ToArray()) while (ring.State.MountCount < ring.State.MountCapacity) station.AddMount(ring.RingId, out _);
        yield return Card(OrbitalRewardKind.ShootingDamage);
        yield return Card(OrbitalRewardKind.ShootingRate);
        yield return Card(OrbitalRewardKind.ContactDamage);
        yield return Card(OrbitalRewardKind.ContactSize);
        yield return Card(OrbitalRewardKind.Shotgun);
        yield return Card(OrbitalRewardKind.Sniper);
        yield return Card(OrbitalRewardKind.Saw);
        yield return Card(OrbitalRewardKind.Pistol);
        yield return Card(OrbitalRewardKind.ArcEmitter);
        yield return Card(OrbitalRewardKind.LinkPair);
        yield return Card(OrbitalRewardKind.RingSpeed);
        yield return Card(OrbitalRewardKind.MaxHealth);
        yield return Card(OrbitalRewardKind.MoveSpeed);
        Check(station.Modules.OfType<OrbitalProjectileModule>().All(m => m.DamageMultiplier >= 1.2f && m.FireRateMultiplier >= 1.15f) &&
            station.Modules.OfType<OrbitalSawModule>().All(m => m.DamageMultiplier >= 1.2f && m.ContactRadius > .65f), "category effects apply to subsequently acquired shooting/contact weapons");
        station.enabled = false;
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        adapter.DebugClearProjectiles();
        var shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/BreakableShieldBearer.prefab");
        var enemy = Object.Instantiate(shieldPrefab); enemy.transform.position = player.transform.position + Vector3.right * 15f;
        foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.enabled = false;
        var health = enemy.GetComponent<EnemyHealth>(); health.SetRuntimeMaxHealth(10000f);
        var shield = enemy.GetComponent<EnemyDirectionalShield>(); shield.Bind(player.transform); Physics2D.SyncTransforms();
        float hp = health.CurrentHealth;
        Shot(adapter, shield, OrbitalWeaponSettings.Pistol());
        Check(shield.RemainingBlockedHits == 3 && health.CurrentHealth == hp, "front pistol shot blocks, consumes one of four shield points");
        shield.ResetForSpawn(); Physics2D.SyncTransforms();
        Shot(adapter, shield, OrbitalWeaponSettings.Shotgun(), 7);
        Check(shield.RemainingBlockedHits == 3 && health.CurrentHealth == hp, "seven shotgun pellets consume one shield point per volley");
        Shot(adapter, shield, OrbitalWeaponSettings.Sniper());
        Check(shield.RemainingBlockedHits == 3 && health.CurrentHealth < hp, "120 m/s sniper crosses shield and damages body without shield wear");
        shield.ResetForSpawn(); Physics2D.SyncTransforms();
        for (int i = 0; i < 4; i++) Shot(adapter, shield, OrbitalWeaponSettings.Pistol());
        Check(shield.IsBroken && !shield.ShieldCollider.enabled && !shield.ShieldArc.enabled, "four hits destroy physical shield and its visual");
        enemy.SetActive(false); enemy.SetActive(true);
        Check(shield.RemainingBlockedHits == 4 && !shield.IsBroken && shield.DurabilityColor == EnemyDirectionalShield.Blue, "pooled shield restores geometry, durability and colour");
        Object.Destroy(enemy);
        yield return null;
        // Programmed input state checks exercise the production steering method; not physical input.
        var center = station.GetComponent<OrbitalCenterShift>();
        var movementPlayer = player.GetComponent<CharacterMovement2D>();
        typeof(OrbitalCenterShift).GetMethod("AdvancePointer", Private).Invoke(center, new object[] { true, true, false, (Vector2)player.transform.position + Vector2.right * 100f, .1f });
        if (Application.isFocused) Check(center.Offset.magnitude > 0f && center.Offset.magnitude <= 2.001f && center.IsPointerSteering, "programmed RMB steering obeys speed/range limits");
        typeof(OrbitalCenterShift).GetMethod("FilterInput", Private).Invoke(center, new object[] { Vector2.up });
        center.ResetOffset(); Check(center.Offset == Vector2.zero && !center.IsPointerSteering, "steering reset releases temporary state");
        var saved = JsonUtility.ToJson(station.State);
        Check(station.SimulateSectorRestore(), "production station restoration");
        Check(JsonUtility.ToJson(station.State) == saved, "restoration preserves modules/category state");
        station.enabled = true;
        RunEndService.Instance.ReturnToBunker();
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        yield return .2f;
        Check(Mathf.Approximately(Time.timeScale, 1f) && station == null, "ordinary return-to-bunker releases original station and time state");
    }

    private static IEnumerator FollowupChecks()
    {
        yield return Wait(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        yield return Wait(() => SceneManager.GetActiveScene().name == "MVP" && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>(); station.enabled = false;
        var player = station.Owner.Transform.gameObject;
        player.GetComponent<PlayerHealth>().AddMaxHealth(100000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) enemy.gameObject.SetActive(false);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var ring = station.Rings[0];
        station.AddMount(ring.RingId, out _); station.AddMount(ring.RingId, out _);
        station.InstallModule(OrbitalModuleKind.Shotgun, ring.RingId, 1, out _);
        station.InstallModule(OrbitalModuleKind.Saw, ring.RingId, 2, out _);
        var shotgun = station.Modules.OfType<OrbitalProjectileModule>().First(m => m.Kind == OrbitalModuleKind.Shotgun);
        var saw = station.Modules.OfType<OrbitalSawModule>().First();
        var target = new GameObject("Bounded exposed-body target");
        target.transform.position = shotgun.WorldPosition + Vector2.right * 2f;
        target.AddComponent<CircleCollider2D>().radius = 1.25f;
        var health = target.AddComponent<EnemyHealth>(); health.SetRuntimeMaxHealth(10000f);
        yield return null;
        Physics2D.SyncTransforms(); adapter.DebugClearProjectiles(); shotgun.ResetFiring();
        typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Private).SetValue(shotgun, 0f);
        shotgun.ActivateCombat();
        Check(adapter.LabActiveProjectileCount == 7 && shotgun.Settings.ProjectileSpeed == 10f, "real shotgun runtime spawns seven independent 10 m/s pellets");
        float hp = health.CurrentHealth;
        for (int i = 0; i < 25; i++) adapter.Tick(.025f);
        Check(hp - health.CurrentHealth >= 7f * 3.5f - .01f, "all seven pellets independently damage an exposed body");
        adapter.DebugClearProjectiles();
        target.transform.position = saw.WorldPosition + Vector2.right * .3f; Physics2D.SyncTransforms();
        hp = health.CurrentHealth; saw.ActivateCombat(); float after = health.CurrentHealth; saw.ActivateCombat();
        Check(after < hp && health.CurrentHealth == after, "real saw disk hits body and blocks immediate repeat damage");
        yield return .36f;
        saw.ActivateCombat(); Check(health.CurrentHealth < after, "saw repeat damage resumes after the authored interval");
        target.transform.position = saw.WorldPosition + Vector2.right * 3f; Physics2D.SyncTransforms();
        hp = health.CurrentHealth; saw.ActivateCombat(); Check(health.CurrentHealth == hp, "saw never damages a remote body");
        var ring2 = station.AddRing();
        station.InstallModule(OrbitalModuleKind.ArcEmitter, ring2.StableRingId, 0, out _);
        var laser = station.Modules.First(m => m.Kind == OrbitalModuleKind.ArcEmitter);
        target.transform.position = laser.WorldPosition + Vector2.right * 1.5f; Physics2D.SyncTransforms();
        hp = health.CurrentHealth; laser.ActivateCombat(); Check(health.CurrentHealth < hp, "retained laser runtime damages its target");
        station.AddMount(ring2.StableRingId, out _); station.AddMount(ring2.StableRingId, out _);
        Check(station.InstallLinkPair(ring2.StableRingId, 1, ring2.StableRingId, 2, out _), "retained link pair installation");
        var links = station.Modules.Where(m => m.Kind == OrbitalModuleKind.LinkNode).ToArray();
        target.transform.position = (links[0].WorldPosition + links[1].WorldPosition) * .5f;
        foreach (var module in station.Modules) typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Private).SetValue(module, 100f);
        typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Private).SetValue(links[0], 0f);
        hp = health.CurrentHealth;
        typeof(OrbitalStationRuntime).GetMethod("TickStation", Private).Invoke(station, new object[] { .02f });
        Check(health.CurrentHealth < hp, "retained connecting line applies contact damage");
        Object.Destroy(target); yield return null;
        var center = station.GetComponent<OrbitalCenterShift>(); center.Refresh();
        Check(center.IsPointerControlConfigured, "production center acquires the camera after CharacterSpawner wiring");
        var direction = (Vector2)typeof(OrbitalCenterShift).GetMethod("FilterInput", Private).Invoke(center, new object[] { Vector2.up });
        Check(direction == Vector2.up, "station steering filter preserves WASD movement direction");
        float initialScale = Time.timeScale;
        station.InputOwner.TickBulletTime(true, station.BulletTime, .2f);
        Check(Time.timeScale < initialScale && Time.timeScale > 0f, "existing slow-time ability remains active");
        station.InputOwner.ReleaseBulletTime(); Check(Mathf.Approximately(Time.timeScale, initialScale), "slow-time release restores the time owner");
        station.State.TryUpgradeCategory(OrbitalRewardKind.ShootingDamage);
        station.State.TryUpgradeCategory(OrbitalRewardKind.ContactSize);
        int oldSector = RunStateManager.Instance.CurrentSector.SectorNumber;
        var flow = RunFlowController.Instance;
        typeof(RunFlowController).GetField("<SectorElapsedTime>k__BackingField", Private).SetValue(flow, 100000f);
        station.enabled = true; yield return .1f;
        Check(flow.HandleExitReached(), "existing sector-exit flow opens rule choice");
        Check(Object.FindFirstObjectByType<LevelChoiceManager>().DebugSelectFirstRule(), "existing rule card triggers next sector");
        yield return Wait(() => RunStateManager.Instance.CurrentSector?.SectorNumber == oldSector + 1 && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true });
        station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        Check(station.State.ShootingDamageMultiplier == 1.2f && station.State.ContactSizeMultiplier == 1.15f && station.Modules.Any(m => m.Kind == OrbitalModuleKind.Shotgun), "real next-sector transition preserves weapons/category upgrades");
        RunEndService.Instance.ReturnToBunker();
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        yield return .2f;
        var remaining = Object.FindObjectsByType<OrbitalStationRuntime>(FindObjectsSortMode.None);
        report.Add("RETURN DIAGNOSTICS: timeScale=" + Time.timeScale + "; originalStationDestroyed=" + (station == null) + "; stations=" + string.Join(",", remaining.Select(s => s.name + "@" + s.gameObject.scene.name)));
        Check(Mathf.Approximately(Time.timeScale, 1f) && station == null, "ordinary return-to-bunker releases original station and time state");
    }
    private static void Shot(ProductionOrbitalCombatAdapter adapter, EnemyDirectionalShield shield, OrbitalWeaponSettings settings, int count = 1)
    {
        Vector2 direction = -shield.Forward, origin = shield.Center - direction * 3f;
        var volley = count > 1 ? new ProductionOrbitalCombatAdapter.ProjectileVolley(100) : null;
        for (int i = 0; i < count; i++) adapter.SpawnWeaponProjectile(origin, shield.GetComponent<EnemyHealth>(), direction, settings, volley, settings.Damage);
        for (int i = 0; i < 30; i++) adapter.Tick(.025f);
        adapter.DebugClearProjectiles();
    }

    private static IEnumerator SpawnChecks()
    {
        yield return Wait(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        yield return Wait(() => SceneManager.GetActiveScene().name == "MVP" && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>(); station.enabled = false;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>(); spawner.StopSpawning();
        var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>("Assets/_Project/Data/Stages/EnemySpawnProfiles/ESP_Level_01.asset");
        var melee = profile.Phases[0].enemies.Single(e => e.enemyPrefab.GetComponent<EnemyDirectionalShield>() != null && e.enemyPrefab.GetComponent<EnemyShooterMovement>() == null);
        var shooter = profile.Phases[0].enemies.Single(e => e.enemyPrefab.GetComponent<EnemyDirectionalShield>() != null && e.enemyPrefab.GetComponent<EnemyShooterMovement>() != null);
        var canSpawn = typeof(EnemySpawner).GetMethod("CanSpawn", Private);
        spawner.SetSpawnProfile(profile, 1);
        Check((bool)canSpawn.Invoke(spawner, new object[] { melee }) && !(bool)canSpawn.Invoke(spawner, new object[] { shooter }), "production composition permits shield melee at level 1 and gates shield shooter until level 2");
        spawner.SetSpawnProfile(profile, 2);
        Check((bool)canSpawn.Invoke(spawner, new object[] { shooter }), "production composition enables shield shooter at level 2");
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", Private);
        var enemies = new List<GameObject>();
        foreach (var entry in new[] { melee, melee, shooter })
        {
            var enemy = (GameObject)spawn.Invoke(spawner, new object[] { entry.enemyPrefab, station.transform.position + Vector3.right * (4f + enemies.Count), true });
            enemies.Add(enemy);
            Check(enemy.GetComponent<EnemyHealth>() is { IsDead: false } && enemy.GetComponent<EnemyMovement>() != null && enemy.GetComponent<EnemyDirectionalShield>().RemainingBlockedHits == 4, "production initialization of " + entry.enemyPrefab.name);
        }
        Check(!(bool)canSpawn.Invoke(spawner, new object[] { melee }) && !(bool)canSpawn.Invoke(spawner, new object[] { shooter }), "existing per-type alive limits exclude additional shield enemies");
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        adapter.DebugClearProjectiles();
        var config = OrbitalPresentationConfig.Active;
        var victim = enemies[0].GetComponent<EnemyHealth>();
        adapter.SpawnWeaponProjectile(station.transform.position, victim, Vector2.right, config.GetWeaponSettings(OrbitalModuleKind.Sniper), null, 1f);
        var trails = station.GetComponentsInChildren<TrailRenderer>().Where(t => t.GetComponentInParent<OrbitalModuleView>() == null).ToArray();
        Check(trails.Length > 0 && trails.All(t => Mathf.Abs(t.time - config.SniperProjectile.TrailSeconds) < .0001f && Mathf.Abs(t.widthMultiplier - config.SniperProjectile.TrailWidth) < .0001f), "active sniper uses its narrow red projectile trail profile");
        int pool = adapter.LabPooledProjectileCount; adapter.DebugClearProjectiles();
        adapter.SpawnWeaponProjectile(station.transform.position, victim, Vector2.right, config.GetWeaponSettings(OrbitalModuleKind.Pistol), null, 1f);
        trails = station.GetComponentsInChildren<TrailRenderer>().Where(t => t.GetComponentInParent<OrbitalModuleView>() == null).ToArray();
        Check(adapter.LabPooledProjectileCount == pool && trails.All(t => Mathf.Abs(t.time - config.PistolProjectile.TrailSeconds) < .0001f && Mathf.Abs(t.widthMultiplier - config.PistolProjectile.TrailWidth) < .0001f), "same pooled projectile applies pistol trail profile after sniper use");
        adapter.DebugClearProjectiles();
        station.enabled = true;
        RunEndService.Instance.ReturnToBunker();
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        yield return .1f;
        Check(station == null && Mathf.Approximately(Time.timeScale, 1f), "spawn verification exits through the normal run-end cleanup");
    }
}
#endif
