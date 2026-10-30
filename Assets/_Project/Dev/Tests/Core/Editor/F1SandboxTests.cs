#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class F1SandboxTests
{
    private const string Output = "Artifacts/GeneratedQA/F1Sandbox-20261030";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(Subject42DebugMenu menu, string method, params object[] args) =>
        typeof(Subject42DebugMenu).GetMethod(method, Hidden).Invoke(menu, args);
    private static GameObject Root(Subject42DebugMenu menu, string field) =>
        (GameObject)typeof(Subject42DebugMenu).GetField(field, Hidden).GetValue(menu);

    [UnityTest]
    public IEnumerator WeaponModesAndSegmentedFeedback()
    {
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.SetInt(LocalizationService.LanguagePreferenceKey, (int)GameLanguage.Russian);
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return WeaponModesAndSegmentedFeedbackScenario();
    }

    private static IEnumerator WeaponModesAndSegmentedFeedbackScenario()
    {
        const string output = "Artifacts/DebugMenuAudit/SegmentedWeaponSmoke-20261031";
        Directory.CreateDirectory(output);
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.Station is { IsInitialized: true } &&
            Object.FindFirstObjectByType<Subject42DebugMenu>() != null);
        yield return null;
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var station = Object.FindFirstObjectByType<CharacterSpawner>().Station;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var run = RunStateManager.Instance;
        spawner.StopSpawning();

        // A pre-restriction save stays valid and the same item survives the production bind.
        var oldState = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(station.State));
        int ringId = oldState.Rings[0].StableRingId;
        Assert.That(oldState.TryGrantSegmentedWeapon(OrbitalModuleKind.Saw), Is.True, "Unbound lab/old states remain permissive.");
        Assert.That(oldState.TryInstallSegmentedWeapon(OrbitalModuleKind.Saw, ringId, 6, out var oldSaw, out var error), Is.True, error);
        oldSaw.DamageLevel = 2;
        int sawId = oldSaw.StableModuleId;
        Assert.That(run.DebugReplaceOrbitalState(oldState), Is.True);
        station.RebuildRuntimeFromState();
        var state = station.State;
        Assert.That(state.Modules.Any(gun => gun.ModuleType == OrbitalModuleKind.Saw), Is.False);
        Assert.That(state.SegmentedWeapons.Single(gun => gun.StableModuleId == sawId).DamageLevel, Is.EqualTo(2));
        Assert.That(state.IsWeaponAvailable(OrbitalModuleKind.Saw), Is.False);
        Assert.That(state.TryInstallSegmentedWeapon(OrbitalModuleKind.Saw, ringId, 6, out _, out _), Is.False);
        Assert.That(state.CanTransferSegmentedWeapon(sawId, true, ringId, 6, out _), Is.False);
        Assert.That(state.CanInstallModule(OrbitalModuleKind.Saw, ringId, 6, out _), Is.False);
        station.RebuildRuntimeFromState();
        Assert.That(state.SegmentedWeapons.Count(gun => gun.StableModuleId == sawId), Is.EqualTo(1), "Restore must be idempotent.");
        using (var provider = new OrbitalRewardProvider(Array.Empty<UpgradeData>()))
            Assert.That(provider.GetEligibleNormalRewards(state, run.ItemSlots).Cast<OrbitalRewardData>()
                .Any(data => data.RewardKind == OrbitalRewardKind.Saw), Is.False);
        var manager = UpgradeManager.Instance;
        Assert.That(manager.DebugRewardCatalogue.Any(data => data.RewardKind == OrbitalRewardKind.Saw), Is.False);
        Assert.That(manager.DebugOfferCatalogueReward(OrbitalRewardKind.Saw, true), Is.False);
        File.WriteAllText(output + "/progress.txt", "PASS: Saw compatibility, production pool/catalogue/grant/install/transfer guards.\n");

        OpenSandbox(menu);
        Assert.That(Root(menu, "menuRoot").GetComponentsInChildren<TMP_Text>()
            .Count(text => text.text.StartsWith("Вариант:", StringComparison.Ordinal)), Is.EqualTo(2));
        Click(menu, "Снайперка +1");
        OpenSandbox(menu);
        Click(menu, "Дробовик +1");
        Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
        Assert.That(station.ConstructWeapon(OrbitalModuleKind.Sniper, ringId, 2, out error), Is.True, error);
        Assert.That(station.ConstructWeapon(OrbitalModuleKind.Shotgun, ringId, 4, out error), Is.True, error);
        Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
        OpenSandbox(menu);
        WeaponMode(menu, "Снайперка", "Заряженная линия").onClick.Invoke();
        Assert.That(state.ChargedLineSniperExperiment, Is.True);
        Call(menu, "CloseMenu");
        OpenSandbox(menu);
        Assert.That(WeaponMode(menu, "Снайперка", "Заряженная линия").interactable, Is.False);
        var sniper = station.Modules.OfType<OrbitalProjectileModule>().Single(module => module.Kind == OrbitalModuleKind.Sniper);
        var shotgun = station.Modules.OfType<OrbitalProjectileModule>().Single(module => module.Kind == OrbitalModuleKind.Shotgun);
        Assert.That(sniper.Settings.ChargeSeconds, Is.EqualTo(.45f));
        Assert.That(sniper.Settings.Damage, Is.EqualTo(24f));
        Assert.That(sniper.Settings.BodyPierces, Is.EqualTo(3));
        Assert.That(sniper.Settings.ProjectileSpeed, Is.EqualTo(120f));
        Assert.That(sniper.Settings.Range, Is.EqualTo(12f));
        Assert.That(shotgun.Settings.Pellets, Is.EqualTo(7));
        Assert.That(shotgun.Settings.BurstShots, Is.EqualTo(1));
        Assert.That(shotgun.Settings.Damage, Is.EqualTo(3.5f));
        Assert.That(shotgun.Settings.FireInterval, Is.EqualTo(1.05f));
        Assert.That(shotgun.Settings.SpreadDegrees, Is.EqualTo(42f));
        Vector3 mountPosition = sniper.CurrentMount.Transform.localPosition;
        var direction = ((Vector2)sniper.CurrentMount.Transform.position - (Vector2)station.transform.position).normalized;
        var targets = new EnemyHealth[3];
        for (int i = 0; i < targets.Length; i++)
        {
            var enemy = spawner.SpawnDebugEnemyAt(spawner.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Basic),
                sniper.WorldPosition + direction * (1.2f + i * 1.2f));
            foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            targets[i] = enemy.GetComponent<EnemyHealth>();
            targets[i].SetRuntimeMaxHealth(100f);
        }
        yield return null;
        int hits = 0, audio = 0;
        float shotResumeScale = 1f;
        station.Combat.Hit += (target, damage) =>
        {
            if (!targets.Contains(target)) return;
            Assert.That(damage, Is.EqualTo(24f));
            hits++;
            if (hits == 1) ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/impact.png"));
        };
        void Cue(AudioCueId id)
        {
            if (id != AudioCueId.ChargedSniperFire) return;
            audio++;
            // Hold the real emitted frame for reliable Game View capture; no production hook is added.
            shotResumeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        AudioService.Instance.DebugCuePlayed += Cue;
        try
        {
            station.ClearLabFlashes();
            Call(menu, "CloseMenu");
            bool mid = false, late = false, shotgunVolley = false;
            float midBrightness = 0f, lateBrightness = 0f;
            var charge = typeof(OrbitalProjectileModule).GetField("chargeRemaining", Hidden);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (sniper.ShotsFired == 0 && Time.realtimeSinceStartup < deadline)
            {
                float remaining = (float)charge.GetValue(sniper);
                float progress = remaining >= 0f ? 1f - remaining / .45f : 0f;
                var guide = sniper.CurrentMount.Transform.GetComponentsInChildren<LineRenderer>(true)
                    .FirstOrDefault(line => line.gameObject.name == "Charged sniper direction");
                if (!mid && progress >= .45f && guide != null && guide.enabled)
                {
                    mid = true; midBrightness = guide.startColor.r;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/charge.png"));
                }
                if (!late && progress >= .86f && guide != null && guide.enabled)
                {
                    late = true; lateBrightness = guide.startColor.r;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/charge-final.png"));
                }
                if (!shotgunVolley && shotgun.ShotsFired > 0)
                {
                    var shots = (IEnumerable)typeof(ProductionOrbitalCombatAdapter).GetField("projectiles", Hidden).GetValue(station.Combat);
                    int pellets = 0;
                    foreach (var shot in shots)
                        if ((bool)shot.GetType().GetField("Active").GetValue(shot) &&
                            (OrbitalModuleKind)shot.GetType().GetField("Kind").GetValue(shot) == OrbitalModuleKind.Shotgun) pellets++;
                    Assert.That(pellets, Is.EqualTo(7), "Ordinary shotgun must emit seven actual projectiles.");
                    shotgunVolley = true;
                }
                yield return null;
            }
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(mid && late && shotgunVolley, Is.True);
            Assert.That(lateBrightness, Is.GreaterThan(midBrightness));
            Assert.That(audio, Is.GreaterThan(0));
            Assert.That(sniper.CurrentMount.Transform.localPosition, Is.EqualTo(mountPosition), "Recoil must not move the mount.");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/shot.png"));
            yield return new WaitForSecondsRealtime(.15f);
            Time.timeScale = shotResumeScale;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(hits, Is.EqualTo(3));
            foreach (var target in targets) Assert.That(target.CurrentHealth, Is.EqualTo(76f));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/fade.png"));
            yield return new WaitForSecondsRealtime(.15f);
            OpenSandbox(menu);
            WeaponMode(menu, "Снайперка", "Обычная").onClick.Invoke();
            Assert.That(state.ChargedLineSniperExperiment, Is.False);
            Assert.That(sniper.CurrentMount.Transform.GetComponentsInChildren<LineRenderer>(true)
                .Where(line => line.gameObject.name.StartsWith("Charged sniper", StringComparison.Ordinal)).All(line => !line.enabled), Is.True);
            Click(menu, "X");
            File.WriteAllText(output + "/result.txt", "PASS: only2 F1 groups; sniper toggle/reopen/Normal; charge brightens in final0.1s; charged audio cue; actual three-body24-damage shot; mount unchanged.\n" +
                "PASS: ordinary shotgun seven actual pellets and unchanged profile; Saw absent from production normal pool, catalogue, grants and installations; old Saw ID/upgrade preserved in reserve; lab policy permissive.\n" +
                "One scripted Unity Editor Play Mode smoke; physical input and subjective feel not verified.\n" + Application.dataPath + "\nUnity=" + Application.unityVersion);
        }
        finally
        {
            Time.timeScale = shotResumeScale;
            if (AudioService.Instance != null) AudioService.Instance.DebugCuePlayed -= Cue;
        }
    }

    [UnityTest]
    public IEnumerator NavigateF1WithoutChangingRun()
    {
        const string output = "Artifacts/DebugMenuAudit/Stage2Smoke";
        Directory.CreateDirectory(output);
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.SetInt(LocalizationService.LanguagePreferenceKey, (int)GameLanguage.Russian);
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        yield return null;
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var run = RunStateManager.Instance;
        var state = Object.FindFirstObjectByType<CharacterSpawner>().Station.State;
        string build = JsonUtility.ToJson(state);
        bool development = run.IsDevelopmentRun;
        float resumeScale = Time.timeScale;
        bool cursorVisible = Cursor.visible;
        var cursorLock = Cursor.lockState;

        Call(menu, "ToggleHub"); // The same entry point as F1; no synthetic keyboard input.
        Assert.That(Root(menu, "menuRoot").activeSelf, Is.True);
        Assert.That(typeof(Subject42DebugMenu).GetField("activeTab", Hidden).GetValue(menu).ToString(), Is.EqualTo("Sandbox"));
        Assert.That(Root(menu, "hubRoot").activeSelf, Is.False);
        CheckListeners();
        yield return Shot("01-sandbox");

        Click(menu, "Билд и улучшения");
        Assert.That(Root(menu, "menuRoot").GetComponentsInChildren<TMP_Text>()
            .Any(t => t.text.Contains("Предметы поступают в запас")), Is.True);
        CheckListeners();
        yield return Shot("02-build");

        Click(menu, "Враги и бой");
        var captions = Root(menu, "menuRoot").GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
        Assert.That(captions.Count(t => t == "АВТОСПАВН"), Is.EqualTo(1));
        Assert.That(captions.Count(t => t == "РЕЖИМ AI"), Is.EqualTo(1));
        Assert.That(captions.Count(t => t == "УБРАТЬ РУЧНЫХ"), Is.EqualTo(1));
        Assert.That(captions.Contains("УБИТЬ ВСЕХ"), Is.False);
        Assert.That(captions.Contains("Очистить обычных врагов"), Is.True);
        CheckListeners();
        yield return Shot("03-enemies");

        Click(menu, "НАЗАД");
        Assert.That((int)typeof(Subject42DebugMenu).GetField("activeTab", Hidden).GetValue(menu), Is.EqualTo(0));
        Click(menu, "X");
        Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(resumeScale));
        Assert.That(Cursor.visible, Is.EqualTo(cursorVisible));
        Assert.That(Cursor.lockState, Is.EqualTo(cursorLock));
        Assert.That(run.IsDevelopmentRun, Is.EqualTo(development));
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(build), "Navigation must preserve the build and experiments.");
        var session = menu.GetComponent<TrailerCaptureSession>();
        Assert.That(session == null || !session.HasSettings, Is.True, "Navigation must not capture or apply run overrides.");

        // The same smoke checks return paths affected by sharing the page shells.
        Call(menu, "ToggleHub");
        Click(menu, "ПОИСК / ИЗБР.");
        var search = Root(menu, "hubRoot").GetComponentInChildren<TMP_InputField>();
        search.text = "Боты";
        var tools = Root(menu, "hubRoot").GetComponentsInChildren<Button>()
            .Single(b => b.transform.parent.name == "diagnostics.tools" &&
                b.GetComponentInChildren<TMP_Text>().text.Contains("Боты"));
        tools.onClick.Invoke();
        Click(menu, "НАЗАД");
        Assert.That(Root(menu, "hubRoot").activeSelf, Is.True, "Back from a search result must return to search.");
        Assert.That(search.text, Is.EqualTo("Боты"));
        Root(menu, "hubRoot").GetComponentsInChildren<Button>()
            .Single(b => b.GetComponentInChildren<TMP_Text>().text == "← Назад").onClick.Invoke();
        Call(menu, "ToggleMvpOrbitSurface");
        var surfaceBack = Root(menu, "visualBackButton").GetComponent<RectTransform>();
        Assert.That(surfaceBack.anchoredPosition.x, Is.EqualTo(-48f));
        Call(menu, "CloseMenu");
        Call(menu, "ToggleHub");
        Assert.That(Root(menu, "menuRoot").activeSelf, Is.True);
        var footer = (RectTransform)typeof(Subject42DebugMenu).GetField("mvpOrbitSaveFooter", Hidden).GetValue(menu);
        Assert.That(footer.gameObject.activeSelf, Is.False, "F9 footer must not leak into Sandbox.");
        Click(menu, "X");
        Assert.That(run.IsDevelopmentRun, Is.EqualTo(development));
        File.WriteAllText(output + "/result.txt", "PASS: F1 entry → Sandbox → Build → Enemies → Back to Build → Close.\n" +
            "Buttons retain runtime listeners; one Auto Spawn / Freeze AI / Clear Manual; separate ordinary cleanup.\n" +
            "Build, Development flag, capture ownership, timeScale and cursor preserved; search return and F9 layout checked.\n" +
            "One scripted Unity Editor Play Mode smoke; physical keyboard/mouse input and combat actions were not executed.\n" +
            Application.dataPath + "\nUnity=" + Application.unityVersion);

        void CheckListeners()
        {
            var prepare = typeof(UnityEngine.Events.UnityEventBase).GetMethod("PrepareInvoke", Hidden);
            foreach (var button in Root(menu, "menuRoot").GetComponentsInChildren<Button>().Where(b => b.interactable))
                Assert.That(((ICollection)prepare.Invoke(button.onClick, null)).Count, Is.GreaterThan(0),
                    "Missing handler: " + button.name);
        }
        IEnumerator Shot(string name)
        {
            Canvas.ForceUpdateCanvases();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/" + name + ".png"));
            yield return new WaitForSecondsRealtime(.15f);
        }
    }

    [Test]
    public void SandboxEntryIsDisabledOutsideProductionMvp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var owner = new GameObject("Test menu");
        var menu = owner.AddComponent<Subject42DebugMenu>();
        try
        {
            Call(menu, "BuildHub");
            var entry = Root(menu, "hubRoot").GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>().text == "ПЕСОЧНИЦА");
            Assert.That(entry, Is.Not.Null, "F1 must expose the sandbox as a main section.");
            Assert.That(entry.interactable, Is.False, "Production sandbox must not mutate a laboratory or bunker.");
        }
        finally
        {
            Object.DestroyImmediate(Root(menu, "hubRoot"));
            Object.DestroyImmediate(owner);
        }
    }

    [UnityTest]
    public IEnumerator PrepareGrantConstructAndStartProductionCombat()
    {
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.SetInt(LocalizationService.LanguagePreferenceKey, (int)GameLanguage.Russian);
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return SandboxScenario();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
    }

    private static IEnumerator SandboxScenario()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/scenario.txt", "RUNNING: production sandbox scenario");
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        yield return new WaitForSecondsRealtime(.5f);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var station = Object.FindFirstObjectByType<CharacterSpawner>().Station;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var health = PlayerRuntimeReference.PlayerHealth;
        var state = station.State;
        File.WriteAllText(Output + "/editor-identity.txt", Application.dataPath + "\n" +
            SceneManager.GetActiveScene().path + "\nUnity=" + Application.unityVersion +
            " PID=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        int weapons = state.SegmentedWeapons.Count, links = state.SegmentedLinkStock.Count, installed = state.Modules.Count;
        OpenSandbox(menu);
        Click(menu, "ПОДГОТОВИТЬ ТЕСТ");
        Assert.That(state.UnlockedSegmentedLevels, Is.EqualTo(3));
        Assert.That(state.SegmentedWeaponLimit, Is.EqualTo(12));
        Assert.That(health.DebugInvulnerable && !spawner.IsSpawningEnabled, Is.True);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        Click(menu, "ПОДГОТОВИТЬ ТЕСТ");
        Assert.That(state.SegmentedWeapons.Count, Is.EqualTo(weapons));
        Assert.That(state.SegmentedLinkStock.Count, Is.EqualTo(links));
        Assert.That(state.Modules.Count, Is.EqualTo(installed), "Preparation must not grant or install weapons.");
        Assert.That(state.UnlockedSegmentedLevels == 3 && state.SegmentedWeaponLimit == 12, Is.True);
        Click(menu, "×1,5");
        Assert.That(spawner.DebugCaptureDensity, Is.EqualTo(1.5f));
        yield return Capture("prepared");
        var scroll = Root(menu, "menuRoot").GetComponentsInChildren<ScrollRect>()
            .Single(s => s.gameObject.activeInHierarchy);
        scroll.verticalNormalizedPosition = 0f;
        yield return Capture("experiments");
        WeaponMode(menu, "Снайперка", "Заряженная линия").onClick.Invoke();
        Assert.That(state.ChargedLineSniperExperiment, Is.True);
        WeaponMode(menu, "Снайперка", "Обычная").onClick.Invoke();
        Assert.That(state.ChargedLineSniperExperiment, Is.False);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);

        Click(menu, "Пистолет +1");
        yield return null;
        Assert.That(state.SegmentedWeapons.Count, Is.EqualTo(weapons + 1));
        Assert.That(state.Modules.Count, Is.EqualTo(installed), "Grant goes to reserve, not a hidden auto-install.");
        Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True);
        OpenSandbox(menu);
        Click(menu, "ОТКРЫТЬ TAB");
        Assert.That(station.InputOwner.IsConstructing, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Root(menu, "menuRoot").activeSelf, Is.False);
        yield return Capture("constructor");
        Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
        Assert.That(Time.timeScale, Is.GreaterThan(0));
        OpenSandbox(menu);
        Click(menu, "НАЧАТЬ БОЙ");
        Assert.That(spawner.IsSpawningEnabled, Is.True);
        Assert.That(spawner.DebugCaptureDensity, Is.EqualTo(1.5f));
        Assert.That(Time.timeScale, Is.GreaterThan(0));
        Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
        yield return CoreTestSupport.Await(() => spawner.DebugCaptureAliveCount > 0);
        yield return new WaitForSecondsRealtime(.5f);
        yield return Capture("combat");
        OpenSandbox(menu);
        Click(menu, "ОЧИСТИТЬ ОБЫЧНЫХ");
        Assert.That(EnemyHealth.ActiveInstances.All(e => e == null || e.IsBoss || !e.gameObject.activeSelf), Is.True);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        Click(menu, "ЗАМОРОЗИТЬ AI");
        Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.True);
        Call(menu, "CloseMenu");
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" &&
            !SceneTransitionOverlay.IsTransitioning);
        Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False,
            "Sandbox AI freeze must not assist the next ordinary run after Development ends.");
        File.WriteAllText(Output + "/scenario.txt", "PASS: production F1 prepare twice, no stock pollution, Development, density 1.5, weapon mutual exclusions, pistol reserve, constructor pause/resume, real spawn, scoped clear, AI freeze released on return to bunker.\nUI listeners invoked by scenario; physical input not simulated.");
    }

    private static void OpenSandbox(Subject42DebugMenu menu)
    {
        Call(menu, "ToggleHub");
        Assert.That(Root(menu, "menuRoot").activeSelf, Is.True);
        Assert.That(typeof(Subject42DebugMenu).GetField("activeTab", Hidden).GetValue(menu).ToString(), Is.EqualTo("Sandbox"));
    }

    private static void Click(Subject42DebugMenu menu, string caption)
    {
        var button = Root(menu, "menuRoot").GetComponentsInChildren<Button>().Single(b =>
            b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text.StartsWith(caption, StringComparison.Ordinal));
        Assert.That(button.interactable, Is.True, caption + " must be available in a ready production run.");
        button.onClick.Invoke();
    }

    private static Button WeaponMode(Subject42DebugMenu menu, string group, string mode) =>
        Root(menu, "menuRoot").GetComponentsInChildren<Button>().Single(button =>
            button.gameObject.activeInHierarchy && button.transform.parent.name == "Вариант: " + group &&
            button.GetComponentInChildren<TMP_Text>().text.StartsWith(mode, StringComparison.Ordinal));

    private static IEnumerator Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        yield return new WaitForSecondsRealtime(.15f);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + name + ".png"));
        yield return new WaitForSecondsRealtime(.2f);
    }
}
#endif
