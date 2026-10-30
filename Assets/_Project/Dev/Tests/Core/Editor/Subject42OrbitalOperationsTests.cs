using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

public sealed class Subject42OrbitalOperationsTests
{
    [Category("Core"), UnityTest]
    public IEnumerator SegmentedWeaponRewardDescriptionsProductionMvpGameViewCapture()
    {
        var assembly = typeof(Editor).Assembly;
        var viewType = assembly.GetType("UnityEditor.GameView");
        var existing = Resources.FindObjectsOfTypeAll(viewType).OfType<EditorWindow>().FirstOrDefault();
        bool hadView = existing != null;
        var view = hadView ? existing : EditorWindow.GetWindow(viewType);
        int originalSize = (int)viewType.GetProperty("selectedSizeIndex").GetValue(view);
        Rect originalPosition = view.position;
        bool originalMaximized = view.maximized;
        var originalFocus = EditorWindow.focusedWindow;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        int originalCount = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        int builtinCount = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        try
        {
            typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return new EnterPlayMode();
            yield return SegmentedWeaponRewardDescriptionsProductionBody();
        }
        finally
        {
            // Reacquire editor reflection targets after EnterPlayMode/domain reload.
            assembly = typeof(Editor).Assembly;
            viewType = assembly.GetType("UnityEditor.GameView");
            view = Resources.FindObjectsOfTypeAll(viewType).OfType<EditorWindow>().FirstOrDefault();
            if (view != null)
            {
                viewType.GetProperty("selectedSizeIndex").SetValue(view, originalSize);
                view.maximized = originalMaximized;
                view.position = originalPosition;
            }
            sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
            if (group != null && (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) == originalCount + 1)
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { originalCount - builtinCount });
            if (!hadView && view != null) view.Close();
            if (originalFocus != null) originalFocus.Focus();
        }
    }

    private static IEnumerator SegmentedWeaponRewardDescriptionsProductionBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var manager = UpgradeManager.Instance;
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        var localization = LocalizationService.EnsureExists();
        var originalLanguage = localization.CurrentLanguage;
        bool originalInvulnerable = health.DebugInvulnerable;
        float originalScale = Time.timeScale;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var languageField = typeof(LocalizationService).GetField("<CurrentLanguage>k__BackingField", hidden);
        var changedField = typeof(LocalizationService).GetField("LanguageChanged", hidden);
        Assert.That(languageField, Is.Not.Null);
        Assert.That(changedField, Is.Not.Null);
        void ChangeRuntimeLanguage(GameLanguage language)
        {
            if (localization == null) return;
            languageField.SetValue(localization, language);
            ((System.Action<GameLanguage>)changedField.GetValue(localization))?.Invoke(language);
        }
        try
        {
            RunStateManager.Instance.DebugMarkDevelopmentRun();
            health.DebugInvulnerable = true;
            var state = station.State;
            Assert.That(state.MissReturnPistolExperiment || state.RamSlugShotgunExperiment ||
                state.SweepingShotgunExperiment || state.PulsedSawExperiment || state.ChargedLineSniperExperiment,
                Is.False, "Ordinary weapons must be checked with weapon experiments OFF.");
            var kinds = new[] { OrbitalRewardKind.Pistol, OrbitalRewardKind.Shotgun,
                OrbitalRewardKind.Saw, OrbitalRewardKind.Sniper };
            var keys = new[] { "reward.segmentedGunDescription", "reward.segmentedShotgunDescription",
                "reward.segmentedSawDescription", "reward.segmentedSniperDescription" };
            int installedBefore = state.Modules.Count;
            int stockBefore = state.SegmentedWeapons.Count;
            foreach (var language in new[] { GameLanguage.Russian, GameLanguage.English })
            {
                ChangeRuntimeLanguage(language);
                var catalogue = manager.DebugRewardCatalogue;
                for (int i = 0; i < kinds.Length; i++)
                {
                    var definition = catalogue.Single(d => d.RewardKind == kinds[i]);
                    Assert.That(definition.description, Is.EqualTo(keys[i]));
                    string resolved = localization.Get(definition.description);
                    Assert.That(resolved, Does.Not.Contain("reward."));
                    Assert.That(resolved, Does.Contain(language == GameLanguage.Russian
                        ? "+1 оружие в запас." : "+1 weapon in reserve."));
                    Assert.That(resolved, Does.Contain(language == GameLanguage.Russian
                        ? "Установка через Tab в ячейку O." : "Place in an O slot via Tab."));
                }
                foreach (var kind in new[] { OrbitalRewardKind.Shotgun, OrbitalRewardKind.Sniper })
                {
                    Assert.That(manager.DebugOfferCatalogueReward(kind), Is.True,
                        manager.DebugRewardUnavailableReason(kind));
                    yield return null;
                    // Let the authored panel's unscaled show animation settle.
                    yield return new WaitForSecondsRealtime(0.5f);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(manager.IsChoosingUpgrade, Is.True);
                    Assert.That(manager.DebugCurrentChoices.Count, Is.EqualTo(1));
                    Assert.That(((OrbitalRewardData)manager.DebugCurrentChoices[0]).RewardKind, Is.EqualTo(kind));
                    var card = manager.DebugCardTemplate;
                    Assert.That(card, Is.Not.Null);
                    Assert.That(card.gameObject.activeInHierarchy, Is.True);
                    var text = (TMPro.TMP_Text)typeof(UpgradeCardView).GetField("descriptionText", hidden).GetValue(card);
                    var definition = manager.DebugRewardCatalogue.Single(d => d.RewardKind == kind);
                    Assert.That(text.text, Does.Contain(localization.Get(definition.description)));
                    Assert.That(text.text, Does.Not.Contain("reward."));
                    Assert.That(station.InputOwner.IsConstructing, Is.False);
                    Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
                    Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
                    Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
                    // Both cards are checked in both languages; capture two representative real frames.
                    if (language == GameLanguage.Russian && kind == OrbitalRewardKind.Shotgun)
                        yield return CaptureSweepingShotgunGameView("weapon-reward-ru");
                    if (language == GameLanguage.English && kind == OrbitalRewardKind.Sniper)
                        yield return CaptureSweepingShotgunGameView("weapon-reward-en");
                    bool grant = language == GameLanguage.English && kind == OrbitalRewardKind.Sniper;
                    if (grant)
                    {
                        var button = (UnityEngine.UI.Button)typeof(UpgradeCardView).GetField("button", hidden).GetValue(card);
                        Assert.That(button != null && button.interactable, Is.True);
                        button.onClick.Invoke(); // Production card callback -> normal reward flow.
                    }
                    else
                        Assert.That(manager.DebugCancelCatalogueReward(), Is.True);
                    yield return CoreTestSupport.Await(() => manager != null && manager.IsRewardQueueIdle);
                    Assert.That(station.InputOwner.IsConstructing, Is.False,
                        "Weapon rewards must not open the constructor automatically.");
                    Assert.That(state.Modules.Count, Is.EqualTo(installedBefore), "No automatic installation.");
                    Assert.That(state.SegmentedWeapons.Count, Is.EqualTo(stockBefore + (grant ? 1 : 0)));
                    if (grant)
                        Assert.That(state.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Sniper),
                            Is.EqualTo(1));
                }
            }
        }
        finally
        {
            if (manager != null && manager.IsChoosingUpgrade) manager.DebugCancelCatalogueReward();
            ChangeRuntimeLanguage(originalLanguage);
            if (health != null) health.DebugInvulnerable = originalInvulnerable;
            Time.timeScale = originalScale;
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator ConstructorLocalizationProductionMvpGameViewCapture()
    {
        var assembly = typeof(Editor).Assembly;
        var viewType = assembly.GetType("UnityEditor.GameView");
        var existing = Resources.FindObjectsOfTypeAll(viewType).OfType<EditorWindow>().FirstOrDefault();
        bool hadView = existing != null;
        var view = hadView ? existing : EditorWindow.GetWindow(viewType);
        int originalSize = (int)viewType.GetProperty("selectedSizeIndex").GetValue(view);
        Rect originalPosition = view.position;
        bool originalMaximized = view.maximized;
        var originalFocus = EditorWindow.focusedWindow;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        int originalCount = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        int builtinCount = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        try
        {
            typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return new EnterPlayMode();
            yield return ConstructorLocalizationProductionBody();
        }
        finally
        {
            // Reacquire editor reflection targets after EnterPlayMode/domain reload.
            assembly = typeof(Editor).Assembly;
            viewType = assembly.GetType("UnityEditor.GameView");
            view = Resources.FindObjectsOfTypeAll(viewType).OfType<EditorWindow>().FirstOrDefault();
            if (view != null)
            {
                viewType.GetProperty("selectedSizeIndex").SetValue(view, originalSize);
                view.maximized = originalMaximized;
                view.position = originalPosition;
            }
            sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
            if (group != null && (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) == originalCount + 1)
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { originalCount - builtinCount });
            if (!hadView && view != null) view.Close();
            if (originalFocus != null) originalFocus.Focus();
        }
    }

    private static IEnumerator ConstructorLocalizationProductionBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var presentation = station.Interaction;
        Assert.That(presentation, Is.Not.Null);
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        var localization = LocalizationService.EnsureExists();
        var originalLanguage = localization.CurrentLanguage;
        bool originalInvulnerable = health.DebugInvulnerable;
        float originalScale = Time.timeScale;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var languageField = typeof(LocalizationService).GetField("<CurrentLanguage>k__BackingField", hidden);
        var changedField = typeof(LocalizationService).GetField("LanguageChanged", hidden);
        Assert.That(languageField, Is.Not.Null);
        Assert.That(changedField, Is.Not.Null);
        void ChangeRuntimeLanguage(GameLanguage language)
        {
            if (localization == null) return;
            languageField.SetValue(localization, language);
            ((System.Action<GameLanguage>)changedField.GetValue(localization))?.Invoke(language);
        }
        T Read<T>(string name) => (T)typeof(OrbitalInteractionPresentation).GetField(name, hidden).GetValue(presentation);
        string RowText(UnityEngine.UI.Button row) => row.GetComponentInChildren<TMPro.TMP_Text>(true).text;
        try
        {
            RunStateManager.Instance.DebugMarkDevelopmentRun();
            health.DebugInvulnerable = true;
            var state = station.State;
            Assert.That(state.SegmentedWeapons, Is.Empty);
            Assert.That(state.SegmentedLinkStock, Is.Empty);
            Assert.That(state.TryGrantSegmentedWeapon(OrbitalModuleKind.Shotgun), Is.True);
            Assert.That(state.TryGrantSegmentedLink(SegmentedLinkKind.Damage), Is.True);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            var panel = Read<GameObject>("constructorPanel");
            var empty = Read<TMPro.TextMeshProUGUI>("constructorEmpty");
            var weapons = Read<UnityEngine.UI.Button[]>("constructorWeapons");
            var links = Read<UnityEngine.UI.Button[]>("constructorLinks");
            var weaponKinds = Read<OrbitalModuleKind[]>("constructorWeaponKinds");
            var linkKinds = Read<SegmentedLinkKind[]>("constructorModuleKinds");
            int weaponIndex = System.Array.IndexOf(weaponKinds, OrbitalModuleKind.Shotgun);
            int linkIndex = System.Array.IndexOf(linkKinds, SegmentedLinkKind.Damage);
            Assert.That(weaponIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(linkIndex, Is.GreaterThanOrEqualTo(0));
            string[] keys = { "constructor.title", "constructor.weaponsHeading", "constructor.modulesHeading",
                "constructor.upgradesButton", "constructor.continueButton" };
            string[][] expected = {
                new[] { "ОРБИТАЛЬНЫЙ КОНСТРУКТОР", "О  ОРУЖИЕ", "M  МОДУЛИ", "TAB — УЛУЧШЕНИЯ", "ПРОДОЛЖИТЬ" },
                new[] { "ORBITAL CONSTRUCTOR", "O  WEAPONS", "M  MODULES", "TAB — UPGRADES", "CONTINUE" }
            };
            for (int languageIndex = 0; languageIndex < 2; languageIndex++)
            {
                var language = languageIndex == 0 ? GameLanguage.Russian : GameLanguage.English;
                ChangeRuntimeLanguage(language);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.activeInHierarchy && station.InputOwner.IsConstructing, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(empty.gameObject.activeSelf, Is.False);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i == 3) continue; // Hidden launcher's OnEnable is exercised below.
                    var label = presentation.GetComponentsInChildren<LocalizedText>(true).Single(component =>
                        (string)typeof(LocalizedText).GetField("localizationKey", hidden).GetValue(component) == keys[i]);
                    var text = label.GetComponent<TMPro.TMP_Text>();
                    Assert.That(text.text, Is.EqualTo(expected[languageIndex][i]), keys[i]);
                    Assert.That(label.gameObject.activeInHierarchy, Is.True, keys[i]);
                }
                Assert.That(weapons[weaponIndex].gameObject.activeInHierarchy, Is.True);
                Assert.That(links[linkIndex].gameObject.activeInHierarchy, Is.True);
                Assert.That(RowText(weapons[weaponIndex]), Is.EqualTo(
                    (languageIndex == 0 ? "Дробовик" : "Shotgun") + "  ×1"));
                Assert.That(RowText(links[linkIndex]), Is.EqualTo(
                    (languageIndex == 0 ? "Урон +20%" : "Damage +20%") + "  ×1"));
                Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
                Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
                Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
                // The launcher is intentionally inactive in the open panel, so verify
                // its authored LocalizedText when the normal HUD makes it visible.
                Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
                yield return null;
                var launcher = Read<UnityEngine.UI.Button>("constructorLauncher");
                Assert.That(launcher.gameObject.activeInHierarchy, Is.True);
                var launcherLabel = launcher.GetComponentInChildren<LocalizedText>(true);
                Assert.That(launcherLabel, Is.Not.Null);
                Assert.That((string)typeof(LocalizedText).GetField("localizationKey", hidden).GetValue(launcherLabel),
                    Is.EqualTo(keys[3]));
                Assert.That(RowText(launcher), Is.EqualTo(expected[languageIndex][3]));
                Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.activeInHierarchy && station.InputOwner.IsConstructing, Is.True);
                yield return CaptureSweepingShotgunGameView(languageIndex == 0 ? "constructor-ru" : "constructor-en");
            }
            int inner = state.Rings.Single(r => r.Order == 0).StableRingId;
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Shotgun, inner, 2, out string weaponError),
                Is.True, weaponError);
            Assert.That(station.ConstructModule(SegmentedLinkKind.Damage, inner, 1, out string moduleError),
                Is.True, moduleError);
            Assert.That(state.SegmentedWeapons, Is.Empty);
            Assert.That(state.SegmentedLinkStock, Is.Empty);
            foreach (var language in new[] { GameLanguage.Russian, GameLanguage.English })
            {
                ChangeRuntimeLanguage(language);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.activeInHierarchy && station.InputOwner.IsConstructing, Is.True);
                Assert.That(empty.gameObject.activeInHierarchy, Is.True);
                Assert.That(empty.text, Is.EqualTo(language == GameLanguage.Russian ? "Запас пуст." : "Stock is empty."));
                Assert.That(weapons.Any(row => row.gameObject.activeSelf), Is.False);
                Assert.That(links.Any(row => row.gameObject.activeSelf), Is.False);
            }
        }
        finally
        {
            ChangeRuntimeLanguage(originalLanguage);
            if (presentation != null) presentation.CloseConstructor();
            else if (station != null && station.InputOwner != null) station.InputOwner.EndConstructor();
            if (health != null) health.DebugInvulnerable = originalInvulnerable;
            Time.timeScale = originalScale;
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator SegmentedBuildOverviewProductionMvpGameViewCapture()
    {
        var assembly = typeof(Editor).Assembly;
        var viewType = assembly.GetType("UnityEditor.GameView");
        var existingView = Resources.FindObjectsOfTypeAll(viewType).OfType<EditorWindow>().FirstOrDefault();
        var previousFocus = EditorWindow.focusedWindow;
        var view = existingView != null ? existingView : EditorWindow.GetWindow(viewType);
        var selectedSize = viewType.GetProperty("selectedSizeIndex");
        int previousSize = (int)selectedSize.GetValue(view);
        Rect previousPosition = view.position;
        bool previousMaximized = view.maximized;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var groupType = group.GetType();
        int previousCount = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int builtinCount = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);
        try
        {
            typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return new EnterPlayMode();
            yield return SegmentedBuildOverviewProductionBody();
        }
        finally
        {
            // Restore the pre-capture editor state even when readiness or an assertion fails.
            if (view != null)
            {
                selectedSize.SetValue(view, previousSize);
                view.maximized = previousMaximized;
                view.position = previousPosition;
            }
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            if (count == previousCount + 1)
                groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { previousCount - builtinCount });
            if (existingView == null && view != null) view.Close();
            if (previousFocus != null) previousFocus.Focus();
        }
    }

    private static IEnumerator SegmentedBuildOverviewProductionBody()
    {
        yield return BeginSegmentedBuildOverviewProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        Assert.That(pause, Is.Not.Null);
        var localization = LocalizationService.EnsureExists();
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        var originalLanguage = localization.CurrentLanguage;
        bool invulnerable = health.DebugInvulnerable;
        float scale = Time.timeScale;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        // Exercise the real LanguageChanged listeners without SetLanguage's persistent preference write.
        var languageField = typeof(LocalizationService).GetField("<CurrentLanguage>k__BackingField", hidden);
        var changedField = typeof(LocalizationService).GetField("LanguageChanged", hidden);
        Assert.That(languageField, Is.Not.Null);
        Assert.That(changedField, Is.Not.Null);
        void ChangeRuntimeLanguage(GameLanguage language)
        {
            languageField.SetValue(localization, language);
            ((System.Action<GameLanguage>)changedField.GetValue(localization))?.Invoke(language);
        }
        TMPro.TMP_Text ReadText(PauseBuildOverview overview, string field) =>
            (TMPro.TMP_Text)typeof(PauseBuildOverview).GetField(field, hidden).GetValue(overview);
        try
        {
            RunStateManager.Instance.DebugMarkDevelopmentRun();
            health.DebugInvulnerable = true;
            var state = station.State;
            Assert.That(state.Modules.Count, Is.EqualTo(1), "Fresh production run starts with one pistol.");
            Assert.That(state.SegmentedLinks, Is.Empty);
            Assert.That(state.SegmentedLinkStock, Is.Empty);
            Assert.That(state.SegmentedWeaponLimit, Is.EqualTo(3));
            int inner = state.Rings.Single(r => r.Order == 0).StableRingId;
            Assert.That(state.TryGrantSegmentedWeapon(OrbitalModuleKind.Shotgun), Is.True);
            Assert.That(state.TryGrantSegmentedLink(SegmentedLinkKind.Damage), Is.True);
            Assert.That(state.TryGrantSegmentedLink(SegmentedLinkKind.Damage), Is.True);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Shotgun, inner, 2, out string weaponError),
                Is.True, weaponError);
            Assert.That(station.ConstructModule(SegmentedLinkKind.Damage, inner, 1, out string moduleError),
                Is.True, moduleError);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            yield return null;
            Assert.That(state.SegmentedLinks.Count, Is.EqualTo(1));
            Assert.That(state.SegmentedLinkStock.Count, Is.EqualTo(1));
            pause.Pause(); // Same public path as Escape, including run binding and full pause.
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var overview = Object.FindFirstObjectByType<PauseBuildOverview>();
            Assert.That(overview, Is.Not.Null);
            Assert.That(overview.gameObject.activeInHierarchy, Is.True);
            foreach (var language in new[] { GameLanguage.Russian, GameLanguage.English })
            {
                ChangeRuntimeLanguage(language); // Keep the overview open: verify its live subscription.
                yield return null;
                Canvas.ForceUpdateCanvases();
                var rings = ReadText(overview, "ringsText");
                var modules = ReadText(overview, "modulesText");
                Assert.That(rings.text, Does.Contain("O: 2/6").And.Contain("M: 1/6"));
                string shortName = language == GameLanguage.Russian ? "Урон +20%" : "Damage +20%";
                string limit = language == GameLanguage.Russian ? "Лимит оружия" : "Weapon limit";
                string shotgun = language == GameLanguage.Russian ? "Дробовик" : "Shotgun";
                Assert.That(localization.Get("constructor.damageLabel"), Is.EqualTo(shortName));
                Assert.That(localization.Get("pause.weaponLimit"), Is.EqualTo(limit));
                Assert.That(modules.text, Does.Contain(shortName + " ×1"));
                Assert.That(modules.text, Does.Not.Contain(shortName + " ×2"), "Reserve must not count as installed M.");
                Assert.That(modules.text, Does.Contain(shotgun + " ×1"));
                Assert.That(modules.text, Does.Contain(limit + ": 2/3"), "M must not consume the weapon limit.");
                Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
                Assert.That(Camera.main.isActiveAndEnabled, Is.True);
                Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
                yield return CaptureSweepingShotgunGameView(language == GameLanguage.Russian
                    ? "build-overview-ru" : "build-overview-en");
            }
        }
        finally
        {
            ChangeRuntimeLanguage(originalLanguage);
            station.InputOwner.EndConstructor();
            pause.Resume();
            health.DebugInvulnerable = invulnerable;
            Time.timeScale = scale;
        }
    }

    private static IEnumerator BeginSegmentedBuildOverviewProductionRun()
    {
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(RunEndService.BunkerSceneName);
        float bunkerDeadline = Time.realtimeSinceStartup + 30f;
        yield return WaitForBuildOverviewDependency("bunker starter",
            () => Object.FindFirstObjectByType<BunkerRunStarter>() != null, bunkerDeadline);
        yield return WaitForBuildOverviewDependency("bunker transition",
            () => !SceneTransitionOverlay.IsTransitioning, bunkerDeadline);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        // One shared 30-second readiness budget; splitting dependencies adds diagnostics, not time.
        float deadline = Time.realtimeSinceStartup + 30f;
        yield return WaitForBuildOverviewDependency("MVP scene",
            () => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP", deadline);
        yield return WaitForBuildOverviewDependency("composition",
            () => ProductionSceneComposition.Active is { IsReady: true }, deadline);
        yield return WaitForBuildOverviewDependency("CharacterSpawner.SpawnedPlayer",
            () => Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null, deadline);
        yield return WaitForBuildOverviewDependency("PlayerRuntimeReference.CachedPlayer",
            () => PlayerRuntimeReference.CachedPlayer != null, deadline);
        yield return WaitForBuildOverviewDependency("initialized station",
            () => Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true }, deadline);
        yield return WaitForBuildOverviewDependency("finished transition",
            () => !SceneTransitionOverlay.IsTransitioning, deadline);
        yield return WaitForBuildOverviewDependency("Camera.main",
            () => Camera.main != null && Camera.main.isActiveAndEnabled, deadline);
        yield return WaitForBuildOverviewDependency("RunStateManager", () => RunStateManager.Instance != null, deadline);
        yield return WaitForBuildOverviewDependency("UpgradeManager", () => UpgradeManager.Instance != null, deadline);
        yield return WaitForBuildOverviewDependency("idle reward queue",
            () => UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle, deadline);
    }

    private static IEnumerator WaitForBuildOverviewDependency(string dependency, System.Func<bool> ready, float deadline)
    {
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(ready(), Is.True, $"Build overview readiness failed: {dependency}. " +
            $"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}; " +
            $"composition={ProductionSceneComposition.Active?.IsReady}; " +
            $"spawnerPlayer={Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null}; " +
            $"cachedPlayer={PlayerRuntimeReference.CachedPlayer != null}; " +
            $"station={Object.FindFirstObjectByType<OrbitalStationRuntime>()?.IsInitialized}; " +
            $"transition={SceneTransitionOverlay.IsTransitioning}; camera={Camera.main != null}; " +
            $"upgradeManager={UpgradeManager.Instance != null}; queueIdle={UpgradeManager.Instance?.IsRewardQueueIdle}");
    }

    [Category("Core"), UnityTest]
    public IEnumerator MissReturnPistolWorldLineCollisionAndPoolReuse()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        yield return MissReturnPistolWorldLineBody();
    }

    private static IEnumerator MissReturnPistolWorldLineBody()
    {
        var root = new GameObject("Miss return production projectile contract");
        var enemies = new GameObject[2];
        ProductionOrbitalCombatAdapter adapter = null;
        try
        {
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            Assert.That(EnemyHealth.ActiveInstances.Count, Is.Zero);
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
                enemies[i].transform.position = new Vector3(30f, 30f + i * 3f);
                foreach (var movement in enemies[i].GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
                foreach (var body in enemies[i].GetComponentsInChildren<Rigidbody2D>())
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
            yield return null; // Authored Start must finish before health is prepared.
            var health = enemies.Select(e => e.GetComponent<EnemyHealth>()).ToArray();
            var hits = new int[2];
            for (int i = 0; i < health.Length; i++)
            {
                int index = i;
                health[i].SetRuntimeMaxHealth(1000f);
                health[i].HitFeedback += (_, _, _) => hits[index]++;
            }
            Physics2D.SyncTransforms();
            var authored = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol);
            string authoredBefore = JsonUtility.ToJson(authored);
            var settings = authored.CreateMissReturnPistolExperiment();
            adapter = new ProductionOrbitalCombatAdapter(root.transform, null);
            adapter.SpawnDirectionalWeaponProjectile(Vector2.zero, Vector2.right, settings, null, settings.Damage, 0);
            object shot = MissReturnProjectiles(adapter).Single(p => MissReturnField<bool>(p, "Active"));
            root.transform.position = new Vector3(20f, 17f);
            adapter.Tick(0f);
            Assert.That(MissReturnField<Vector2>(shot, "Position"), Is.EqualTo(Vector2.zero));
            Assert.That(MissReturnField<bool>(shot, "MissReturnPending"), Is.True, "Pause consumes no path.");
            adapter.Tick(10f);
            Assert.That(MissReturnField<Vector2>(shot, "Position"), Is.EqualTo(Vector2.right * 4f));
            Assert.That(MissReturnField<Vector2>(shot, "Direction"), Is.EqualTo(Vector2.left));
            Assert.That(MissReturnField<float>(shot, "RemainingRange"), Is.EqualTo(4f));
            Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(1), "Large Tick ends only the outward leg.");
            adapter.Tick(10f);
            Assert.That(MissReturnField<Vector2>(shot, "Position"), Is.EqualTo(Vector2.zero));
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero, "Exactly one return leg, fixed in world space.");

            adapter.SpawnDirectionalWeaponProjectile(Vector2.zero, Vector2.right, settings, null, settings.Damage, 0);
            Assert.That(MissReturnProjectiles(adapter).Single(p => MissReturnField<bool>(p, "Active")), Is.SameAs(shot));
            adapter.Tick(10f);
            var collider = enemies[0].GetComponent<Collider2D>();
            enemies[0].transform.position += (Vector3)(Vector2.right * 2f - (Vector2)collider.bounds.center);
            Physics2D.SyncTransforms();
            adapter.Tick(10f); // Fresh reverse sweep must find a body introduced after the turn.
            Assert.That(hits[0], Is.EqualTo(1));
            Assert.That(health[0].CurrentHealth, Is.EqualTo(1000f - settings.Damage).Within(.001f));
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero);
            adapter.Tick(10f);
            Assert.That(hits[0], Is.EqualTo(1));

            var secondCollider = enemies[1].GetComponent<Collider2D>();
            enemies[1].transform.position += (Vector3)(new Vector2(2f, 2f) - (Vector2)secondCollider.bounds.center);
            Physics2D.SyncTransforms();
            adapter.SpawnDirectionalWeaponProjectile(Vector2.zero, Vector2.right, settings, null, settings.Damage, 1);
            adapter.Tick(10f);
            Assert.That(hits[0], Is.EqualTo(2));
            Assert.That(MissReturnField<bool>(shot, "MissReturnPending"), Is.False, "Body/RIC cancels return.");
            Assert.That(MissReturnField<float>(shot, "MissReturnRange"), Is.Zero);
            Assert.That(MissReturnField<Vector2>(shot, "Direction").y, Is.GreaterThan(.5f));
            adapter.Tick(10f);
            Assert.That(hits[1], Is.EqualTo(1), "Ordinary RIC still reaches the second body.");
            Assert.That(health[1].CurrentHealth, Is.EqualTo(1000f - settings.Damage).Within(.001f));
            Assert.That(hits[0], Is.EqualTo(2), "RIC never damages the first body twice.");
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero);

            foreach (var enemy in enemies) enemy.transform.position += Vector3.up * 30f;
            Physics2D.SyncTransforms();
            adapter.SpawnDirectionalWeaponProjectile(Vector2.zero, Vector2.right, authored, null, authored.Damage, 0);
            Assert.That(MissReturnProjectiles(adapter).Single(p => MissReturnField<bool>(p, "Active")), Is.SameAs(shot));
            Assert.That(MissReturnField<bool>(shot, "MissReturnPending"), Is.False);
            Assert.That(MissReturnField<float>(shot, "MissReturnRange"), Is.Zero);
            adapter.Tick(4f / authored.ProjectileSpeed);
            Assert.That(MissReturnField<Vector2>(shot, "Direction"), Is.EqualTo(Vector2.right));
            Assert.That(MissReturnField<float>(shot, "RemainingRange"), Is.EqualTo(4f).Within(.001f));
            adapter.Tick(10f);
            Assert.That(MissReturnField<Vector2>(shot, "Position"), Is.EqualTo(Vector2.right * 8f));
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero);
            Assert.That(JsonUtility.ToJson(authored), Is.EqualTo(authoredBefore));
        }
        finally
        {
            adapter?.DebugClearProjectiles();
            adapter?.Teardown();
            foreach (var enemy in enemies) if (enemy != null) Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(root);
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator MissReturnPistolOptInPreservesRuntimeAndAirborneShots()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return MissReturnPistolOptInBody();
    }

    private static IEnumerator MissReturnPistolOptInBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var state = station.State;
        var gun = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.Kind == OrbitalModuleKind.Pistol);
        var authored = gun.Settings;
        string authoredBefore = JsonUtility.ToJson(authored);
        var cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        bool stationEnabled = station.enabled;
        float previousTimeScale = Time.timeScale;
        float previousCooldown = (float)cooldown.GetValue(gun);
        object[] ownedShots = new object[0];
        try
        {
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            Assert.That(OrbitalRunState.CreateDefault(1).MissReturnPistolExperiment, Is.False);
            Assert.That(state.MissReturnPistolExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
            Assert.That(authored.MissReturnExperiment, Is.False);
            Assert.That(authored.Range, Is.EqualTo(8f));
            Assert.That(station.InputOwner.BeginConstructor(), Is.True);
            station.enabled = false; // Address only this module synchronously; no frame is yielded below.
            int id = gun.StableModuleId;
            Assert.That(station.UpgradeModuleDamage(id), Is.True);
            string identity = JsonUtility.ToJson(state.FindModule(id));
            var mount = gun.CurrentMount;
            cooldown.SetValue(gun, .37f);
            int shots = gun.ShotsFired;
            Assert.That(station.DebugSetMissReturnPistolExperiment(true), Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(station.Modules.Single(m => m.StableModuleId == id), Is.SameAs(gun));
            Assert.That(gun.CurrentMount, Is.SameAs(mount));
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(.37f));
            Assert.That(gun.ShotsFired, Is.EqualTo(shots), "Opt-in must not fire.");
            Assert.That(gun.Settings.MissReturnExperiment, Is.True);
            Assert.That(gun.Settings.Range, Is.EqualTo(4f));
            Assert.That(JsonUtility.ToJson(state.FindModule(id)), Is.EqualTo(identity));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).MissReturnPistolExperiment, Is.True);
            Assert.That(state.TryGrantSegmentedWeapon(OrbitalModuleKind.Pistol), Is.True);
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Pistol, 1, 2, out string error), Is.True, error);
            var later = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.Kind == OrbitalModuleKind.Pistol && m.StableModuleId != id);
            Assert.That(later.Settings.MissReturnExperiment, Is.True, "Ordinary inventory installation captures opt-in.");
            string beforeRestore = JsonUtility.ToJson(station.CaptureState());
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(JsonUtility.ToJson(station.CaptureState()), Is.EqualTo(beforeRestore));
            adapter = (ProductionOrbitalCombatAdapter)station.Combat;
            gun = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.StableModuleId == id);
            previousCooldown = (float)cooldown.GetValue(gun);
            Assert.That(gun.Settings.MissReturnExperiment, Is.True);
            Assert.That(JsonUtility.ToJson(station.State.FindModule(id)), Is.EqualTo(identity));
            mount = gun.CurrentMount;
            var beforeShots = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active")).ToArray();
            cooldown.SetValue(gun, 0f);
            gun.ActivateCombat(); // The actual ordinary module path, with its damage/rate/link handling.
            ownedShots = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active") && !beforeShots.Contains(p)).ToArray();
            Assert.That(ownedShots.Length, Is.EqualTo(1));
            object airborne = ownedShots.Single();
            Assert.That(MissReturnField<bool>(airborne, "MissReturnPending"), Is.True);
            Vector2 origin = MissReturnField<Vector2>(airborne, "Position");
            Vector2 direction = MissReturnField<Vector2>(airborne, "Direction");
            float shotCooldown = (float)cooldown.GetValue(gun);
            Assert.That(shotCooldown, Is.GreaterThan(0f));
            shots = gun.ShotsFired;
            Assert.That(station.DebugSetMissReturnPistolExperiment(false), Is.True);
            Assert.That(gun.CurrentMount, Is.SameAs(mount));
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(shotCooldown));
            Assert.That(gun.ShotsFired, Is.EqualTo(shots));
            Assert.That(gun.Settings.MissReturnExperiment, Is.False);
            Assert.That(gun.Settings.Range, Is.EqualTo(8f));
            Assert.That(JsonUtility.ToJson(gun.Settings), Is.EqualTo(authoredBefore));
            Assert.That(station.Modules.OfType<OrbitalProjectileModule>().Where(m => m.Kind == OrbitalModuleKind.Pistol)
                .All(m => !m.Settings.MissReturnExperiment && m.Settings.Range == 8f), Is.True);
            Assert.That(MissReturnField<bool>(airborne, "MissReturnPending"), Is.True, "OFF affects future shots only.");
            adapter.Tick(4f / authored.ProjectileSpeed);
            Assert.That(MissReturnField<bool>(airborne, "Active"), Is.True);
            Assert.That(Vector2.Distance(MissReturnField<Vector2>(airborne, "Position"), origin + direction * 4f), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(MissReturnField<Vector2>(airborne, "Direction"), -direction), Is.LessThan(.001f));
            Assert.That(MissReturnField<float>(airborne, "RemainingRange"), Is.EqualTo(4f));
            Assert.That(JsonUtility.ToJson(station.State.FindModule(id)), Is.EqualTo(identity));
            Assert.That(JsonUtility.ToJson(authored), Is.EqualTo(authoredBefore));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(station.State)).MissReturnPistolExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "OFF cannot restore production eligibility.");
        }
        finally
        {
            // Release only projectiles emitted by this contract, through their real owner.
            var release = typeof(ProductionOrbitalCombatAdapter).GetMethod("Release",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (object shot in ownedShots) if (MissReturnField<bool>(shot, "Active")) release.Invoke(adapter, new[] { shot });
            if (station.State.MissReturnPistolExperiment) station.DebugSetMissReturnPistolExperiment(false);
            station.InputOwner.EndConstructor();
            cooldown.SetValue(gun, previousCooldown);
            station.enabled = stationEnabled;
            Time.timeScale = previousTimeScale;
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator MissReturnPistolProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return MissReturnPistolProductionCaptureBody();
    }

    private static IEnumerator MissReturnPistolProductionCaptureBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var gun = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.Kind == OrbitalModuleKind.Pistol);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        Assert.That(menu, Is.Not.Null);
        Assert.That(spawner, Is.Not.Null);
        Assert.That(health, Is.Not.Null);
        Assert.That(body, Is.Not.Null);
        bool experiment = station.State.MissReturnPistolExperiment;
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Call(string name, params object[] args) => typeof(Subject42DebugMenu).GetMethod(name, hidden).Invoke(menu, args);
        string folder = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/MissReturnPistol");
        string outboundPath = System.IO.Path.Combine(folder, "miss-return-outbound.png");
        string inboundPath = System.IO.Path.Combine(folder, "miss-return-inbound.png");
        bool Visible(object shot)
        {
            var visual = MissReturnField<GameObject>(shot, "GameObject");
            var camera = Camera.main;
            if (visual == null || !visual.activeInHierarchy || camera == null || !camera.isActiveAndEnabled) return false;
            var point = camera.WorldToViewportPoint(visual.transform.position);
            return point.z > 0f && point.x > .08f && point.x < .92f && point.y > .08f && point.y < .92f &&
                visual.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite != null && r.color.a > 0f);
        }
        void AssertLive(object shot)
        {
            AssertSweepingShotgunProductionReady(station);
            Assert.That(station.isActiveAndEnabled && station.InputOwner.IsIdle, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(body.simulated && !health.IsDead, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(MissReturnField<bool>(shot, "Active") && Visible(shot), Is.True,
                "Capture requires the same active visible production pistol projectile.");
        }
        try
        {
            Assert.That(experiment, Is.False, "Fresh MVP must default to the ordinary pistol.");
            Assert.That(gun.Settings.MissReturnExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Call("OpenMenu");
            var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", System.Reflection.BindingFlags.NonPublic);
            Call("SelectTab", System.Enum.ToObject(tabType, 0), true);
            var toggle = ((GameObject)typeof(Subject42DebugMenu).GetField("menuRoot", hidden).GetValue(menu))
                .GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy &&
                    b.GetComponentInChildren<TMPro.TMP_Text>().text == "ВКЛЮЧИТЬ" &&
                    b.transform.parent.name.StartsWith("Эксперимент: возврат промаха", System.StringComparison.Ordinal));
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(station.State.MissReturnPistolExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(gun.Settings.MissReturnExperiment && gun.Settings.Range == 4f, Is.True);
            Call("CloseMenu");
            System.IO.Directory.CreateDirectory(folder);
            int shotsBefore = gun.ShotsFired;
            object tracked = null;
            Vector2 start = default, direction = default, previous = default, turn = default;
            float outwardRemaining = 0f;
            bool outwardRequested = false, returning = false, captured = false;
            System.DateTime outboundRequested = default, inboundRequested = default;
            float deadline = Time.realtimeSinceStartup + 12f;
            int interrupted = 0;
            while (!captured && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                if (tracked != null && !MissReturnField<bool>(tracked, "Active"))
                {
                    // A real body/shield may consume a shot. Wait for another natural miss.
                    interrupted++;
                    tracked = null;
                    outwardRequested = returning = false;
                }
                if (tracked == null)
                {
                    tracked = MissReturnProjectiles(adapter).FirstOrDefault(p => MissReturnField<bool>(p, "Active") &&
                        MissReturnField<OrbitalModuleKind>(p, "Kind") == OrbitalModuleKind.Pistol &&
                        MissReturnField<bool>(p, "MissReturnPending") && MissReturnField<float>(p, "RemainingRange") > 3f && Visible(p));
                    if (tracked == null) continue;
                    start = previous = MissReturnField<Vector2>(tracked, "Position");
                    direction = MissReturnField<Vector2>(tracked, "Direction");
                    outwardRemaining = MissReturnField<float>(tracked, "RemainingRange");
                    continue;
                }
                Vector2 position = MissReturnField<Vector2>(tracked, "Position");
                Vector2 currentDirection = MissReturnField<Vector2>(tracked, "Direction");
                Assert.That(Mathf.Abs(Vector2.Dot(position - start, new Vector2(-direction.y, direction.x))),
                    Is.LessThan(.02f), "Natural return stays on its original world line.");
                if (MissReturnField<bool>(tracked, "MissReturnPending"))
                {
                    if (!outwardRequested && Vector2.Dot(position - previous, direction) > .01f && Visible(tracked))
                    {
                        AssertLive(tracked);
                        outboundRequested = System.DateTime.UtcNow;
                        ScreenCapture.CaptureScreenshot(outboundPath);
                        outwardRequested = true;
                    }
                }
                else if (Vector2.Dot(currentDirection, direction) < -.999f && outwardRequested)
                {
                    if (!returning)
                    {
                        returning = true;
                        turn = position;
                        Assert.That(Vector2.Dot(turn - start, direction), Is.EqualTo(outwardRemaining).Within(.02f),
                            "The natural outward leg must exhaust its captured range before turning.");
                        Assert.That(((IEnumerable)tracked.GetType().GetField("HitBodies").GetValue(tracked)).Cast<object>(),
                            Is.Empty, "Only a complete miss may turn back.");
                    }
                    else if (Vector2.Dot(position - turn, direction) < -.01f && Visible(tracked))
                    {
                        AssertLive(tracked);
                        inboundRequested = System.DateTime.UtcNow;
                        ScreenCapture.CaptureScreenshot(inboundPath);
                        captured = true;
                    }
                }
                else
                {
                    interrupted++;
                    tracked = null; // Body/RIC cancelled the return; do not alter collision or enemies.
                    outwardRequested = returning = false;
                }
                previous = position;
            }
            Assert.That(captured, Is.True, "No visible natural full miss/return within 12 s; interrupted shots=" + interrupted);
            Assert.That(gun.ShotsFired, Is.GreaterThan(shotsBefore), "Live station Update must emit the observed pistol shot.");
            // Disk waits happen only after BOTH short phases have been observed and requested.
            yield return CoreTestSupport.Await(() => System.IO.File.Exists(outboundPath) &&
                System.IO.File.GetLastWriteTimeUtc(outboundPath) >= outboundRequested && new System.IO.FileInfo(outboundPath).Length > 0 &&
                System.IO.File.Exists(inboundPath) && System.IO.File.GetLastWriteTimeUtc(inboundPath) >= inboundRequested &&
                new System.IO.FileInfo(inboundPath).Length > 0);
            Debug.Log("Miss return pistol production Game View: " + outboundPath + " ; " + inboundPath);
        }
        finally
        {
            Call("CloseMenu");
            station.DebugSetMissReturnPistolExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    private static object[] MissReturnProjectiles(ProductionOrbitalCombatAdapter adapter) =>
        ((IEnumerable)typeof(ProductionOrbitalCombatAdapter).GetField("projectiles",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(adapter)).Cast<object>().ToArray();

    private static T MissReturnField<T>(object shot, string name) => (T)shot.GetType().GetField(name).GetValue(shot);

    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        // Keep ExitPlayMode visible to the runner across the domain transition.
        IEnumerator cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
    }

    [Category("Core"), UnityTest]
    public IEnumerator PulsedSawPhaseControlsContactAndPreservesCooldown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return PulsedSawPhaseBody();
    }

    private static IEnumerator PulsedSawPhaseBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var saw = InstallPulsedSaw(station, 2);
        Assert.That(station.DebugSetPulsedSawExperiment(true), Is.True);
        var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(saw);
        var cooldowns = (System.Collections.Generic.Dictionary<EnemyHealth, float>)typeof(OrbitalSawModule)
            .GetField("nextHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(saw);
        var reset = typeof(OrbitalSawModule).GetMethod("ResetPulsePhase",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        GameObject first = null, second = null, shielded = null;
        try
        {
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            first = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
            second = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
            shielded = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"));
            foreach (var target in new[] { first, second, shielded })
            {
                target.transform.position = (Vector3)saw.WorldPosition + Vector3.right * 30f;
                foreach (var movement in target.GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
                foreach (var body in target.GetComponentsInChildren<Rigidbody2D>())
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
            yield return null; // Finish authored EnemyHealth.Start before setting HP.
            var firstHealth = first.GetComponent<EnemyHealth>();
            var secondHealth = second.GetComponent<EnemyHealth>();
            firstHealth.SetRuntimeMaxHealth(10000f);
            secondHealth.SetRuntimeMaxHealth(10000f);
            var collider = first.GetComponent<Collider2D>();
            Assert.That(collider, Is.Not.Null);
            float authored = saw.Settings.ContactRadius * station.State.ContactSizeMultiplier;
            Assert.That(saw.ContactRadius, Is.EqualTo(authored * 1.35f).Within(.0001f));
            Assert.That(visual.transform.localScale.x, Is.EqualTo(saw.ContactRadius / .65f).Within(.0001f));
            // Put the nearest body surface exclusively inside the extended contact band.
            first.transform.position += (Vector3)(saw.WorldPosition + Vector2.right *
                (authored + .05f + collider.bounds.extents.x) - (Vector2)collider.bounds.center);
            Physics2D.SyncTransforms();
            float distance = Vector2.Distance(collider.ClosestPoint(saw.WorldPosition), saw.WorldPosition);
            Assert.That(distance, Is.GreaterThan(authored).And.LessThan(saw.ContactRadius));
            int hits = saw.BodyHits;
            saw.ActivateCombat();
            Assert.That(saw.BodyHits, Is.EqualTo(hits + 1));
            Assert.That(firstHealth.CurrentHealth, Is.LessThan(10000f));
            float until = cooldowns[firstHealth];
            for (int i = 0; i < 8; i++) { saw.ActivateCombat(); saw.Tick(0f); }
            Assert.That(saw.IsPulseContactActive, Is.True);
            Assert.That(saw.BodyHits, Is.EqualTo(hits + 1), "Repeated activation respects individual cooldown.");
            reset.Invoke(saw, null);
            Assert.That(cooldowns[firstHealth], Is.EqualTo(until));
            Assert.That(station.DebugSetPulsedSawExperiment(false), Is.True);
            Assert.That(station.DebugSetPulsedSawExperiment(true), Is.True);
            Assert.That(cooldowns[firstHealth], Is.EqualTo(until), "Toggle must not ready an already hit body.");
            saw.ActivateCombat();
            Assert.That(saw.BodyHits, Is.EqualTo(hits + 1));
            saw.Tick(.799f);
            Assert.That(saw.IsPulseContactActive, Is.True, "Contact remains active until the .8 second boundary.");

            first.transform.position += Vector3.right * 30f;
            var secondCollider = second.GetComponent<Collider2D>();
            second.transform.position += (Vector3)(saw.WorldPosition - (Vector2)secondCollider.bounds.center);
            Physics2D.SyncTransforms();
            saw.Tick(.002f);
            Assert.That(saw.IsPulseContactActive, Is.False);
            Assert.That(saw.ContactRadius, Is.EqualTo(authored).Within(.0001f));
            Assert.That(visual.transform.localScale.x, Is.EqualTo(authored / .65f).Within(.0001f));
            Quaternion recoveryRotation = visual.transform.localRotation;
            saw.Tick(.2f);
            for (int i = 0; i < 8; i++) { saw.ActivateCombat(); saw.Tick(0f); }
            Assert.That(saw.IsPulseContactActive, Is.False);
            Assert.That(Quaternion.Angle(recoveryRotation, visual.transform.localRotation), Is.LessThan(.001f));
            Assert.That(secondHealth.CurrentHealth, Is.EqualTo(10000f), "Recovery must reject actual overlapping bodies.");
            saw.Tick(.201f);
            Assert.That(saw.IsPulseContactActive, Is.True);
            Assert.That(secondHealth.CurrentHealth, Is.LessThan(10000f), "Next active window restores contact.");
            Assert.That(Quaternion.Angle(recoveryRotation, visual.transform.localRotation), Is.GreaterThan(.01f));

            second.transform.position += Vector3.right * 30f;
            var shield = shielded.GetComponent<EnemyDirectionalShield>();
            var shieldHealth = shielded.GetComponent<EnemyHealth>();
            shieldHealth.SetRuntimeMaxHealth(10000f);
            shielded.transform.position += (Vector3)(saw.WorldPosition - Vector2.right * shield.ShieldDistance - shield.Center);
            shield.Bind(visual.transform);
            Physics2D.SyncTransforms();
            int blocks = shield.Blocks;
            saw.ActivateCombat();
            Assert.That(shield.Blocks, Is.EqualTo(blocks + 1), "Active pulse retains BlockDiskContact.");
            Assert.That(shieldHealth.CurrentHealth, Is.EqualTo(10000f));
        }
        finally
        {
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            if (shielded != null) Object.DestroyImmediate(shielded);
            station.DebugSetPulsedSawExperiment(false);
            station.InputOwner.EndConstructor();
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator PulsedSawOptInPreservesIdentityAndRestoresContinuousContact()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return PulsedSawOptInBody();
    }

    private static IEnumerator PulsedSawOptInBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var state = station.State;
        Assert.That(OrbitalRunState.CreateDefault(1).PulsedSawExperiment, Is.False);
        Assert.That(state.PulsedSawExperiment, Is.False);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        var saw = InstallPulsedSaw(station, 2);
        var settings = saw.Settings;
        string authored = JsonUtility.ToJson(settings);
        Assert.That(state.Validate(out string upgradeError), Is.True, upgradeError);
        float previousContactSize = state.ContactSizeMultiplier;
        Assert.That(state.TryUpgradeCategory(OrbitalRewardKind.ContactSize), Is.True,
            "ContactSize reward must be accepted for the valid production state.");
        float upgradedContactSize = previousContactSize * 1.15f;
        Assert.That(state.ContactSizeMultiplier, Is.EqualTo(upgradedContactSize).Within(.0001f));
        int id = saw.StableModuleId;
        string identity = JsonUtility.ToJson(state.FindModule(id));
        GameObject target = null;
        try
        {
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
            target.transform.position = (Vector3)saw.WorldPosition + Vector3.right * 30f;
            foreach (var movement in target.GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
            foreach (var body in target.GetComponentsInChildren<Rigidbody2D>())
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            yield return null;
            var health = target.GetComponent<EnemyHealth>();
            health.SetRuntimeMaxHealth(10000f);
            Assert.That(station.DebugSetPulsedSawExperiment(true), Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(station.Modules.Single(m => m.StableModuleId == id), Is.SameAs(saw));
            Assert.That(saw.Settings, Is.SameAs(settings));
            var later = InstallPulsedSaw(station, 4);
            Assert.That(later.PulsedSawExperiment, Is.True);
            Assert.That(later.ContactRadius, Is.EqualTo(later.Settings.ContactRadius * state.ContactSizeMultiplier * 1.35f).Within(.0001f));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).PulsedSawExperiment, Is.True);
            var roundTripped = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
            Assert.That(roundTripped.ContactSizeMultiplier, Is.EqualTo(upgradedContactSize).Within(.0001f));
            var captured = station.CaptureState();
            Assert.That(captured.ContactSizeMultiplier, Is.EqualTo(upgradedContactSize).Within(.0001f));
            string beforeRestore = JsonUtility.ToJson(captured);
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(station.CaptureState(), Is.SameAs(captured));
            Assert.That(JsonUtility.ToJson(station.State), Is.EqualTo(beforeRestore));
            saw = station.Modules.OfType<OrbitalSawModule>().Single(m => m.StableModuleId == id);
            Assert.That(saw.PulsedSawExperiment, Is.True);
            Assert.That(station.State.ContactSizeMultiplier, Is.EqualTo(upgradedContactSize).Within(.0001f));
            Assert.That(saw.ContactRadius, Is.EqualTo(saw.Settings.ContactRadius * upgradedContactSize * 1.35f).Within(.0001f));
            Assert.That(JsonUtility.ToJson(state.FindModule(id)), Is.EqualTo(identity));
            saw.Tick(.81f);
            Assert.That(saw.IsPulseContactActive, Is.False);
            var mount = saw.CurrentMount;
            saw.Detach();
            saw.Attach(mount);
            Assert.That(saw.IsPulseContactActive, Is.True, "Reattachment starts a fresh active window.");
            saw.Tick(.81f);
            Assert.That(saw.IsPulseContactActive, Is.False);
            Assert.That(station.DebugSetPulsedSawExperiment(false), Is.True);
            Assert.That(station.State.ContactSizeMultiplier, Is.EqualTo(upgradedContactSize).Within(.0001f));
            Assert.That(saw.ContactRadius, Is.EqualTo(saw.Settings.ContactRadius * state.ContactSizeMultiplier).Within(.0001f));
            var collider = target.GetComponent<Collider2D>();
            target.transform.position += (Vector3)(saw.WorldPosition - (Vector2)collider.bounds.center);
            Physics2D.SyncTransforms();
            saw.Tick(.81f);
            Assert.That(health.CurrentHealth, Is.LessThan(10000f), "OFF restores actual contact at the former recovery time.");
            foreach (float delta in new[] { 0f, .81f, .4f, 1.21f })
            {
                saw.Tick(delta);
                Assert.That(saw.IsPulseContactActive, Is.True, "OFF has no recovery window.");
            }
            Assert.That(station.Modules.OfType<OrbitalSawModule>().All(m => !m.PulsedSawExperiment), Is.True);
            Assert.That(JsonUtility.ToJson(settings), Is.EqualTo(authored));
            Assert.That(JsonUtility.ToJson(state.FindModule(id)), Is.EqualTo(identity));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).PulsedSawExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            if (target != null) Object.DestroyImmediate(target);
            station.DebugSetPulsedSawExperiment(false);
            station.InputOwner.EndConstructor();
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator InertialSawPhaseGatesContactAndPreservesIntervals()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return InertialSawPhaseBody();
    }

    private static IEnumerator InertialSawPhaseBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool inertial = station.State.InertialSawExperiment, pulsed = station.State.PulsedSawExperiment;
        var saw = InstallPulsedSaw(station, 2);
        var mount = saw.CurrentMount;
        float phase = mount.Ring.Phase;
        Vector3 ownerPosition = station.Owner.Transform.position;
        var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(saw);
        var intervals = (System.Collections.Generic.Dictionary<EnemyHealth, float>)typeof(OrbitalSawModule)
            .GetField("nextHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(saw);
        GameObject target = null, second = null;
        try
        {
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            if (pulsed) Assert.That(station.DebugSetPulsedSawExperiment(false), Is.True);
            Assert.That(station.DebugSetInertialSawExperiment(true), Is.True);
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
            target.transform.position = (Vector3)saw.WorldPosition + Vector3.right * 30f;
            foreach (var movement in target.GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
            foreach (var body in target.GetComponentsInChildren<Rigidbody2D>())
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            second = Object.Instantiate(target);
            second.transform.position += Vector3.up * 10f;
            yield return null; // Authored EnemyHealth.Start, while the constructor pauses the run.
            var health = target.GetComponent<EnemyHealth>();
            health.SetRuntimeMaxHealth(10000f);
            var secondHealth = second.GetComponent<EnemyHealth>();
            secondHealth.SetRuntimeMaxHealth(10000f);
            var collider = target.GetComponent<Collider2D>();
            target.transform.position += (Vector3)(saw.WorldPosition - (Vector2)collider.bounds.center);
            Physics2D.SyncTransforms();
            float radius = saw.Settings.ContactRadius * station.State.ContactSizeMultiplier;
            Quaternion stopped = visual.transform.localRotation;
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.False, "First positive tick only samples phase.");
            saw.Tick(.01f);
            Assert.That(health.CurrentHealth, Is.EqualTo(10000f));
            Assert.That(Quaternion.Angle(stopped, visual.transform.localRotation), Is.LessThan(.001f));

            station.Owner.Transform.position += Vector3.right * .1f;
            target.transform.position += (Vector3)(saw.WorldPosition - (Vector2)collider.bounds.center);
            Physics2D.SyncTransforms();
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.False, "Player translation is not ring rotation.");
            Assert.That(health.CurrentHealth, Is.EqualTo(10000f));

            station.State.SetPhase(mount.Ring.RingId, phase + 1f);
            mount.Ring.Tick(0f);
            target.transform.position += (Vector3)(saw.WorldPosition - (Vector2)collider.bounds.center);
            Physics2D.SyncTransforms();
            saw.Tick(0f);
            saw.Tick(-.1f);
            Assert.That(health.CurrentHealth, Is.EqualTo(10000f));
            Assert.That(saw.IsInertialContactActive, Is.False);
            float expectedDamage = saw.Settings.Damage * mount.Ring.PowerMultiplier *
                station.GetModuleMetaDamageMultiplier(saw.Kind) * station.GetModuleDamageMultiplier(saw.StableModuleId) *
                station.State.ContactDamageMultiplier * 1.5f;
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.True, "Nonpositive ticks must not consume the changed phase.");
            Assert.That(saw.ContactRadius, Is.EqualTo(radius).Within(.0001f));
            Assert.That(10000f - health.CurrentHealth, Is.EqualTo(expectedDamage).Within(.01f));
            Assert.That(Quaternion.Angle(stopped, visual.transform.localRotation), Is.GreaterThan(.01f));
            float until = intervals[health];
            int hits = saw.BodyHits;
            Quaternion rotating = visual.transform.localRotation;
            var secondCollider = second.GetComponent<Collider2D>();
            second.transform.position += (Vector3)(saw.WorldPosition - (Vector2)secondCollider.bounds.center);
            Physics2D.SyncTransforms();
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.False);
            Assert.That(secondHealth.CurrentHealth, Is.EqualTo(10000f), "Stopped contact rejects a fresh overlapping body.");
            Assert.That(Quaternion.Angle(rotating, visual.transform.localRotation), Is.LessThan(.001f));
            second.transform.position += Vector3.up * 10f;
            station.State.SetPhase(mount.Ring.RingId, phase + 2f);
            mount.Ring.Tick(0f);
            Physics2D.SyncTransforms();
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.True);
            Assert.That(saw.BodyHits, Is.EqualTo(hits), "Renewed rotation cannot bypass nextHit.");
            Assert.That(station.DebugSetInertialSawExperiment(false), Is.True);
            Assert.That(saw.IsInertialContactActive, Is.True, "OFF restores ordinary contact immediately.");
            second.transform.position += (Vector3)(saw.WorldPosition - (Vector2)secondCollider.bounds.center);
            Physics2D.SyncTransforms();
            saw.ActivateCombat();
            Assert.That(intervals[health], Is.EqualTo(until));
            Assert.That(saw.BodyHits, Is.EqualTo(hits + 1));
            Assert.That(10000f - secondHealth.CurrentHealth, Is.EqualTo(expectedDamage / 1.5f).Within(.01f),
                "OFF restores actual ordinary damage against a fresh body without phase movement.");
            hits = saw.BodyHits;
            Assert.That(station.DebugSetInertialSawExperiment(true), Is.True);
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.False);
            station.State.SetPhase(mount.Ring.RingId, phase + 3f);
            mount.Ring.Tick(0f);
            Physics2D.SyncTransforms();
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.True);
            Assert.That(intervals[health], Is.EqualTo(until));
            Assert.That(saw.BodyHits, Is.EqualTo(hits), "OFF/ON must preserve the actual hit interval.");
            target.transform.position += Vector3.right * 30f;
            station.State.SetPhase(mount.Ring.RingId, phase + 4f);
            saw.Tick(0f);
            saw.Tick(-.1f);
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.True, "Nonpositive ticks preserve an existing phase sample too.");
        }
        finally
        {
            if (target != null) Object.DestroyImmediate(target);
            if (second != null) Object.DestroyImmediate(second);
            station.Owner.Transform.position = ownerPosition;
            station.State.SetPhase(mount.Ring.RingId, phase);
            mount.Ring.Tick(0f);
            station.DebugSetInertialSawExperiment(false);
            station.DebugSetPulsedSawExperiment(pulsed);
            station.DebugSetInertialSawExperiment(inertial);
            station.InputOwner.EndConstructor();
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator InertialSawOptInPreservesStateAndExcludesPulse()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return InertialSawOptInBody();
    }

    private static IEnumerator InertialSawOptInBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var state = station.State;
        bool inertial = state.InertialSawExperiment, pulsed = state.PulsedSawExperiment;
        Assert.That(OrbitalRunState.CreateDefault(1).InertialSawExperiment, Is.False);
        Assert.That(inertial, Is.False);
        Assert.That(pulsed, Is.False);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        var saw = InstallPulsedSaw(station, 2);
        var settings = saw.Settings;
        string authored = JsonUtility.ToJson(settings);
        Assert.That(state.UpgradeModuleDamage(saw.StableModuleId), Is.True);
        Assert.That(state.TryUpgradeCategory(OrbitalRewardKind.ContactSize), Is.True);
        int id = saw.StableModuleId;
        string identity = JsonUtility.ToJson(state.FindModule(id));
        var mount = saw.CurrentMount;
        var cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        cooldown.SetValue(saw, .37f);
        try
        {
            Assert.That(station.DebugSetInertialSawExperiment(true), Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(station.DebugSetPulsedSawExperiment(true), Is.False);
            Assert.That(state.InertialSawExperiment, Is.True);
            Assert.That(state.PulsedSawExperiment, Is.False);
            Assert.That(station.Modules.Single(m => m.StableModuleId == id), Is.SameAs(saw));
            Assert.That(saw.Settings, Is.SameAs(settings));
            Assert.That(saw.CurrentMount, Is.SameAs(mount));
            Assert.That((float)cooldown.GetValue(saw), Is.EqualTo(.37f));
            Assert.That(JsonUtility.ToJson(state.FindModule(id)), Is.EqualTo(identity));
            Assert.That(station.DebugSetInertialSawExperiment(false), Is.True);
            Assert.That(station.DebugSetPulsedSawExperiment(true), Is.True);
            Assert.That(station.DebugSetInertialSawExperiment(true), Is.False);
            Assert.That(state.PulsedSawExperiment, Is.True);
            Assert.That(state.InertialSawExperiment, Is.False);
            Assert.That((float)cooldown.GetValue(saw), Is.EqualTo(.37f));
            Assert.That(station.DebugSetPulsedSawExperiment(false), Is.True);
            Assert.That(station.DebugSetInertialSawExperiment(true), Is.True);
            var later = InstallPulsedSaw(station, 4);
            Assert.That(later.InertialSawExperiment, Is.True);
            later.Tick(.01f);
            Assert.That(later.IsInertialContactActive, Is.False, "Later installation needs its own phase sample.");
            var roundTrip = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
            Assert.That(roundTrip.InertialSawExperiment, Is.True);
            Assert.That(roundTrip.PulsedSawExperiment, Is.False);
            Assert.That(roundTrip.ContactSizeMultiplier, Is.EqualTo(state.ContactSizeMultiplier));
            Assert.That(JsonUtility.ToJson(roundTrip.FindModule(id)), Is.EqualTo(identity));
            saw.Tick(.01f);
            state.SetPhase(mount.Ring.RingId, mount.Ring.Phase + 1f);
            mount.Ring.Tick(0f);
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.True, "Restore must discard a previously active phase sample.");
            var captured = station.CaptureState();
            string beforeRestore = JsonUtility.ToJson(captured);
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(station.CaptureState(), Is.SameAs(captured));
            Assert.That(JsonUtility.ToJson(station.State), Is.EqualTo(beforeRestore));
            saw = station.Modules.OfType<OrbitalSawModule>().Single(m => m.StableModuleId == id);
            Assert.That(saw.InertialSawExperiment, Is.True);
            Assert.That(JsonUtility.ToJson(saw.Settings), Is.EqualTo(authored));
            Assert.That(JsonUtility.ToJson(station.State.FindModule(id)), Is.EqualTo(identity));
            Assert.That(saw.CurrentMount.MountIndex, Is.EqualTo(mount.MountIndex));
            Assert.That(saw.CurrentMount.Ring.RingId, Is.EqualTo(mount.Ring.RingId));
            station.State.SetPhase(saw.CurrentMount.Ring.RingId, saw.CurrentMount.Ring.Phase + 1f);
            saw.CurrentMount.Ring.Tick(0f);
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.False, "Restore starts with a fresh phase sample.");
            mount = saw.CurrentMount;
            station.State.SetPhase(mount.Ring.RingId, mount.Ring.Phase + 1f);
            mount.Ring.Tick(0f);
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.True);
            saw.Detach();
            saw.Attach(mount);
            station.State.SetPhase(mount.Ring.RingId, mount.Ring.Phase + 1f);
            mount.Ring.Tick(0f);
            saw.Tick(.01f);
            Assert.That(saw.IsInertialContactActive, Is.False, "Reattachment must not inherit phase activity.");
            Assert.That(station.DebugSetInertialSawExperiment(false), Is.True);
            Assert.That(saw.IsInertialContactActive, Is.True);
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(station.State)).InertialSawExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "OFF does not restore production eligibility.");
        }
        finally
        {
            station.DebugSetInertialSawExperiment(false);
            station.DebugSetPulsedSawExperiment(pulsed);
            station.DebugSetInertialSawExperiment(inertial);
            station.InputOwner.EndConstructor();
        }
    }

    private static OrbitalSawModule InstallPulsedSaw(OrbitalStationRuntime station, int position)
    {
        AssertSweepingShotgunProductionReady(station);
        Assert.That(station.State.TryGrantSegmentedWeapon(OrbitalModuleKind.Saw), Is.True);
        if (!station.InputOwner.IsConstructing) Assert.That(station.InputOwner.BeginConstructor(), Is.True);
        Assert.That(station.ConstructWeapon(OrbitalModuleKind.Saw, 1, position, out string error), Is.True, error);
        return station.Modules.OfType<OrbitalSawModule>().Single(m => m.CurrentMount != null && m.CurrentMount.MountIndex == position);
    }

    [Category("Core"), UnityTest]
    public IEnumerator SweepingShotgunSequenceTracksCurrentRadialAndExtraShot()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var gun = InstallSweepingShotgun(station, 2);
        Assert.That(station.DebugSetSweepingShotgunExperiment(true), Is.True);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var mount = gun.CurrentMount;
        Vector3 originalPosition = mount.Transform.position;
        try
        {
            mount.Transform.position = station.transform.position + Vector3.right * 1.75f;
            adapter.DebugClearProjectiles();
            ShotgunClock.SetValue(gun, 0f);
            gun.Tick(.001f);
            AssertShotgunPair(adapter, Vector2.right, -18f);
            Assert.That(ShotgunClock.GetValue(gun), Is.EqualTo(.12f / gun.FireRateMultiplier).Within(.0001f));
            float clock = (float)ShotgunClock.GetValue(gun);

            // A real 2X volley between ordinary volleys must not advance their sequence/clock.
            mount.Transform.position = station.transform.position + Vector3.up * 1.75f;
            adapter.DebugClearProjectiles();
            Assert.That(gun.FireExtraShot(), Is.True);
            AssertShotgunPair(adapter, Vector2.up, 0f);
            Assert.That((float)ShotgunClock.GetValue(gun), Is.EqualTo(clock));
            adapter.DebugClearProjectiles();
            gun.Tick(clock * .5f);
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero, "2X must not ready the ordinary volley early.");
            gun.Tick(clock * .5f + .001f);
            AssertShotgunPair(adapter, Vector2.up, 0f);

            mount.Transform.position = station.transform.position + Vector3.left * 1.75f;
            adapter.DebugClearProjectiles();
            gun.Tick((float)ShotgunClock.GetValue(gun) + .001f);
            AssertShotgunPair(adapter, Vector2.left, 18f);
            Assert.That(gun.ShotsFired, Is.EqualTo(4), "Three ordinary volleys and one independent extra volley.");
            Assert.That((float)ShotgunClock.GetValue(gun),
                Is.EqualTo(gun.Settings.FireInterval / gun.FireRateMultiplier).Within(.0001f));
        }
        finally
        {
            mount.Transform.position = originalPosition;
            adapter.DebugClearProjectiles();
            station.InputOwner.EndConstructor();
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator SweepingShotgunOptInRestoresProfileAndCancelsPendingBurst()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var state = station.State;
        Assert.That(state.SweepingShotgunExperiment, Is.False);
        Assert.That(OrbitalRunState.CreateDefault(1).SweepingShotgunExperiment, Is.False);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        var gun = InstallSweepingShotgun(station, 2);
        Assert.That(gun, Is.Not.Null, "Constructor must return a live Shotgun runtime.");
        var authored = gun.Settings;
        Assert.That(authored, Is.Not.Null, "Installed Shotgun must retain its authored profile before opt-in.");
        string authoredBefore = JsonUtility.ToJson(authored);
        Assert.That(authored.Pellets, Is.EqualTo(7));
        Assert.That(state.UpgradeModuleDamage(gun.StableModuleId), Is.True);
        string identity = JsonUtility.ToJson(state.FindModule(gun.StableModuleId));
        var mount = gun.CurrentMount;
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        try
        {
            Assert.That(station.DebugSetSweepingShotgunExperiment(true), Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(station.Modules.Single(m => m.StableModuleId == gun.StableModuleId), Is.SameAs(gun));
            Assert.That(gun.CurrentMount, Is.SameAs(mount));
            Assert.That(gun.Settings, Is.Not.SameAs(authored));
            Assert.That(gun.Settings.BurstShots, Is.EqualTo(3));
            Assert.That(gun.Settings.Pellets, Is.EqualTo(2));
            Assert.That(gun.Settings.Damage, Is.EqualTo(authored.Damage));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).SweepingShotgunExperiment, Is.True);
            var later = InstallSweepingShotgun(station, 4);
            Assert.That(later.Settings.BurstShots, Is.EqualTo(3));
            Assert.That(later.Settings.Pellets, Is.EqualTo(2));

            ShotgunClock.SetValue(gun, 0f);
            gun.Tick(.001f); // Leave two ordinary volleys pending.
            Assert.That(station.DebugSetSweepingShotgunExperiment(false), Is.True);
            Assert.That(gun.Settings, Is.SameAs(authored));
            Assert.That(later.Settings.BurstShots, Is.EqualTo(authored.BurstShots));
            Assert.That(later.Settings.Pellets, Is.EqualTo(authored.Pellets));
            adapter.DebugClearProjectiles();
            gun.Tick(.13f);
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero, "Disabling cancels the pending .12 second volley.");
            gun.Tick(.08f);
            Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(7), "Next shot uses the authored instantaneous fan.");

            Assert.That(station.DebugSetSweepingShotgunExperiment(true), Is.True);
            ShotgunClock.SetValue(gun, 0f);
            gun.Tick(.001f);
            int beforeDetach = gun.ShotsFired;
            gun.Detach();
            adapter.DebugClearProjectiles();
            gun.Tick(1f);
            Assert.That(gun.ShotsFired, Is.EqualTo(beforeDetach));
            Assert.That(gun.FireExtraShot(), Is.False);
            gun.Attach(mount);
            gun.Tick(.13f);
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero, "Reattachment does not resume the old burst.");
            gun.Tick(.08f);
            Vector2 radial = ((Vector2)mount.Transform.position - (Vector2)station.transform.position).normalized;
            AssertShotgunPair(adapter, radial, -18f);

            // Also exercise the constructor's real removal/teardown path during a burst.
            Assert.That(state.UpgradeModuleDamage(later.StableModuleId), Is.True);
            int laterDamageLevel = state.FindModule(later.StableModuleId).DamageLevel;
            ShotgunClock.SetValue(later, 0f);
            later.Tick(.001f);
            int laterId = later.StableModuleId;
            Assert.That(station.StowWeapon(laterId), Is.True);
            Assert.That(station.Modules.Any(m => m.StableModuleId == laterId), Is.False);
            var stored = state.SegmentedWeapons.Single(m => m.StableModuleId == laterId);
            Assert.That(stored.ModuleType, Is.EqualTo(OrbitalModuleKind.Shotgun));
            Assert.That(stored.DamageLevel, Is.EqualTo(laterDamageLevel));
            Assert.That(later.FireExtraShot(), Is.False, "Removed runtime cannot emit a pending or extra volley.");
            Assert.That(station.DebugSetSweepingShotgunExperiment(false), Is.True);
            Assert.That(gun.Settings, Is.SameAs(authored));
            Assert.That(JsonUtility.ToJson(authored), Is.EqualTo(authoredBefore));
            Assert.That(JsonUtility.ToJson(state.FindModule(gun.StableModuleId)), Is.EqualTo(identity));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).SweepingShotgunExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Opt-out cannot restore production progression.");
        }
        finally
        {
            if (gun.CurrentMount == null) gun.Attach(mount);
            adapter.DebugClearProjectiles();
            station.InputOwner.EndConstructor();
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator InertialSawProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return InertialSawProductionCaptureBody();
    }

    private static IEnumerator InertialSawProductionCaptureBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null, "Production player must expose PlayerHealth before capture setup.");
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        bool experiment = station.State.InertialSawExperiment;
        bool pulsed = station.State.PulsedSawExperiment;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Call(string name, params object[] args) => typeof(Subject42DebugMenu).GetMethod(name, hidden).Invoke(menu, args);
        var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", System.Reflection.BindingFlags.NonPublic);
        UnityEngine.UI.Button Button(string label, string rowPrefix) =>
            ((GameObject)typeof(Subject42DebugMenu).GetField("menuRoot", hidden).GetValue(menu))
            .GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMPro.TMP_Text>().text == label &&
                b.transform.parent.name.StartsWith(rowPrefix, System.StringComparison.Ordinal));
        try
        {
            Assert.That(experiment, Is.False, "Fresh run must default to ordinary Saw.");
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Call("OpenMenu");
            Call("SelectTab", System.Enum.ToObject(tabType, 0), true);
            Assert.That(pulsed, Is.False, "Fresh run must also default pulse OFF.");
            var toggle = Button("ВКЛЮЧИТЬ", "Эксперимент: инерционная пила");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(station.State.InertialSawExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);

            // The actual F1 reward listener uses the normal catalogue/stock grant path.
            int stock = station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Saw);
            var definition = UpgradeManager.Instance.DebugRewardCatalogue.Single(d => d.RewardKind == OrbitalRewardKind.Saw);
            var grant = Button("ВЫДАТЬ", LocalizationService.EnsureExists().Get(definition.upgradeName));
            Assert.That(grant.interactable, Is.True);
            grant.onClick.Invoke();
            yield return CoreTestSupport.Await(() => UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle &&
                station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Saw) == stock + 1);
            AssertSweepingShotgunProductionReady(station);
            Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Saw, 1, 2, out string error), Is.True, error);
            var gun = station.Modules.OfType<OrbitalSawModule>().Single(g => g.Kind == OrbitalModuleKind.Saw);
            Assert.That(gun.CurrentMount, Is.Not.Null, "Constructor must attach the rewarded Saw before capture.");
            Assert.That(gun.Settings, Is.Not.Null, "Rewarded Saw must have its runtime profile.");
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            yield return null; // Constructor consumes its closing frame, just as Tab does.
            Assert.That(station.enabled && station.InputOwner.IsIdle && Time.timeScale > 0f, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual", hidden).GetValue(gun);
            Assert.That(visual, Is.Not.Null);
            Assert.That(gun.InertialSawExperiment, Is.True);
            // Natural station Update samples the resting mount; never tick the Saw here.
            yield return null;
            yield return null;
            Assert.That(gun.IsInertialContactActive, Is.False);
            Quaternion restingRotation = visual.transform.localRotation;
            yield return null;
            Assert.That(Quaternion.Angle(restingRotation, visual.transform.localRotation), Is.LessThan(.001f));

            string directory = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/InertialSaw");
            System.IO.Directory.CreateDirectory(directory);
            string rotatingPath = System.IO.Path.Combine(directory, "inertial-rotating.png");
            string stoppedPath = System.IO.Path.Combine(directory, "inertial-stopped.png");
            void HoldE() => station.TickManualRotation(Time.deltaTime, false, true,
                station.Owner.CanControl && station.InputOwner.IsIdle && station.InputOwner.CanUseDebugPlacement);
            void RequestCapture(bool active, string capturePath)
            {
                AssertSweepingShotgunProductionReady(station);
                Assert.That(station.enabled && station.InputOwner.IsIdle && !station.InputOwner.IsConstructing, Is.True);
                Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
                Assert.That(Time.timeScale, Is.GreaterThan(0f));
                Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
                Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
                Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
                Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
                Assert.That(gun.InertialSawExperiment && gun.IsInertialContactActive == active, Is.True);
                Assert.That(visual.activeInHierarchy, Is.True);
                var view = visual.GetComponent<OrbitalModuleView>();
                Assert.That(view, Is.Not.Null);
                var renderer = view.Sprites.FirstOrDefault(r => r != null && r != view.Halo &&
                    r.enabled && r.gameObject.activeInHierarchy && r.sprite != null && r.color.a > .01f);
                Assert.That(renderer != null && renderer.enabled && renderer.sprite != null, Is.True);
                Vector3 viewport = Camera.main.WorldToViewportPoint(renderer.bounds.center);
                Assert.That(viewport.z > 0f && viewport.x > .05f && viewport.x < .95f &&
                    viewport.y > .05f && viewport.y < .95f, Is.True, "Installed Saw must be inside the live Game View.");
                ScreenCapture.CaptureScreenshot(capturePath);
            }
            bool FreshFile(string capturePath, System.DateTime requested) => System.IO.File.Exists(capturePath) &&
                System.IO.File.GetLastWriteTimeUtc(capturePath) >= requested && new System.IO.FileInfo(capturePath).Length > 0;

            float energy = station.RotationEnergy;
            float phase = gun.CurrentMount.Ring.Phase;
            Quaternion beforeSpin = visual.transform.localRotation;
            float until = Time.time + .35f;
            do
            {
                HoldE();
                yield return null; // The live station Update observes E's actual phase change.
            } while (Time.time < until);
            Assert.That(gun.IsInertialContactActive, Is.True);
            Assert.That(Quaternion.Angle(beforeSpin, visual.transform.localRotation), Is.GreaterThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase, gun.CurrentMount.Ring.Phase)), Is.GreaterThan(1f));
            Assert.That(station.RotationEnergy, Is.LessThan(energy), "Holding E consumes ordinary rotation energy.");
            yield return new WaitForEndOfFrame();
            var rotatingRequested = System.DateTime.UtcNow;
            RequestCapture(true, rotatingPath);
            float deadline = Time.realtimeSinceStartup + 6f;
            // Continue holding through the capture frame and disk write, with all simulation live.
            while (!FreshFile(rotatingPath, rotatingRequested))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Rotating Game View was not written.");
                HoldE();
                yield return null;
            }
            // Release E: ordinary Update must stop contact and the Saw's own local spin.
            yield return null;
            yield return null;
            Assert.That(gun.IsInertialContactActive, Is.False);
            Quaternion stoppedRotation = visual.transform.localRotation;
            yield return null;
            Assert.That(gun.IsInertialContactActive, Is.False);
            Assert.That(Quaternion.Angle(stoppedRotation, visual.transform.localRotation), Is.LessThan(.001f));
            yield return new WaitForEndOfFrame();
            var stoppedRequested = System.DateTime.UtcNow;
            RequestCapture(false, stoppedPath);
            yield return CoreTestSupport.Await(() => FreshFile(stoppedPath, stoppedRequested));
            Debug.Log("Inertial Saw production Game View: " + rotatingPath + " ; " + stoppedPath);
        }
        finally
        {
            Call("CloseMenu");
            station.InputOwner.EndConstructor();
            station.DebugSetInertialSawExperiment(false);
            station.DebugSetPulsedSawExperiment(pulsed);
            station.DebugSetInertialSawExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator PulsedSawProductionMvpGameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return PulsedSawProductionCaptureBody();
    }

    private static IEnumerator PulsedSawProductionCaptureBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null, "Production player must expose PlayerHealth before capture setup.");
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        bool experiment = station.State.PulsedSawExperiment;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Call(string name, params object[] args) => typeof(Subject42DebugMenu).GetMethod(name, hidden).Invoke(menu, args);
        var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", System.Reflection.BindingFlags.NonPublic);
        UnityEngine.UI.Button Button(string label, string rowPrefix) =>
            ((GameObject)typeof(Subject42DebugMenu).GetField("menuRoot", hidden).GetValue(menu))
            .GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMPro.TMP_Text>().text == label &&
                b.transform.parent.name.StartsWith(rowPrefix, System.StringComparison.Ordinal));
        try
        {
            Assert.That(experiment, Is.False, "Fresh run must default to continuous Saw.");
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Call("OpenMenu");
            Call("SelectTab", System.Enum.ToObject(tabType, 0), true);
            var toggle = Button("ВКЛЮЧИТЬ", "Импульсная пила");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(station.State.PulsedSawExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);

            // The actual F1 reward listener uses the normal catalogue/stock grant path.
            int stock = station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Saw);
            var definition = UpgradeManager.Instance.DebugRewardCatalogue.Single(d => d.RewardKind == OrbitalRewardKind.Saw);
            var grant = Button("ВЫДАТЬ", LocalizationService.EnsureExists().Get(definition.upgradeName));
            Assert.That(grant.interactable, Is.True);
            grant.onClick.Invoke();
            yield return CoreTestSupport.Await(() => UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle &&
                station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Saw) == stock + 1);
            AssertSweepingShotgunProductionReady(station);
            Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Saw, 1, 2, out string error), Is.True, error);
            var gun = station.Modules.OfType<OrbitalSawModule>().Single(g => g.Kind == OrbitalModuleKind.Saw);
            Assert.That(gun.CurrentMount, Is.Not.Null, "Constructor must attach the rewarded Saw before capture.");
            Assert.That(gun.Settings, Is.Not.Null, "Rewarded Saw must have its runtime profile.");
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            yield return null; // Constructor consumes its closing frame, just as Tab does.
            Assert.That(station.enabled && station.InputOwner.IsIdle && Time.timeScale > 0f, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual", hidden).GetValue(gun);
            Assert.That(visual, Is.Not.Null);
            // Observe a complete natural cycle; never tick the weapon or edit its phase clock.
            yield return CoreTestSupport.Await(() => !gun.IsPulseContactActive);
            yield return CoreTestSupport.Await(() => gun.IsPulseContactActive);
            Quaternion activeRotation = visual.transform.localRotation;
            yield return null;
            Assert.That(gun.IsPulseContactActive, Is.True);
            Assert.That(Quaternion.Angle(activeRotation, visual.transform.localRotation), Is.GreaterThan(.01f));
            yield return CapturePulsedSawGameView(station, gun, true, "pulsed-active");
            yield return CoreTestSupport.Await(() => !gun.IsPulseContactActive);
            Quaternion recoveryRotation = visual.transform.localRotation;
            yield return null;
            Assert.That(gun.IsPulseContactActive, Is.False);
            Assert.That(Quaternion.Angle(recoveryRotation, visual.transform.localRotation), Is.LessThan(.001f));
            yield return CapturePulsedSawGameView(station, gun, false, "pulsed-recovery");
            yield return CoreTestSupport.Await(() => gun.IsPulseContactActive);
            Assert.That(station.enabled && station.InputOwner.IsIdle && Time.timeScale > 0f, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            Call("CloseMenu");
            station.InputOwner.EndConstructor();
            station.DebugSetPulsedSawExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    private static IEnumerator CapturePulsedSawGameView(OrbitalStationRuntime station, OrbitalSawModule saw,
        bool active, string filename)
    {
        string path = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/PulsedSaw/" + filename + ".png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        // Recovery lasts .4 s: verify the actual phase after Update, immediately before capture.
        float deadline = Time.realtimeSinceStartup + 6f;
        do
        {
            yield return new WaitForEndOfFrame();
            if (saw.IsPulseContactActive == active) break;
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Live Saw phase did not arrive for capture.");
            yield return null;
        } while (true);
        AssertSweepingShotgunProductionReady(station);
        Assert.That(station.enabled && station.InputOwner.IsIdle && !station.InputOwner.IsConstructing, Is.True);
        Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
        Assert.That(Time.timeScale, Is.GreaterThan(0f));
        Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
        Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        Assert.That(saw.PulsedSawExperiment && saw.IsPulseContactActive == active, Is.True);
        float expected = saw.Settings.ContactRadius * station.State.ContactSizeMultiplier *
            (active ? OrbitalSawModule.PulseRadiusMultiplier : 1f);
        Assert.That(saw.ContactRadius, Is.EqualTo(expected).Within(.0001f));
        var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(saw);
        Assert.That(visual.activeInHierarchy, Is.True);
        Assert.That(visual.transform.localScale.x, Is.EqualTo(expected / .65f).Within(.0001f));
        var requested = System.DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path) &&
            System.IO.File.GetLastWriteTimeUtc(path) >= requested && new System.IO.FileInfo(path).Length > 0);
        Debug.Log("Pulsed Saw production Game View (active=" + active + "): " + path);
    }

    [Category("Core"), UnityTest]
    public IEnumerator SweepingShotgunProductionMvpGameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null, "Production player must expose PlayerHealth before capture setup.");
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Call(string name, params object[] args) => typeof(Subject42DebugMenu).GetMethod(name, hidden).Invoke(menu, args);
        var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", System.Reflection.BindingFlags.NonPublic);
        UnityEngine.UI.Button Button(string label, string rowPrefix) =>
            ((GameObject)typeof(Subject42DebugMenu).GetField("menuRoot", hidden).GetValue(menu))
            .GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMPro.TMP_Text>().text == label &&
                b.transform.parent.name.StartsWith(rowPrefix, System.StringComparison.Ordinal));
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Call("OpenMenu");
            Call("SelectTab", System.Enum.ToObject(tabType, 0), true);
            var toggle = Button("ВКЛЮЧИТЬ", "Веерная очередь Shotgun");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(station.State.SweepingShotgunExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);

            // The actual F1 reward listener uses the normal catalogue/stock grant path.
            int stock = station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Shotgun);
            var definition = UpgradeManager.Instance.DebugRewardCatalogue.Single(d => d.RewardKind == OrbitalRewardKind.Shotgun);
            var grant = Button("ВЫДАТЬ", LocalizationService.EnsureExists().Get(definition.upgradeName));
            Assert.That(grant.interactable, Is.True);
            grant.onClick.Invoke();
            yield return CoreTestSupport.Await(() => UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle &&
                station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Shotgun) == stock + 1);
            AssertSweepingShotgunProductionReady(station);
            Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Shotgun, 1, 2, out string error), Is.True, error);
            var gun = station.Modules.OfType<OrbitalProjectileModule>().Single(g => g.Kind == OrbitalModuleKind.Shotgun);
            Assert.That(gun.CurrentMount, Is.Not.Null, "Constructor must attach the rewarded Shotgun before capture.");
            Assert.That(gun.Settings, Is.Not.Null, "Rewarded Shotgun must have its runtime profile.");
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            yield return null; // Constructor consumes its closing frame, just as Tab does.
            Assert.That(station.enabled && station.InputOwner.IsIdle && Time.timeScale > 0f, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
            Assert.That(gun.Settings.BurstShots, Is.EqualTo(3));
            float energy = station.RotationEnergy;
            float phase = station.Rings[0].Phase;
            int shots = gun.ShotsFired;
            float deadline = Time.realtimeSinceStartup + 8f;
            // Feed E's held state into the same method used by Update's Q/E input.
            // Station Update, projectile simulation, AI, camera and presentation remain live.
            while (gun.ShotsFired < shots + 1 && Time.realtimeSinceStartup < deadline)
            {
                station.TickManualRotation(Time.deltaTime, false, true,
                    station.Owner.CanControl && station.InputOwner.IsIdle && station.InputOwner.CanUseDebugPlacement);
                yield return null;
            }
            Assert.That(gun.ShotsFired, Is.GreaterThanOrEqualTo(shots + 1));
            yield return CaptureSweepingShotgunGameView("sweeping-start");
            while (gun.ShotsFired < shots + 3 && Time.realtimeSinceStartup < deadline)
            {
                station.TickManualRotation(Time.deltaTime, false, true,
                    station.Owner.CanControl && station.InputOwner.IsIdle && station.InputOwner.CanUseDebugPlacement);
                yield return null;
            }
            Assert.That(gun.ShotsFired, Is.GreaterThanOrEqualTo(shots + 3), "Live station emits the subsequent volleys without manual gun ticks.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase, station.Rings[0].Phase)), Is.GreaterThan(1f));
            Assert.That(station.RotationEnergy, Is.LessThan(energy), "Production rotation consumes energy.");
            Assert.That(station.enabled && !Subject42DebugMenu.IsDebugMenuOpen && !station.InputOwner.IsConstructing, Is.True);
            yield return CaptureSweepingShotgunGameView("sweeping-followthrough");
        }
        finally
        {
            Call("CloseMenu");
            station.InputOwner.EndConstructor();
            station.DebugSetSweepingShotgunExperiment(false);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    private static IEnumerator CaptureSweepingShotgunGameView(string phase)
    {
        string path = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/SweepingShotgun/" + phase + ".png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var requested = System.DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path) &&
            System.IO.File.GetLastWriteTimeUtc(path) >= requested && new System.IO.FileInfo(path).Length > 0);
        Debug.Log("Sweeping Shotgun production Game View: " + path);
    }

    [Category("Core"), UnityTest]
    public IEnumerator RamSlugShotgunHitsThreeBodiesAndStopsAtShield()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        yield return RamSlugShotgunBodiesAndShieldBody();
    }

    private static IEnumerator RamSlugShotgunBodiesAndShieldBody()
    {
        var root = new GameObject("Ram slug production contact contract");
        var enemies = new GameObject[6];
        ProductionOrbitalCombatAdapter adapter = null;
        try
        {
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            Assert.That(EnemyHealth.ActiveInstances.Count, Is.Zero);
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(i == 4
                    ? "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"
                    : "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
                enemies[i].transform.position = new Vector3(30f, 30f + i * 3f);
                foreach (var movement in enemies[i].GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
                foreach (var body in enemies[i].GetComponentsInChildren<Rigidbody2D>())
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
            yield return null; // Authored Start precedes health preparation and bounds measurement.
            var health = enemies.Select(e => e.GetComponent<EnemyHealth>()).ToArray();
            var movements = enemies.Select(e => e.GetComponent<EnemyChaseMovement>()).ToArray();
            var impulseField = typeof(EnemyChaseMovement).GetField("knockbackVelocity",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(impulseField, Is.Not.Null);
            var hits = new int[6];
            for (int i = 0; i < enemies.Length; i++)
            {
                int index = i;
                Assert.That(movements[i], Is.Not.Null);
                health[i].SetRuntimeMaxHealth(1000f);
                health[i].HitFeedback += (_, _, _) => hits[index]++;
                Assert.That((Vector2)impulseField.GetValue(movements[i]), Is.EqualTo(Vector2.zero));
            }
            Physics2D.SyncTransforms();
            var settings = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Shotgun)
                .CreateRamSlugShotgunExperiment();
            Assert.That(settings.Range, Is.EqualTo(3.5f));
            Assert.That(settings.Damage, Is.EqualTo(10f));
            Assert.That(settings.BypassShields, Is.False);
            // Pack separate authored bounds without changing scale, colliders or weapon range.
            float edge = .04f;
            for (int i = 0; i < 4; i++)
            {
                var body = enemies[i].GetComponent<Collider2D>();
                Assert.That(body, Is.Not.Null);
                var bounds = body.bounds;
                enemies[i].transform.position += (Vector3)(new Vector2(edge + bounds.extents.x, 0f)
                    - (Vector2)bounds.center);
                edge += bounds.size.x + .025f;
            }
            Physics2D.SyncTransforms();
            Assert.That(edge - .025f, Is.LessThan(settings.Range),
                "Four real bounds must fit; do not shrink geometry for PASS.");
            adapter = new ProductionOrbitalCombatAdapter(root.transform, null);
            adapter.SpawnDirectionalWeaponProjectile(Vector2.zero, Vector2.right, settings, null, settings.Damage, 0);
            object shot = MissReturnProjectiles(adapter).Single(p => MissReturnField<bool>(p, "Active"));
            Assert.That(MissReturnField<int>(shot, "BodiesRemaining"), Is.EqualTo(3));
            Assert.That(MissReturnField<int>(shot, "RicochetsRemaining"), Is.Zero);
            var sweep = new System.Collections.Generic.List<RaycastHit2D>();
            Physics2D.CapsuleCast(Vector2.zero, MissReturnField<Vector2>(shot, "BodySize"),
                CapsuleDirection2D.Horizontal, 0f, Vector2.right, new ContactFilter2D { useTriggers = true },
                sweep, settings.Range);
            for (int i = 0; i < 4; i++)
            {
                var bounds = enemies[i].GetComponent<Collider2D>().bounds;
                Assert.That(bounds.min.x, Is.GreaterThan(0f));
                Assert.That(bounds.max.x, Is.LessThan(settings.Range));
                Assert.That(sweep.Any(h => h.collider.GetComponentInParent<EnemyHealth>() == health[i]), Is.True,
                    "Precondition: sweep reaches body " + i);
                if (i > 0) Assert.That(bounds.min.x,
                    Is.GreaterThan(enemies[i - 1].GetComponent<Collider2D>().bounds.max.x));
            }
            for (int frame = 0; frame < 90 && adapter.LabActiveProjectileCount > 0; frame++) adapter.Tick(1f / 120f);
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(hits[i], Is.EqualTo(i < 3 ? 1 : 0), "Distinct body " + i);
                Assert.That(health[i].CurrentHealth, Is.EqualTo(i < 3 ? 990f : 1000f).Within(.001f));
                Vector2 impulse = (Vector2)impulseField.GetValue(movements[i]);
                Assert.That(impulse.x, Is.EqualTo(i < 3 ? 5f : 0f).Within(.001f));
                Assert.That(impulse.y, Is.EqualTo(0f).Within(.001f));
            }
            // No yielded physics: this proves impulse delivery, not visible displacement.
            for (int i = 0; i < 4; i++) enemies[i].transform.position += Vector3.up * 30f;
            var shield = enemies[4].GetComponent<EnemyDirectionalShield>();
            root.transform.position = shield.Center + Vector2.right * 2f;
            shield.Bind(root.transform);
            Vector2 direction = -shield.Forward;
            Vector2 origin = shield.Center + shield.Forward * 2f;
            var protectedBody = enemies[5].GetComponent<Collider2D>();
            enemies[5].transform.position += (Vector3)(shield.Center + direction * .7f - (Vector2)protectedBody.bounds.center);
            Physics2D.SyncTransforms();
            Assert.That(shield.CanBlockProjectile(direction), Is.True);
            var ray = Physics2D.RaycastAll(origin, direction, settings.Range).OrderBy(h => h.distance).ToArray();
            var plate = ray.Where(h => h.collider == shield.ShieldCollider).ToArray();
            var target = ray.Where(h => h.collider.GetComponentInParent<EnemyHealth>() == health[5]).ToArray();
            Assert.That(plate.Length, Is.GreaterThan(0), "Actual frontal plate intersection.");
            Assert.That(target.Length, Is.GreaterThan(0), "Protected target is reachable behind plate.");
            Assert.That(plate[0].distance, Is.LessThan(target[0].distance));
            int blocks = shield.Blocks;
            adapter.SpawnDirectionalWeaponProjectile(origin, direction, settings, null, settings.Damage, 0);
            shot = MissReturnProjectiles(adapter).Single(p => MissReturnField<bool>(p, "Active"));
            sweep.Clear();
            Physics2D.CapsuleCast(origin, MissReturnField<Vector2>(shot, "BodySize"),
                CapsuleDirection2D.Horizontal, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                direction, new ContactFilter2D { useTriggers = true }, sweep, settings.Range);
            var orderedContacts = sweep.Where(h => h.collider.GetComponentInParent<EnemyHealth>() != null)
                .OrderBy(h => h.distance).ToArray();
            Assert.That(orderedContacts.Length, Is.GreaterThan(0));
            Assert.That(orderedContacts[0].collider, Is.SameAs(shield.ShieldCollider),
                "Production swept shape must meet the plate before either body.");
            for (int frame = 0; frame < 90 && adapter.LabActiveProjectileCount > 0; frame++) adapter.Tick(1f / 120f);
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero);
            Assert.That(shield.Blocks, Is.EqualTo(blocks + 1));
            for (int i = 4; i < 6; i++)
            {
                Assert.That(hits[i], Is.Zero);
                Assert.That(health[i].CurrentHealth, Is.EqualTo(1000f));
                Assert.That((Vector2)impulseField.GetValue(movements[i]), Is.EqualTo(Vector2.zero));
            }
        }
        finally
        {
            adapter?.Teardown();
            foreach (var enemy in enemies) if (enemy != null) Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(root);
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator RamSlugShotgunProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return RamSlugShotgunProductionCaptureBody();
    }
    private static IEnumerator RamSlugShotgunProductionCaptureBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        yield return CoreTestSupport.Await(() => Camera.main != null &&
            UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        AssertSweepingShotgunProductionReady(station);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null, "Production player must expose PlayerHealth before capture setup.");
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        bool experiment = station.State.RamSlugShotgunExperiment;
        bool sweeping = station.State.SweepingShotgunExperiment;
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Call(string name, params object[] args) => typeof(Subject42DebugMenu).GetMethod(name, hidden).Invoke(menu, args);
        var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", System.Reflection.BindingFlags.NonPublic);
        UnityEngine.UI.Button Button(string label, string rowPrefix) =>
            ((GameObject)typeof(Subject42DebugMenu).GetField("menuRoot", hidden).GetValue(menu))
            .GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMPro.TMP_Text>().text == label &&
                b.transform.parent.name.StartsWith(rowPrefix, System.StringComparison.Ordinal));
        try
        {
            Assert.That(OrbitalRunState.CreateDefault(1).RamSlugShotgunExperiment, Is.False);
            Assert.That(experiment, Is.False, "Fresh run must default to ordinary Shotgun.");
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Call("OpenMenu");
            Call("SelectTab", System.Enum.ToObject(tabType, 0), true);
            Assert.That(sweeping, Is.False, "Fresh run must also default Sweeping OFF.");
            var toggle = Button("ВКЛЮЧИТЬ", "Эксперимент: картечный таран");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(station.State.RamSlugShotgunExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);

            // The actual F1 reward listener uses the normal catalogue/stock grant path.
            int stock = station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Shotgun);
            var definition = UpgradeManager.Instance.DebugRewardCatalogue.Single(d => d.RewardKind == OrbitalRewardKind.Shotgun);
            var grant = Button("ВЫДАТЬ", LocalizationService.EnsureExists().Get(definition.upgradeName));
            Assert.That(grant.interactable, Is.True);
            grant.onClick.Invoke();
            yield return CoreTestSupport.Await(() => UpgradeManager.Instance != null && UpgradeManager.Instance.IsRewardQueueIdle &&
                station.State.SegmentedWeapons.Count(w => w.ModuleType == OrbitalModuleKind.Shotgun) == stock + 1);
            AssertSweepingShotgunProductionReady(station);
            Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(station.ConstructWeapon(OrbitalModuleKind.Shotgun, 1, 2, out string error), Is.True, error);
            var gun = station.Modules.OfType<OrbitalProjectileModule>().Single(g => g.Kind == OrbitalModuleKind.Shotgun);
            Assert.That(gun.CurrentMount, Is.Not.Null, "Constructor must attach the rewarded Shotgun before capture.");
            Assert.That(gun.Settings, Is.Not.Null, "Rewarded Shotgun must have its runtime profile.");
            Assert.That(station.InputOwner.TryToggleConstructor(), Is.True);
            yield return null; // Constructor consumes its closing frame, just as Tab does.
            Assert.That(station.enabled && station.InputOwner.IsIdle && Time.timeScale > 0f, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
            Assert.That(gun.Settings.Pellets, Is.EqualTo(1));
            Assert.That(gun.Settings.BurstShots, Is.EqualTo(1));
            Assert.That(gun.Settings.SpreadDegrees, Is.Zero);
            Assert.That(gun.Settings.ProjectileSpeed, Is.EqualTo(7f));
            Assert.That(gun.Settings.Range, Is.EqualTo(3.5f));
            Assert.That(gun.Settings.BodyPierces, Is.EqualTo(3));
            string directory = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/RamSlugShotgun");
            System.IO.Directory.CreateDirectory(directory);
            string livePath = System.IO.Path.Combine(directory, "ram-slug-live.png");
            string directedPath = System.IO.Path.Combine(directory, "ram-slug-directed.png");

            // Only observe production projectiles; never tick a gun or advance combat manually.
            SpriteRenderer VisibleSlug()
            {
                foreach (var shot in MissReturnProjectiles(adapter))
                {
                    if (!MissReturnField<bool>(shot, "Active") ||
                        MissReturnField<OrbitalModuleKind>(shot, "Kind") != OrbitalModuleKind.Shotgun ||
                        !Mathf.Approximately(MissReturnField<float>(shot, "Speed"), 7f)) continue;
                    var body = MissReturnField<GameObject>(shot, "GameObject");
                    if (body == null || !body.activeInHierarchy) continue;
                    foreach (var sprite in body.GetComponentsInChildren<SpriteRenderer>())
                    {
                        if (!sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null || sprite.color.a <= .01f) continue;
                        Vector3 viewport = Camera.main.WorldToViewportPoint(sprite.bounds.center);
                        if (viewport.z > 0f && viewport.x > .05f && viewport.x < .95f &&
                            viewport.y > .05f && viewport.y < .95f) return sprite;
                    }
                }
                return null;
            }
            void AssertLiveCapture()
            {
                AssertSweepingShotgunProductionReady(station);
                Assert.That(station.enabled && station.InputOwner.IsIdle && !station.InputOwner.IsConstructing, Is.True);
                Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
                Assert.That(Time.timeScale, Is.GreaterThan(0f));
                Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False);
                Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
                Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
                Assert.That(station.State.RamSlugShotgunExperiment, Is.True);
                Assert.That(station.State.SweepingShotgunExperiment, Is.False);
                Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
                Assert.That(VisibleSlug(), Is.Not.Null, "A live Ram Slug sprite must be inside the actual camera viewport.");
            }
            IEnumerator CaptureNextNaturalShot(string capturePath)
            {
                int shots = gun.ShotsFired;
                float deadline = Time.realtimeSinceStartup + 8f;
                do
                {
                    yield return new WaitForEndOfFrame();
                    if (gun.ShotsFired > shots && VisibleSlug() != null) break;
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                        "No natural visible Ram Slug arrived in the running MVP.");
                    yield return null;
                } while (true);
                AssertLiveCapture();
                var requested = System.DateTime.UtcNow;
                ScreenCapture.CaptureScreenshot(capturePath);
                yield return CoreTestSupport.Await(() => System.IO.File.Exists(capturePath) &&
                    System.IO.File.GetLastWriteTimeUtc(capturePath) >= requested &&
                    new System.IO.FileInfo(capturePath).Length > 0);
                Debug.Log("Ram Slug production Game View: " + capturePath);
            }

            yield return CaptureNextNaturalShot(livePath);
            float energy = station.RotationEnergy;
            float phase = gun.CurrentMount.Ring.Phase;
            float until = Time.time + .35f;
            do
            {
                station.TickManualRotation(Time.deltaTime, false, true,
                    station.Owner.CanControl && station.InputOwner.IsIdle && station.InputOwner.CanUseDebugPlacement);
                yield return null; // All ordinary Update, AI, physics, spawner and camera remain live.
            } while (Time.time < until);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase, gun.CurrentMount.Ring.Phase)), Is.GreaterThan(1f));
            Assert.That(station.RotationEnergy, Is.LessThan(energy), "E must consume the ordinary rotation energy.");
            yield return CaptureNextNaturalShot(directedPath);
        }
        finally
        {
            Call("CloseMenu");
            station.InputOwner.EndConstructor();
            if (station.State.RamSlugShotgunExperiment != experiment)
                station.DebugSetRamSlugShotgunExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator RamSlugShotgunFutureShotsPreserveLiveProjectiles()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return RamSlugShotgunFutureShotsBody();
    }

    private static IEnumerator RamSlugShotgunFutureShotsBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var gun = InstallSweepingShotgun(station, 2); // Ordinary Shotgun, no Sweeping opt-in.
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var authored = gun.Settings;
        string authoredJson = JsonUtility.ToJson(authored);
        var presentation = authored.Presentation;
        Assert.That(presentation, Is.Not.Null, "Production Shotgun needs its authored presentation.");
        string presentationJson = JsonUtility.ToJson(presentation);
        var cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float previousCooldown = (float)cooldown.GetValue(gun);
        bool ram = station.State.RamSlugShotgunExperiment;
        var before = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active")).ToArray();
        try
        {
            Assert.That(ram, Is.False);
            Assert.That(station.State.SweepingShotgunExperiment, Is.False);
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            cooldown.SetValue(gun, 0f);
            gun.ActivateCombat();
            var fan = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active") && !before.Contains(p)).ToArray();
            Assert.That(fan.Length, Is.EqualTo(7));
            var fanSnapshots = fan.Select(RamSlugProjectileSnapshot).ToArray();
            float clock = (float)cooldown.GetValue(gun);
            Assert.That(clock, Is.GreaterThan(0f));
            Assert.That(station.DebugSetRamSlugShotgunExperiment(true), Is.True);
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(clock));
            for (int i = 0; i < fan.Length; i++)
                Assert.That(RamSlugProjectileSnapshot(fan[i]), Is.EqualTo(fanSnapshots[i]), "ON must preserve airborne fan.");
            Assert.That(gun.Settings, Is.Not.SameAs(authored));
            Assert.That(gun.Settings.Presentation, Is.Not.SameAs(presentation));
            Assert.That(gun.Settings.Presentation.BodySize, Is.EqualTo(presentation.BodySize * 1.6f));
            Assert.That(gun.Settings.Presentation.Color, Is.EqualTo(presentation.Color));
            Assert.That(gun.Settings.FireInterval, Is.EqualTo(authored.FireInterval));
            Assert.That(gun.Settings.Pellets, Is.EqualTo(1));
            Assert.That(gun.Settings.BurstShots, Is.EqualTo(1));
            Assert.That(gun.Settings.SpreadDegrees, Is.Zero);
            var beforeSlug = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active")).ToArray();
            gun.Tick(clock + .001f); // Let the ordinary cooldown expire through the real module path.
            var slug = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active") && !beforeSlug.Contains(p)).Single();
            Assert.That(MissReturnField<float>(slug, "Damage"), Is.EqualTo(10f * gun.CurrentMount.Ring.PowerMultiplier * station.GetModuleMetaDamageMultiplier(gun.Kind) * station.GetModuleDamageMultiplier(gun.StableModuleId) * gun.DamageMultiplier).Within(.001f));
            Assert.That(MissReturnField<float>(slug, "Speed"), Is.EqualTo(7f));
            Assert.That(MissReturnField<float>(slug, "RemainingRange"), Is.EqualTo(3.5f));
            Assert.That(MissReturnField<int>(slug, "BodiesRemaining"), Is.EqualTo(3));
            Assert.That(MissReturnField<float>(slug, "Knockback"), Is.EqualTo(5f));
            Assert.That(MissReturnField<bool>(slug, "BypassShields"), Is.False);
            Assert.That(MissReturnField<Vector2>(slug, "BodySize"), Is.EqualTo(presentation.BodySize * 1.6f * gun.Settings.ProjectileVisualScale));
            clock = (float)cooldown.GetValue(gun);
            var beforeExtra = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active")).ToArray();
            Assert.That(gun.FireExtraShot(), Is.True);
            var extra = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active") && !beforeExtra.Contains(p)).Single();
            Assert.That(MissReturnField<float>(extra, "Speed"), Is.EqualTo(7f));
            Assert.That(MissReturnField<float>(extra, "RemainingRange"), Is.EqualTo(3.5f));
            Assert.That(MissReturnField<int>(extra, "BodiesRemaining"), Is.EqualTo(3));
            Assert.That(MissReturnField<float>(extra, "Knockback"), Is.EqualTo(5f));
            Assert.That(MissReturnField<bool>(extra, "BypassShields"), Is.False);
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(clock));
            var airborne = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active")).ToArray();
            var snapshots = airborne.Select(RamSlugProjectileSnapshot).ToArray();
            Assert.That(station.DebugSetRamSlugShotgunExperiment(false), Is.True);
            Assert.That(gun.Settings, Is.SameAs(authored));
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(clock));
            for (int i = 0; i < airborne.Length; i++)
                Assert.That(RamSlugProjectileSnapshot(airborne[i]), Is.EqualTo(snapshots[i]), "OFF must preserve every live projectile.");
            gun.Tick(clock + .001f);
            var restoredFan = MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active") && !airborne.Contains(p)).ToArray();
            Assert.That(restoredFan.Length, Is.EqualTo(authored.Pellets));
            Assert.That(restoredFan.All(p => MissReturnField<float>(p, "Speed") == authored.ProjectileSpeed &&
                MissReturnField<float>(p, "RemainingRange") == authored.Range), Is.True);
            Assert.That(JsonUtility.ToJson(authored), Is.EqualTo(authoredJson));
            Assert.That(JsonUtility.ToJson(presentation), Is.EqualTo(presentationJson));
        }
        finally
        {
            var release = typeof(ProductionOrbitalCombatAdapter).GetMethod("Release",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var shot in MissReturnProjectiles(adapter).Where(p => MissReturnField<bool>(p, "Active") && !before.Contains(p)))
                release.Invoke(adapter, new[] { shot });
            if (station.State.RamSlugShotgunExperiment != ram) station.DebugSetRamSlugShotgunExperiment(ram);
            cooldown.SetValue(gun, previousCooldown);
            station.InputOwner.EndConstructor();
        }
    }

    private static object[] RamSlugProjectileSnapshot(object shot) => new object[]
    {
        MissReturnField<bool>(shot, "Active"), MissReturnField<Vector2>(shot, "Position"),
        MissReturnField<Vector2>(shot, "Direction"), MissReturnField<float>(shot, "Damage"),
        MissReturnField<float>(shot, "Speed"), MissReturnField<float>(shot, "RemainingRange"),
        MissReturnField<int>(shot, "BodiesRemaining"), MissReturnField<float>(shot, "Knockback"),
        MissReturnField<bool>(shot, "BypassShields"), MissReturnField<Vector2>(shot, "BodySize"),
        MissReturnField<OrbitalWeaponSettings.ProjectileStyle>(shot, "Presentation")
    };

    [Category("Core"), UnityTest]
    public IEnumerator RamSlugShotgunOptInPreservesState()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return RamSlugShotgunOptInBody();
    }

    private static IEnumerator RamSlugShotgunOptInBody()
    {
        yield return BeginSweepingShotgunProductionRun();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var state = station.State;
        Assert.That(OrbitalRunState.CreateDefault(1).RamSlugShotgunExperiment, Is.False);
        Assert.That(state.RamSlugShotgunExperiment, Is.False);
        Assert.That(state.SweepingShotgunExperiment, Is.False);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        var gun = InstallSweepingShotgun(station, 2);
        var authored = gun.Settings;
        int id = gun.StableModuleId;
        Assert.That(station.UpgradeModuleDamage(id), Is.True);
        string identity = JsonUtility.ToJson(state.FindModule(id));
        var mount = gun.CurrentMount;
        var cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float previousCooldown = (float)cooldown.GetValue(gun);
        try
        {
            cooldown.SetValue(gun, .37f);
            Assert.That(station.DebugSetRamSlugShotgunExperiment(true), Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(station.DebugSetSweepingShotgunExperiment(true), Is.False);
            Assert.That(state.RamSlugShotgunExperiment, Is.True);
            Assert.That(state.SweepingShotgunExperiment, Is.False);
            Assert.That(station.Modules.Single(m => m.StableModuleId == id), Is.SameAs(gun));
            Assert.That(gun.CurrentMount, Is.SameAs(mount));
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(.37f));
            Assert.That(JsonUtility.ToJson(state.FindModule(id)), Is.EqualTo(identity));
            Assert.That(station.DebugSetRamSlugShotgunExperiment(false), Is.True);
            Assert.That(gun.Settings, Is.SameAs(authored));
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(.37f));
            // Only test the mutual refusal; do not fire the blocked Sweeping variant.
            Assert.That(station.DebugSetSweepingShotgunExperiment(true), Is.True);
            float sweepingClock = (float)cooldown.GetValue(gun);
            Assert.That(station.DebugSetRamSlugShotgunExperiment(true), Is.False);
            Assert.That(state.SweepingShotgunExperiment, Is.True);
            Assert.That(state.RamSlugShotgunExperiment, Is.False);
            Assert.That((float)cooldown.GetValue(gun), Is.EqualTo(sweepingClock));
            Assert.That(station.DebugSetSweepingShotgunExperiment(false), Is.True);
            Assert.That(station.DebugSetRamSlugShotgunExperiment(true), Is.True);
            var later = InstallSweepingShotgun(station, 4);
            Assert.That(later.Settings.Pellets, Is.EqualTo(1));
            Assert.That(later.Settings.BodyPierces, Is.EqualTo(3));
            var roundTrip = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
            Assert.That(roundTrip.RamSlugShotgunExperiment, Is.True);
            Assert.That(roundTrip.SweepingShotgunExperiment, Is.False);
            Assert.That(JsonUtility.ToJson(roundTrip.FindModule(id)), Is.EqualTo(identity));
            string captured = JsonUtility.ToJson(station.CaptureState());
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(JsonUtility.ToJson(station.CaptureState()), Is.EqualTo(captured));
            gun = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.StableModuleId == id);
            Assert.That(station.State.RamSlugShotgunExperiment, Is.True);
            Assert.That(gun.Settings.Pellets, Is.EqualTo(1));
            Assert.That(gun.Settings.BodyPierces, Is.EqualTo(3));
            Assert.That(gun.CurrentMount.MountIndex, Is.EqualTo(mount.MountIndex));
            Assert.That(gun.CurrentMount.Ring.RingId, Is.EqualTo(mount.Ring.RingId));
            Assert.That(JsonUtility.ToJson(station.State.FindModule(id)), Is.EqualTo(identity));
            previousCooldown = (float)cooldown.GetValue(gun); // Restore owns the new runtime clock.
            Assert.That(station.DebugSetRamSlugShotgunExperiment(false), Is.True);
            Assert.That(gun.Settings.Pellets, Is.EqualTo(authored.Pellets));
            Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(station.State)).RamSlugShotgunExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            if (station.State.RamSlugShotgunExperiment) station.DebugSetRamSlugShotgunExperiment(false);
            if (station.State.SweepingShotgunExperiment) station.DebugSetSweepingShotgunExperiment(false);
            cooldown.SetValue(gun, previousCooldown);
            station.InputOwner.EndConstructor();
        }
    }

    private static readonly System.Reflection.PropertyInfo ShotgunClock = typeof(OrbitalModuleRuntime)
        .GetProperty("RuntimeCooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    private static void AssertShotgunPair(ProductionOrbitalCombatAdapter adapter, Vector2 radial, float center)
    {
        // Inspect actual production projectiles; the LabProjectileSpawned event only covers lab shots.
        var projectiles = (IEnumerable)typeof(ProductionOrbitalCombatAdapter).GetField("projectiles",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(adapter);
        float[] angles = projectiles.Cast<object>()
            .Where(p => (bool)p.GetType().GetField("Active").GetValue(p))
            .Select(p => Vector2.SignedAngle(radial, (Vector2)p.GetType().GetField("Direction").GetValue(p)))
            .OrderBy(a => a).ToArray();
        Assert.That(angles.Length, Is.EqualTo(2));
        Assert.That(angles[0], Is.EqualTo(center - 6f).Within(.001f));
        Assert.That(angles[1], Is.EqualTo(center + 6f).Within(.001f));
    }

    private static OrbitalProjectileModule InstallSweepingShotgun(OrbitalStationRuntime station, int position)
    {
        AssertSweepingShotgunProductionReady(station);
        Assert.That(station.State.TryGrantSegmentedWeapon(OrbitalModuleKind.Shotgun), Is.True);
        if (!station.InputOwner.IsConstructing) Assert.That(station.InputOwner.BeginConstructor(), Is.True);
        Assert.That(station.ConstructWeapon(OrbitalModuleKind.Shotgun, 1, position, out string error), Is.True, error);
        var gun = station.Modules.OfType<OrbitalProjectileModule>()
            .SingleOrDefault(g => g.Kind == OrbitalModuleKind.Shotgun &&
                g.CurrentMount != null && g.CurrentMount.MountIndex == position);
        Assert.That(gun, Is.Not.Null, "Successful ConstructWeapon must produce an attached Shotgun at the requested mount.");
        Assert.That(gun.Settings, Is.Not.Null, "ConstructWeapon produced a Shotgun without settings.");
        Assert.That(gun.CurrentMount, Is.Not.Null, "ConstructWeapon produced a detached Shotgun.");
        Assert.That(station.State.FindModule(gun.StableModuleId), Is.Not.Null,
            "Installed Shotgun identity must resolve in the authoritative run state.");
        return gun;
    }

    private static void AssertSweepingShotgunProductionReady(OrbitalStationRuntime station)
    {
        Assert.That(RunStateManager.Instance, Is.Not.Null, "RunStateManager lifetime ended before the Shotgun scenario continued.");
        Assert.That(UpgradeManager.Instance, Is.Not.Null, "UpgradeManager is not alive in the production MVP.");
        Assert.That(station, Is.Not.Null, "Production station is missing or was destroyed.");
        Assert.That(station.IsInitialized, Is.True, "Production station has not finished initialization.");
        Assert.That(station.State, Is.Not.Null, "Initialized station must own run state.");
        Assert.That(station.InputOwner, Is.Not.Null, "Initialized station must have the production constructor/input owner.");
        Assert.That(station.Combat, Is.InstanceOf<ProductionOrbitalCombatAdapter>());
        Assert.That(PlayerRuntimeReference.CachedPlayer, Is.Not.Null, "Production player lifetime ended before the scenario continued.");
    }

    private static IEnumerator BeginSweepingShotgunProductionRun()
    {
        // EnterPlayMode is deliberately at each test's top level; wait for player and station before access.
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        Assert.That(starter, Is.Not.Null);
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null &&
            PlayerRuntimeReference.CachedPlayer != null &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        // Composition readiness alone does not specify singleton readiness. Bound this
        // local wait and diagnose each dependency instead of dereferencing in a predicate.
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((RunStateManager.Instance == null || UpgradeManager.Instance == null) &&
            Time.realtimeSinceStartup < deadline)
            yield return null;
        AssertSweepingShotgunProductionReady(Object.FindFirstObjectByType<OrbitalStationRuntime>());
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().UsesSegmentedOrbit, Is.True);
    }

    [Category("Core"), UnityTest]
    public IEnumerator ChargedLineSniperOptInPreservesRuntimeAndDevelopmentAttempt()
    {
        // Domain transitions must be yielded at test level (see DamagePopupPresentationTests).
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var manager = RunStateManager.Instance;
        var state = station.State;
        Assert.That(state.ChargedLineSniperExperiment, Is.False);
        Assert.That(manager.IsDevelopmentRun, Is.False);
        Assert.That(OrbitalRunState.CreateDefault(1).ChargedLineSniperExperiment, Is.False);
        Assert.That(state.TryGrantSegmentedWeapon(OrbitalModuleKind.Sniper), Is.True);
        Assert.That(station.InputOwner.BeginConstructor(), Is.True);
        Assert.That(station.ConstructWeapon(OrbitalModuleKind.Sniper, 1, 2, out string error), Is.True, error);
        var sniper = station.Modules.OfType<OrbitalProjectileModule>().Single(g => g.Kind == OrbitalModuleKind.Sniper);
        Assert.That(state.UpgradeModuleDamage(sniper.StableModuleId), Is.True);
        string moduleBefore = JsonUtility.ToJson(state.FindModule(sniper.StableModuleId));
        var mountBefore = sniper.CurrentMount;
        var authored = sniper.Settings;
        Assert.That(authored.ChargeSeconds, Is.Zero);
        Assert.That(authored.BodyPierces, Is.EqualTo(1));
        Assert.That(station.DebugSetChargedLineSniperExperiment(true), Is.True);
        Assert.That(manager.IsDevelopmentRun, Is.True);
        Assert.That(station.Modules.Single(g => g.StableModuleId == sniper.StableModuleId), Is.SameAs(sniper));
        Assert.That(sniper.CurrentMount, Is.SameAs(mountBefore));
        Assert.That(JsonUtility.ToJson(state.FindModule(sniper.StableModuleId)), Is.EqualTo(moduleBefore));
        Assert.That(sniper.Settings, Is.Not.SameAs(authored));
        Assert.That(sniper.Settings.ChargeSeconds, Is.EqualTo(.45f));
        Assert.That(sniper.Settings.BodyPierces, Is.EqualTo(3));
        Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).ChargedLineSniperExperiment, Is.True);
        Assert.That(state.TryGrantSegmentedWeapon(OrbitalModuleKind.Sniper), Is.True);
        Assert.That(station.ConstructWeapon(OrbitalModuleKind.Sniper, 1, 4, out error), Is.True, error);
        var later = station.Modules.OfType<OrbitalProjectileModule>().Single(g => g.Kind == OrbitalModuleKind.Sniper && g != sniper);
        Assert.That(later.Settings.ChargeSeconds, Is.EqualTo(.45f));
        Assert.That(state.TryGrantSegmentedLink(SegmentedLinkKind.ExtraShot), Is.True);
        Assert.That(station.ConstructModule(SegmentedLinkKind.ExtraShot, 1, 1, out error), Is.True, error);
        station.InputOwner.EndConstructor();

        // Keep the real MVP rendering, enemies and camera; manually advance only this weapon
        // so frame duration cannot hide the charging boundary or add an ordinary volley.
        bool wasEnabled = station.enabled;
        station.enabled = false;
        try
        {
            sniper.ResetFiring();
            int before = sniper.ShotsFired;
            sniper.Tick(.21f); // expires reset cooldown, starts the charge
            Assert.That(sniper.ShotsFired, Is.EqualTo(before));
            sniper.Tick(.20f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(before), "Charge must delay the ordinary shot.");
            sniper.Tick(0f); // apply the current charge to the authored weapon visual without advancing combat
            yield return CaptureChargedLineGameView("preparing");
            sniper.Tick(.26f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(before + 2), "One ordinary volley plus exactly one 2X volley; no recursion.");
            sniper.Tick(.01f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(before + 2), "2X must not reset the ordinary cooldown.");
            station.Combat.Tick(.012f); // show actual emitted projectiles slightly beyond the muzzle
            yield return CaptureChargedLineGameView("fired");
        }
        finally { station.enabled = wasEnabled; }

        Assert.That(station.DebugSetChargedLineSniperExperiment(false), Is.True);
        Assert.That(manager.IsDevelopmentRun, Is.True, "Disabling experimental combat cannot restore production eligibility.");
        Assert.That(sniper.Settings, Is.SameAs(authored));
        Assert.That(later.Settings.ChargeSeconds, Is.Zero);
        Assert.That(authored.BodyPierces, Is.EqualTo(1));
        Assert.That(authored.Damage, Is.EqualTo(32f));
        Assert.That(JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state)).ChargedLineSniperExperiment, Is.False);
        Assert.That(JsonUtility.ToJson(state.FindModule(sniper.StableModuleId)), Is.EqualTo(moduleBefore));
    }

    private static IEnumerator CaptureChargedLineGameView(string phase)
    {
        string path = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/ChargedLineSniper/" + phase + ".png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var requested = System.DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path) &&
            System.IO.File.GetLastWriteTimeUtc(path) >= requested);
        Debug.Log("Charged Line Sniper Game View: " + path);
    }

    [Category("Core"), UnityTest]
    public IEnumerator ChargedLineSniperDamagesThreeDistinctBodiesAndStopsAtFourth()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        var root = new GameObject("Charged Sniper production projectile contract");
        var enemies = new GameObject[4];
        ProductionOrbitalCombatAdapter adapter = null;
        try
        {
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            Assert.That(EnemyHealth.ActiveInstances.Count, Is.Zero);
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(i == 0
                    ? "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"
                    : "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
                foreach (var movement in enemies[i].GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
                foreach (var body in enemies[i].GetComponentsInChildren<Rigidbody2D>())
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
            yield return null;
            var shield = enemies[0].GetComponent<EnemyDirectionalShield>();
            root.transform.position = shield.Center + Vector2.right * 3f;
            shield.Bind(root.transform);
            Vector2 direction = -shield.Forward;
            Vector2 origin = shield.Center + shield.Forward * 3f;
            for (int i = 1; i < enemies.Length; i++)
            {
                var body = enemies[i].GetComponent<Collider2D>();
                enemies[i].transform.position += (Vector3)(shield.Center + direction * (i * 1.8f) - (Vector2)body.bounds.center);
            }
            // Two overlapping colliders on one production EnemyHealth must still count as one body.
            enemies[1].AddComponent<CircleCollider2D>().radius = .2f;
            Physics2D.SyncTransforms();
            var authored = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Sniper);
            string authoredBefore = JsonUtility.ToJson(authored);
            var settings = authored.CreateChargedLineSniperExperiment();
            var health = enemies.Select(e => e.GetComponent<EnemyHealth>()).ToArray();
            var hits = new int[4];
            for (int i = 0; i < health.Length; i++)
            {
                int index = i;
                health[i].SetRuntimeMaxHealth(1000f);
                health[i].HitFeedback += (_, _, _) => hits[index]++;
            }
            Assert.That(shield.CanBlockProjectile(direction), Is.True);
            var ray = Physics2D.RaycastAll(origin, direction, settings.Range);
            Assert.That(ray.Any(h => h.collider == shield.ShieldCollider), Is.True);
            foreach (var body in health)
                Assert.That(ray.Any(h => h.collider.GetComponentInParent<EnemyHealth>() == body), Is.True, "All four bodies must be in range.");
            int blocks = shield.Blocks;
            adapter = new ProductionOrbitalCombatAdapter(root.transform, null);
            adapter.SpawnDirectionalWeaponProjectile(origin, direction, settings, null, settings.Damage, 0);
            for (int frame = 0; frame < 120 && adapter.LabActiveProjectileCount > 0; frame++) adapter.Tick(1f / 120f);
            Assert.That(adapter.LabActiveProjectileCount, Is.Zero);
            for (int i = 0; i < health.Length; i++)
            {
                Assert.That(hits[i], Is.EqualTo(i < 3 ? 1 : 0), "Body " + i);
                Assert.That(health[i].CurrentHealth, Is.EqualTo(1000f - (i < 3 ? settings.Damage : 0f)).Within(.001f), "Body " + i);
            }
            Assert.That(shield.Blocks, Is.EqualTo(blocks), "Experimental Sniper retains frontal shield bypass.");
            Assert.That(JsonUtility.ToJson(authored), Is.EqualTo(authoredBefore));
            Assert.That(authored.BodyPierces, Is.EqualTo(1));
            Assert.That(authored.ChargeSeconds, Is.Zero);
        }
        finally
        {
            adapter?.DebugClearProjectiles();
            adapter?.Teardown();
            foreach (var enemy in enemies) if (enemy != null) Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(root);
        }
    }

    [Category("Core"), UnityTest]
    public IEnumerator ProductionSniperBypassesFrontalPlateButStopsAtFirstBody()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        yield return VerifySniperShieldAndBodyLimit();
    }

    // Create all runtime locals after domain reload. No station, player, spawner or hazard
    // exists in this scene; only the explicitly ticked adapter can deal damage.
    private static IEnumerator VerifySniperShieldAndBodyLimit()
    {
        GameObject root = null, first = null, second = null;
        ProductionOrbitalCombatAdapter adapter = null;
        try
        {
            Assert.That(EnemyHealth.ActiveInstances.Count, Is.Zero, "Fixture must start without enemies.");
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            root = new GameObject("Sniper shield contract");
            first = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"));
            second = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
            foreach (var enemy in new[] { first, second })
            {
                foreach (var movement in enemy.GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
                foreach (var body in enemy.GetComponentsInChildren<Rigidbody2D>())
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
            yield return null; // EnemyHealth.Start must finish before setting baseline HP.
            var shield = first.GetComponent<EnemyDirectionalShield>();
            var firstHealth = first.GetComponent<EnemyHealth>();
            var secondHealth = second.GetComponent<EnemyHealth>();
            root.transform.position = shield.Center + Vector2.right * 3f;
            shield.Bind(root.transform);
            Vector2 direction = -shield.Forward;
            Vector2 origin = shield.Center + shield.Forward * 3f;
            var secondBody = second.GetComponent<Collider2D>();
            Assert.That(secondBody, Is.Not.Null);
            second.transform.position += (Vector3)(shield.Center + direction * 2f - (Vector2)secondBody.bounds.center);
            Physics2D.SyncTransforms();

            var sniper = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Sniper);
            var pistol = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol);
            Assert.That(sniper.BypassShields, Is.True);
            Assert.That(sniper.BodyPierces, Is.EqualTo(1));
            Assert.That(pistol.BypassShields, Is.False);
            Assert.That(shield.CanBlockProjectile(direction), Is.True);
            // Observe actual colliders independently of the adapter's swept-shape query.
            var hits = Physics2D.RaycastAll(origin, direction, Mathf.Min(sniper.Range, pistol.Range))
                .OrderBy(hit => hit.distance).ToArray();
            var plate = hits.Where(hit => hit.collider == shield.ShieldCollider).ToArray();
            var firstBodies = hits.Where(hit => hit.collider != shield.ShieldCollider &&
                hit.collider.GetComponentInParent<EnemyHealth>() == firstHealth).ToArray();
            var secondBodies = hits.Where(hit => hit.collider.GetComponentInParent<EnemyHealth>() == secondHealth).ToArray();
            Assert.That(plate.Length, Is.GreaterThan(0), "Ray must cross frontal plate.");
            Assert.That(firstBodies.Length, Is.GreaterThan(0), "Ray must cross first body.");
            Assert.That(secondBodies.Length, Is.GreaterThan(0), "Second body must be reachable by both weapons.");
            Assert.That(plate[0].distance, Is.LessThan(firstBodies[0].distance));
            Assert.That(firstBodies.Max(hit => hit.distance), Is.LessThan(secondBodies[0].distance));
            Assert.That(secondBodies.Max(hit => hit.distance), Is.LessThan(Mathf.Min(sniper.Range, pistol.Range) - .5f));

            adapter = new ProductionOrbitalCombatAdapter(root.transform, null);
            int firstHits = 0, secondHits = 0;
            firstHealth.HitFeedback += (_, _, _) => firstHits++;
            secondHealth.HitFeedback += (_, _, _) => secondHits++;
            foreach (float step in new[] { 1f / 30f, 1f / 120f })
            foreach (var settings in new[] { sniper, pistol })
            {
                adapter.DebugClearProjectiles();
                firstHealth.SetRuntimeMaxHealth(1000f);
                secondHealth.SetRuntimeMaxHealth(1000f);
                firstHits = secondHits = 0;
                int blocks = shield.Blocks;
                adapter.SpawnWeaponProjectile(origin, firstHealth, direction, settings, null, settings.Damage);
                Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(1));
                int frames = Mathf.CeilToInt(settings.Range / settings.ProjectileSpeed / step) + 2;
                for (int frame = 0; frame < frames; frame++) adapter.Tick(step);
                string context = $"{settings.Kind}, dt={step}";
                Assert.That(adapter.LabActiveProjectileCount, Is.Zero, context);
                bool bypass = settings == sniper;
                Assert.That(firstHealth.CurrentHealth, Is.EqualTo(1000f - (bypass ? settings.Damage : 0f)).Within(.001f), context);
                Assert.That(secondHealth.CurrentHealth, Is.EqualTo(1000f), context);
                Assert.That(firstHits, Is.EqualTo(bypass ? 1 : 0), context);
                Assert.That(secondHits, Is.Zero, context);
                Assert.That(shield.Blocks, Is.EqualTo(blocks + (bypass ? 0 : 1)), context);
            }
        }
        finally
        {
            adapter?.DebugClearProjectiles();
            adapter?.Teardown();
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    [Category("Core"), Test]
    public void RingMountAndModuleTransactionsCommitOnceAndRejectOccupiedTargets()
    {
        var state = OrbitalRunState.CreateDefault(1);
        int revision = state.Revision;
        Assert.That(state.TryAddRing(out var ring, out _), Is.True);
        Assert.That(state.AddMount(ring.StableRingId, out _), Is.True);
        Assert.That(ring.MountCount, Is.EqualTo(2));
        Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter,
            ring.StableRingId, 0, out var module), Is.True);
        Assert.That(state.Revision, Is.EqualTo(revision + 3));
        int moduleId = module.StableModuleId;
        string before = JsonUtility.ToJson(state);
        Assert.That(state.MoveModule(moduleId, 1, 0, out _), Is.False);
        Assert.That(state.InstallModule(OrbitalModuleKind.Pistol,
            ring.StableRingId, 0, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(state.MoveModule(moduleId, ring.StableRingId, 1, out _), Is.True);
        Assert.That(state.FindModule(moduleId), Is.SameAs(module));
        Assert.That(state.IsMountFree(ring.StableRingId, 0), Is.True);
        Assert.That(module.MountIndex, Is.EqualTo(1));
        Assert.That(state.RemoveModule(moduleId), Is.True);
        Assert.That(state.IsMountFree(ring.StableRingId, 1), Is.True);
        Assert.That(state.NextStableModuleId, Is.EqualTo(moduleId + 1));
        Assert.That(state.Revision, Is.EqualTo(revision + 5));
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), Test]
    public void LinkPairCommitsAtomicallyPreservesPartnerOnMoveAndUpgradesCore()
    {
        var state = OrbitalRunState.CreateDefault(2);
        Assert.That(state.AddMount(1, out _), Is.True);
        Assert.That(state.AddMount(1, out _), Is.True);
        var ring = state.AddRing();
        Assert.That(ring, Is.Not.Null);
        string before = JsonUtility.ToJson(state);
        Assert.That(state.InstallLinkPair(1, 1, 1, 1, out _, out _, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        int revision = state.Revision, nextId = state.NextStableModuleId;
        Assert.That(state.InstallLinkPair(1, 1, 1, 2, out var a, out var b, out _), Is.True);
        Assert.That(state.Revision, Is.EqualTo(revision + 1));
        Assert.That(state.NextStableModuleId, Is.EqualTo(nextId + 2));
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.MoveModule(a.StableModuleId, ring.StableRingId, 0, out _), Is.True);
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));

        Assert.That(state.UpgradeLinkMatrix(), Is.True);
        Assert.That(state.LinkMatrixUpgradeLevel, Is.EqualTo(1));
        Assert.That(state.RemoveModule(a.StableModuleId), Is.True);
        Assert.That(state.FindLinkPartner(b.StableModuleId), Is.Zero);
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), Test]
    public void VikaCustomPathsFinalizeMultipleRingsInCreationOrder()
    {
        var state = OrbitalRunState.CreateDefault(3, customPaths: true);
        var first = state.NextPendingRing;
        Assert.That(state.TryAddRing(out var second, out _), Is.True);
        Assert.That(CustomOrbitPath.TryRestore(new[] {
            new Vector2(0f, .8f), new Vector2(.8f, 0f),
            new Vector2(0f, -.8f), new Vector2(-.8f, 0f)
        }, out var firstPath), Is.True);
        Assert.That(CustomOrbitPath.TryRestore(new[] {
            new Vector2(0f, 1.4f), new Vector2(1.2f, .2f),
            new Vector2(.1f, -1.1f), new Vector2(-1.3f, -.1f)
        }, out var secondPath), Is.True);
        Assert.That(state.PendingRingCount, Is.EqualTo(2));
        string before = JsonUtility.ToJson(state);
        Assert.That(state.TrySetCustomPath(second.StableRingId, secondPath, 2f, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(state.TrySetCustomPath(first.StableRingId, firstPath, 2f, out _), Is.True);
        Assert.That(state.NextPendingRing, Is.SameAs(second));
        Assert.That(state.TrySetCustomPath(second.StableRingId, secondPath, 2f, out _), Is.True);
        Assert.That(first.CustomPath, Is.EqualTo(firstPath.CapturePoints()));
        Assert.That(second.CustomPath, Is.EqualTo(secondPath.CapturePoints()));
        Assert.That(state.PendingRingCount, Is.Zero);
        Assert.That(state.NextPendingRing, Is.Null);
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), UnityTest]
    public IEnumerator SectorRestorePreservesCompleteOrbitalStateAndRebuildsOccupancy()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        var manager = RunStateManager.EnsureExists();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        var player = new GameObject("Orbital restore test player");
        try
        {
            manager.BeginNewRun(null, null, stage, rule, anomaly);
            var state = manager.OrbitalStationState;
            var second = state.AddRing();
            Assert.That(second, Is.Not.Null);
            Assert.That(state.AddMount(1, out _), Is.True);
            Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter,
                second.StableRingId, 0, out var arc), Is.True);
            Assert.That(state.MoveModule(arc.StableModuleId, 1, 1, out _), Is.True);
            Assert.That(state.UpgradeModuleDamage(arc.StableModuleId), Is.True);
            Assert.That(state.UpgradeRingPower(1), Is.True);
            Assert.That(state.UpgradeRingSpeed(second.StableRingId), Is.True);

            Assert.That(state.AddMount(second.StableRingId, out _), Is.True);
            Assert.That(state.InstallLinkPair(second.StableRingId, 0, second.StableRingId, 1, out _, out _, out _), Is.True);
            Assert.That(state.UpgradeLinkMatrix(), Is.True);
            state.SetPhase(1, 17.5f);
            state.SetPhase(second.StableRingId, 81.25f);
            string before = JsonUtility.ToJson(state);
            var station = OrbitalStationRuntime.CreateFromState(player, state);
            Assert.That(station, Is.Not.Null);
            Assert.That(station.IsInitialized, Is.True);
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(station.CaptureState(), Is.SameAs(state));
            Assert.That(manager.OrbitalStationState, Is.SameAs(state));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(station.Rings.Count, Is.EqualTo(state.Rings.Count));
            Assert.That(station.Modules.Count, Is.EqualTo(state.Modules.Count));
            foreach (var module in state.Modules)
            {
                var restored = station.Modules.Single(m => m.StableModuleId == module.StableModuleId);
                Assert.That(restored.Kind, Is.EqualTo(module.ModuleType));
                Assert.That(restored.CurrentMount.Ring.RingId, Is.EqualTo(module.StableRingId));
                Assert.That(restored.CurrentMount.MountIndex, Is.EqualTo(module.MountIndex));
            }
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(rule);
            Object.DestroyImmediate(anomaly);
        }

    }
}
