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
        Experiment(menu, "Дробовик: веерная очередь").onClick.Invoke();
        Assert.That(state.SweepingShotgunExperiment, Is.True);
        Assert.That(Experiment(menu, "Дробовик: картечный таран").interactable, Is.False);
        Experiment(menu, "Дробовик: веерная очередь").onClick.Invoke();
        Assert.That(Experiment(menu, "Дробовик: картечный таран").interactable, Is.True);
        Experiment(menu, "Пила: импульсная").onClick.Invoke();
        Assert.That(state.PulsedSawExperiment, Is.True);
        Assert.That(Experiment(menu, "Пила: инерционная").interactable, Is.False);
        Experiment(menu, "Пила: импульсная").onClick.Invoke();
        Assert.That(Experiment(menu, "Пила: инерционная").interactable, Is.True);
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
        var root = Root(menu, "hubRoot");
        var entry = root.GetComponentsInChildren<Button>().Single(b =>
            b.transform.parent.name == "Debug Hub Panel" && b.GetComponentInChildren<TMP_Text>().text == "ПЕСОЧНИЦА");
        Assert.That(entry.interactable, Is.True);
        entry.onClick.Invoke();
    }

    private static void Click(Subject42DebugMenu menu, string caption)
    {
        var button = Root(menu, "menuRoot").GetComponentsInChildren<Button>().Single(b =>
            b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text.StartsWith(caption, StringComparison.Ordinal));
        Assert.That(button.interactable, Is.True, caption + " must be available in a ready production run.");
        button.onClick.Invoke();
    }

    private static Button Experiment(Subject42DebugMenu menu, string row) =>
        Root(menu, "menuRoot").GetComponentsInChildren<Button>().Single(b =>
            b.gameObject.activeInHierarchy && b.transform.parent.name.StartsWith(row, StringComparison.Ordinal));

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
