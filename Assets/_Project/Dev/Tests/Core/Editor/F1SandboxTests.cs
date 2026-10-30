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
        const string output = "Artifacts/DebugMenuAudit/LegacyRemovalSmoke";
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
        File.WriteAllText(output + "/progress.txt", "Production MVP and station ready.\n");
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        File.AppendAllText(output + "/progress.txt", "Menu found: " + (menu != null) + "\n");
        var spawnerOwner = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(spawnerOwner, Is.Not.Null);
        var station = spawnerOwner.Station;
        Assert.That(station, Is.Not.Null);
        var state = station.State;
        Assert.That(state, Is.Not.Null);
        Assert.That(state.Modules, Is.Not.Null);
        Assert.That(state.SegmentedWeapons, Is.Not.Null);
        File.AppendAllText(output + "/progress.txt", "Station/state/collections found.\n");
        int installed = state.Modules.Count, stock = state.SegmentedWeapons.Count;
        try { OpenSandbox(menu); }
        catch (Exception exception)
        {
            File.AppendAllText(output + "/progress.txt", exception.ToString() + "\n");
            throw;
        }
        File.AppendAllText(output + "/progress.txt", "Sandbox opened.\n");
        Choose("Пистолет", "Возврат промаха");
        Assert.That(state.MissReturnPistolExperiment, Is.True);
        Call(menu, "CloseMenu");
        OpenSandbox(menu);
        Assert.That(WeaponMode(menu, "Пистолет", "Возврат промаха").interactable, Is.False);
        Choose("Пистолет", "Обычный");
        Assert.That(state.MissReturnPistolExperiment, Is.False);
        Choose("Снайперка", "Заряженная линия");
        Assert.That(state.ChargedLineSniperExperiment, Is.True);
        Choose("Снайперка", "Обычная");
        Assert.That(state.ChargedLineSniperExperiment, Is.False);
        Choose("Дробовик", "Веерная очередь");
        Assert.That(state.SweepingShotgunExperiment && !state.RamSlugShotgunExperiment, Is.True);
        Choose("Дробовик", "Картечный таран");
        Assert.That(!state.SweepingShotgunExperiment && state.RamSlugShotgunExperiment, Is.True);
        Choose("Дробовик", "Веерная очередь");
        Assert.That(state.SweepingShotgunExperiment && !state.RamSlugShotgunExperiment, Is.True);
        Choose("Дробовик", "Обычный");
        Assert.That(!state.SweepingShotgunExperiment && !state.RamSlugShotgunExperiment, Is.True);
        Choose("Пила", "Импульсная");
        Assert.That(state.PulsedSawExperiment && !state.InertialSawExperiment, Is.True);
        Choose("Пила", "Инерционная");
        Assert.That(!state.PulsedSawExperiment && state.InertialSawExperiment, Is.True);
        Choose("Пила", "Импульсная");
        Assert.That(state.PulsedSawExperiment && !state.InertialSawExperiment, Is.True);
        Choose("Пила", "Обычная");
        Assert.That(!state.PulsedSawExperiment && !state.InertialSawExperiment, Is.True);
        File.AppendAllText(output + "/progress.txt", "All four groups switched mutually exclusively.\n");

        Choose("Пистолет", "Возврат промаха");
        Click(menu, "QA / Лаборатории");
        Root(menu, "menuRoot").GetComponentsInChildren<Button>()
            .Single(b => b.transform.parent.name == "Оружие: эксперименты").onClick.Invoke();
        Assert.That(WeaponMode(menu, "Пистолет", "Возврат промаха").interactable, Is.False,
            "QA and Sandbox must read the same runtime flags.");
        Choose("Снайперка", "Заряженная линия");
        Choose("Дробовик", "Картечный таран");
        Choose("Пила", "Инерционная");
        Assert.That(Root(menu, "menuRoot").GetComponentsInChildren<TMP_Text>()
            .Count(t => t.text.Contains("Не установлен: профиль применится после установки")), Is.EqualTo(3));
        Canvas.ForceUpdateCanvases();
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/selectors.png"));
        yield return new WaitForSecondsRealtime(.15f);
        Choose("Пистолет", "Обычный");
        Choose("Снайперка", "Обычная");
        Choose("Дробовик", "Обычный");
        Choose("Пила", "Обычная");
        Assert.That(state.Modules.Count, Is.EqualTo(installed));
        Assert.That(state.SegmentedWeapons.Count, Is.EqualTo(stock));
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True,
            "Returning to Normal must not restore production progression eligibility.");

        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        spawner.StopSpawning();
        File.AppendAllText(output + "/progress.txt", "Preparing actual projectile target.\n");
        var pistol = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.Kind == OrbitalModuleKind.Pistol);
        Assert.That(pistol.CurrentMount, Is.Not.Null);
        Assert.That(pistol.CurrentMount.Transform, Is.Not.Null);
        Vector2 direction = ((Vector2)pistol.CurrentMount.Transform.position - (Vector2)station.transform.position).normalized;
        Vector2 targetPosition = pistol.WorldPosition + direction * 2f;
        var enemy = spawner.SpawnDebugEnemyAt(spawner.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Basic), targetPosition);
        Assert.That(enemy, Is.Not.Null);
        foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.enabled = false;
        var body = enemy.GetComponent<Rigidbody2D>();
        Assert.That(body, Is.Not.Null);
        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
        yield return null;
        var health = enemy.GetComponent<EnemyHealth>();
        health.SetRuntimeMaxHealth(16f);
        var presentation = enemy.GetComponent<EnemyHealthPresentation>();
        Assert.That(presentation.DeathFxPrefab, Is.Not.Null);
        bool died = false, deathFx = false, popup = false, projectile = false;
        int hits = 0, shotsBefore = pistol.ShotsFired;
        health.DeathFeedback += () =>
        {
            died = true;
            deathFx = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Any(fx => fx.isPlaying && fx.gameObject.name.StartsWith(presentation.DeathFxPrefab.name, StringComparison.Ordinal));
        };
        station.Combat.Hit += (target, damage) =>
        {
            if (target != health) return;
            hits++;
            popup |= Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)
                .Any(p => p.gameObject.activeInHierarchy && p.GetComponent<TextMeshPro>().text == "8");
            if (hits == 1) ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/impact.png"));
        };
        Call(menu, "CloseMenu");
        float deadline = Time.realtimeSinceStartup + 6f;
        while (!died && Time.realtimeSinceStartup < deadline)
        {
            projectile |= ((ProductionOrbitalCombatAdapter)station.Combat).LabActiveProjectileCount > 0;
            yield return null;
        }
        Assert.That(pistol.ShotsFired, Is.GreaterThan(shotsBefore), "The production Segmented pistol must fire itself.");
        Assert.That(projectile && hits >= 2 && popup && died && deathFx, Is.True,
            $"projectile={projectile}, hits={hits}, popup={popup}, death={died}, deathFX={deathFx}");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/death.png"));
        yield return new WaitForSecondsRealtime(.15f);
        File.WriteAllText(output + "/result.txt", "PASS: four runtime selectors, all six variants, immediate replacement, Normal, reopen and shared QA/Sandbox state.\n" +
            "Installed build/stock preserved; uninstalled weapon disclosure and irreversible Development flag verified.\n" +
            "Actual production Segmented pistol fired projectiles, hit twice, produced pooled damage popup and enemy death FX.\n" +
            "One scripted Editor Play Mode smoke; no physical keyboard/mouse simulation.\n" + Application.dataPath + "\nUnity=" + Application.unityVersion);

        void Choose(string group, string mode)
        {
            var button = WeaponMode(menu, group, mode);
            Assert.That(button.interactable, Is.True, group + ": " + mode);
            button.onClick.Invoke();
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
        WeaponMode(menu, "Дробовик", "Веерная очередь").onClick.Invoke();
        Assert.That(state.SweepingShotgunExperiment, Is.True);
        WeaponMode(menu, "Дробовик", "Картечный таран").onClick.Invoke();
        Assert.That(state.RamSlugShotgunExperiment && !state.SweepingShotgunExperiment, Is.True);
        WeaponMode(menu, "Дробовик", "Обычный").onClick.Invoke();
        Assert.That(!state.RamSlugShotgunExperiment && !state.SweepingShotgunExperiment, Is.True);
        WeaponMode(menu, "Пила", "Импульсная").onClick.Invoke();
        Assert.That(state.PulsedSawExperiment, Is.True);
        WeaponMode(menu, "Пила", "Инерционная").onClick.Invoke();
        Assert.That(state.InertialSawExperiment && !state.PulsedSawExperiment, Is.True);
        WeaponMode(menu, "Пила", "Обычная").onClick.Invoke();
        Assert.That(!state.InertialSawExperiment && !state.PulsedSawExperiment, Is.True);
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
