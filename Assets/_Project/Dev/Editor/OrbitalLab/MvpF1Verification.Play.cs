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

// Opt-in, one bounded MVP run. Captures engine-rendered UI, never simulates a manual review.
public static partial class MvpF1Verification
{
    private const string PlayKey = "MvpF1Verification.Active";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Stack<IEnumerator> playSteps = new();
    private static readonly List<string> playReport = new();
    private static double nextStep, playDeadline;
    private static int failures, runtimeErrors;
    private static Subject42DebugMenu menu;
    private static OrbitalStationRuntime station;
    private static UpgradeManager manager;
    private static object Field(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    private static void Check(bool pass, string message)
    {
        playReport.Add((pass ? "PASS: " : "FAIL: ") + message);
        if (!pass) failures++;
        File.WriteAllLines(Output + "/play-progress.txt", playReport);
    }
    private static void PlayLog(string message, string stack, LogType type)
    {
        if (playSteps.Count > 0 && type is LogType.Error or LogType.Exception or LogType.Assert)
        { runtimeErrors++; playReport.Add("UNITY ERROR: " + message); }
    }
    private static void PollPlay()
    {
        if (File.Exists(Output + "/play.request") && !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(Output + "/play.request");
            try
            {
                Audit();
                string audit = File.ReadAllText(Output + "/audit.txt");
                if (audit.Contains("TEXT_FAIL") || audit.Contains("MISSING_ICON")) throw new Exception("Card preflight failed");
                PlayerScriptCompilationCheck.Run();
                string compilation = File.ReadAllText("Artifacts/GeneratedQA/Phase6/Compilation/results.txt");
                File.WriteAllText(Output + "/compile.txt", compilation);
                if (!compilation.Contains("Release: PASS") || !compilation.Contains("Development: PASS")) throw new Exception("Script compilation failed");
                CoreTestSupport.PreservePreferences();
                PlayerPrefs.SetInt(LocalizationService.LanguagePreferenceKey, (int)GameLanguage.Russian);
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
                SessionState.SetBool(PlayKey, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { File.WriteAllText(Output + "/play.txt", "PREFLIGHT FAIL: " + e); }
        }
        if (playSteps.Count == 0 || EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            if (EditorApplication.timeSinceStartup > playDeadline) throw new Exception("Bounded run timed out");
            var step = playSteps.Peek();
            if (!step.MoveNext()) { playSteps.Pop(); if (playSteps.Count == 0) FinishPlay(); return; }
            if (step.Current is IEnumerator child) playSteps.Push(child);
            else nextStep = EditorApplication.timeSinceStartup + (step.Current is float delay ? delay : .025);
        }
        catch (Exception e) { failures++; playReport.Add(e.ToString()); FinishPlay(); }
    }
    private static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PlayKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            failures = runtimeErrors = 0; playReport.Clear();
            playSteps.Push(PlayChecks()); nextStep = 0; playDeadline = EditorApplication.timeSinceStartup + 180;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(PlayKey);
            var cleanup = CoreTestSupport.CleanupPlayMode(); while (cleanup.MoveNext()) { }
        }
    }
    private static void FinishPlay()
    {
        playSteps.Clear();
        playReport.Add($"RESULT: {(failures == 0 && runtimeErrors == 0 ? "PASS" : "FAIL")} · failures={failures}, runtime errors={runtimeErrors}");
        playReport.Add("One scripted Play Mode. Physical keyboard/mouse interaction was not verified by this runner.");
        File.WriteAllLines(Output + "/play.txt", playReport);
        EditorApplication.ExitPlaymode();
    }
    private static IEnumerator Wait(Func<bool> condition, string message, float seconds = 25f)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        while (!condition() && EditorApplication.timeSinceStartup < until) yield return null;
        if (!condition()) throw new Exception("Timed out: " + message);
    }
    private static void Open(int tab)
    {
        Call(menu, "SelectTab", Enum.ToObject(typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic), tab), true);
        Call(menu, "SetOpen", true);
    }
    private static Button RowButton(string row)
    {
        var content = (RectTransform)Field(menu, "contentRoot");
        return content.GetComponentsInChildren<Button>().First(b => b.transform.parent.name == row);
    }
    private static IEnumerator Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        yield return .15f;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + name + ".png"));
        yield return .15f;
    }
    private static IEnumerator ResolveReward()
    {
        double until = EditorApplication.timeSinceStartup + 7;
        while (manager.IsChoosingUpgrade && EditorApplication.timeSinceStartup < until)
        { station.RewardFlow.DebugChooseFirstValidTarget(); yield return .06f; }
        Check(manager.IsRewardQueueIdle && Mathf.Approximately(Time.timeScale, 1f), "reward flow terminates and restores gameplay time");
    }
    private static IEnumerator ChooseCard(OrbitalRewardKind kind)
    {
        Check(manager.DebugOfferCatalogueReward(kind), "specific normal card offered: " + kind);
        yield return .08f;
        var panel = Object.FindFirstObjectByType<UpgradePanelView>();
        if (panel == null) throw new Exception("Missing real card UI: " + kind);
        Check(panel.CardViews.Count(c => c.gameObject.activeSelf) == 1, "one actual card, no placeholders: " + kind);
        panel.CardViews.First(c => c.gameObject.activeSelf).GetComponent<Button>().onClick.Invoke();
        yield return ResolveReward();
    }
    private static IEnumerator PlayChecks()
    {
        yield return Wait(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning, "bunker");
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        yield return Wait(() => SceneManager.GetActiveScene().name == "MVP" && Object.FindFirstObjectByType<CharacterSpawner>()?.Station is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning, "MVP");
        station = Object.FindFirstObjectByType<CharacterSpawner>().Station; manager = UpgradeManager.Instance;
        menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        station.Owner.Transform.GetComponent<PlayerHealth>().DebugInvulnerable = true;
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        Check(manager.DebugRewardCatalogue.Count == 18 && manager.DebugRewardCatalogue.All(d => OrbitalRewardProvider.IsDemoReward(d.RewardKind)), "18 active source definitions, no retired/lab-only rewards");
        Open(0); yield return .1f;
        Check((bool)Field(menu, "isOpen") && Time.timeScale == 0f && station.InputOwner.IsGameplayInputBlocked, "F1 open pauses and blocks station input");
        var movement = station.Owner.Transform.GetComponent<CharacterMovement2D>();
        Check(movement.DirectionalInputFilter(Vector2.one) == Vector2.zero && !station.InputOwner.CanStartRelocation, "menu blocks movement directions and relocation");
        Check(station.InputOwner.TryConsumeEscape(), "menu Escape is consumed before pause handling");
        Call(menu, "SetOpen", false);
        Check(Time.timeScale == 1f && !station.InputOwner.IsGameplayInputBlocked, "F1 close restores time and input");
        Open(0);
        for (int r = 0; r < 3; r++)
        {
            if (r > 0) RowButton("Добавить орбиту").onClick.Invoke();
            Call(menu, "RefreshCurrentTab"); yield return .03f;
            while (station.State.CanAddMount(station.SelectedRing.RingId, out _))
            { RowButton("Добавить точку крепления").onClick.Invoke(); yield return .03f; }
            Check(!RowButton("Добавить точку крепления").interactable, "selected orbit capacity disables add-point button");
        }
        Check(station.Rings.Count == 3 && station.State.FreeBuiltMounts == 8, "F1 runtime adds orbits and points on selected orbit");
        yield return Capture("f1-orbits");
        Call(menu, "SetOpen", false);
        int modulesBefore = station.Modules.Count;
        Check(manager.DebugOfferCatalogueReward(OrbitalRewardKind.LinkPair, true), "pair placement begins");
        station.RewardFlow.DebugChooseFirstValidTarget();
        yield return Wait(() => station.RewardFlow.State == OrbitalRewardFlowState.SecondLinkPlacement, "second link stage", 5f);
        Check(station.Modules.Count == modulesBefore, "first link target only stages preview");
        Check(manager.DebugCancelCatalogueReward(), "cancel staged pair");
        yield return .05f;
        Check(station.Modules.Count == modulesBefore && manager.IsRewardQueueIdle && station.InputOwner.IsIdle && Time.timeScale == 1f, "pair cancel leaves no module, reservation or paused queue");
        foreach (var kind in new[] { OrbitalRewardKind.Pistol, OrbitalRewardKind.ArcEmitter, OrbitalRewardKind.LinkPair, OrbitalRewardKind.Sniper, OrbitalRewardKind.Shotgun, OrbitalRewardKind.Saw })
        {
            Open(0); yield return .03f;
            var definition = manager.DebugRewardCatalogue.First(d => d.RewardKind == kind);
            RowButton(LocalizationService.Instance.Get(definition.upgradeName)).onClick.Invoke();
            Check(!(bool)Field(menu, "isOpen") && manager.IsChoosingUpgrade, "F1 closes into actual installation: " + kind);
            yield return ResolveReward();
            Check(station.Modules.Any(m => m.Kind == OrbitalRewardProvider.GetModuleKind(kind)), "installed runtime weapon: " + kind);
        }
        Check(manager.DebugRewardUnavailableReason(OrbitalRewardKind.LinkPair) != null && !manager.DebugOfferCatalogueReward(OrbitalRewardKind.LinkPair, true), "pair requires two free points, no limit bypass");
        Open(1); yield return .05f;
        var catalogue = manager.DebugRewardCatalogue.ToArray();
        foreach (var definition in catalogue)
        {
            Set(menu, "mvpPreviewKind", definition.RewardKind); Call(menu, "RefreshCurrentTab"); yield return .03f;
            var preview = (GameObject)Field(menu, "mvpPreviewRoot");
            var card = preview.GetComponentInChildren<UpgradeCardView>();
            var title = (TextMeshProUGUI)Field(card, "titleText");
            var description = (TextMeshProUGUI)Field(card, "descriptionText");
            title.ForceMeshUpdate(); description.ForceMeshUpdate();
            Check(title.text == LocalizationService.Instance.Get(definition.upgradeName) &&
                description.text.Contains(ProductionUpgradePresentation.GetCardDescription(definition)) &&
                title.textInfo.characterCount > 0 && description.textInfo.characterCount > 0 &&
                title.alpha > .99f && description.alpha > .99f && ((Image)Field(card, "iconImage")).sprite != null,
                "fresh real card text/mesh/icon: " + definition.RewardKind);
            if (OrbitalRewardProvider.GetModuleKind(definition.RewardKind).HasValue)
                Check(Mathf.Abs(card.GetComponent<Button>().colors.normalColor.r - .64f) < .01f, "red weapon palette: " + definition.RewardKind);
            yield return Capture("card-" + definition.RewardKind);
        }
        Set(menu, "mvpPreviewKind", OrbitalRewardKind.Saw); Call(menu, "RefreshCurrentTab");
        yield return Capture("f1-catalogue");
        for (int category = 0; category < 4; category++)
        {
            Set(menu, "mvpCategoryFilter", category); Call(menu, "RefreshCurrentTab"); yield return .03f;
            var content = (RectTransform)Field(menu, "contentRoot");
            Check(content.GetComponentsInChildren<Button>().Count(b => b.transform.parent.name.StartsWith("Каталог ·")) == catalogue.Count(d => (int)d.EffectCategory == category), "category filter: " + category);
        }
        Set(menu, "mvpCategoryFilter", -1); Set(menu, "mvpAvailableOnly", true); Call(menu, "RefreshCurrentTab"); yield return .03f;
        Check(((RectTransform)Field(menu, "contentRoot")).GetComponentsInChildren<Button>().Count(b => b.transform.parent.name.StartsWith("Каталог ·")) == catalogue.Count(d => manager.DebugRewardUnavailableReason(d.RewardKind) == null), "available-now filter uses real restrictions");
        Call(menu, "SetOpen", false);
        var priorDamage = station.State.ShootingDamageMultiplier;
        foreach (var kind in new[] { OrbitalRewardKind.ShootingDamage, OrbitalRewardKind.ShootingRate, OrbitalRewardKind.ContactDamage, OrbitalRewardKind.ContactSize, OrbitalRewardKind.RingPower, OrbitalRewardKind.RingSpeed, OrbitalRewardKind.RingCapacity, OrbitalRewardKind.AddMount, OrbitalRewardKind.MaxHealth, OrbitalRewardKind.MoveSpeed, OrbitalRewardKind.NewRing })
            yield return ChooseCard(kind);
        Check(Mathf.Approximately(station.State.ShootingDamageMultiplier, priorDamage * 1.2f) && station.Rings.Count == 4, "specific cards actually change category/orbit state");
        Open(1); RowButton("Случайный выбор из трёх").onClick.Invoke(); yield return .05f;
        Check(manager.DebugCurrentChoices.Count == 3 && manager.DebugCurrentChoices.All(d => d != null), "normal random three contains only real eligible choices");
        manager.DebugCancelCatalogueReward(); yield return .05f;
        // Reproduce an interrupted reveal and a null gap against the actual shared card UI.
        var panelView = (UpgradePanelView)Field(manager, "upgradePanelView");
        var reused = panelView.CardViews[0]; var group = reused.GetComponent<CanvasGroup>();
        if (group == null) group = reused.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;
        reused.Setup(null, null);
        panelView.ShowWorldEventReward("Проверка", "Две реальные карточки", new UpgradeData[] { catalogue[0], null, catalogue[1] }, _ => { });
        panelView.Hide();
        panelView.Show(2, new UpgradeData[] { catalogue[1], null, catalogue[0] }, _ => { });
        yield return .1f;
        Check(panelView.CardViews.Count(c => c.gameObject.activeSelf) == 2 && panelView.CardViews.Take(2).All(c => c.gameObject.activeSelf) && group.alpha == 1f && group.blocksRaycasts, "interrupted reveal + null gap reuse gives two visible compact cards");
        panelView.Hide();
        foreach (int tab in new[] { 0, 1, 2, 3, 1, 0 }) { Open(tab); yield return .03f; Call(menu, "SetOpen", false); }
        Check(Time.timeScale == 1f && manager.IsRewardQueueIdle, "repeated tabs/open/close restore gameplay");
        // State cap reached through its normal runtime mutation, then catalogue refuses further rewards.
        foreach (var ring in station.Rings) while (station.State.CanUpgradeRingSpeed(ring.RingId, out _)) station.UpgradeRingSpeed(ring.RingId);
        Check(manager.DebugRewardUnavailableReason(OrbitalRewardKind.RingSpeed) != null && !manager.DebugOfferCatalogueReward(OrbitalRewardKind.RingSpeed), "speed cap is enforced by catalogue");
    }
}
#endif
