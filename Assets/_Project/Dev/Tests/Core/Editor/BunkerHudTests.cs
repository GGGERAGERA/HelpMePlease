#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

public sealed class BunkerHudTests
{
    [Test]
    public void ExistingCurrencyAndProgressionEventsRefreshHiddenHudAndModesRestoreIt()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var metaInstance = typeof(MetaProgressionManager).GetField("<Instance>k__BackingField", flags);
        var localizationInstance = typeof(LocalizationService).GetField("<Instance>k__BackingField", flags);
        var oldCurrency = CurrencyManager.Instance;
        var oldMeta = MetaProgressionManager.Instance;
        var oldLocalization = LocalizationService.Instance;
        bool hadGold = PlayerPrefs.HasKey("TOTAL_GOLD");
        int savedGold = PlayerPrefs.GetInt("TOTAL_GOLD");
        bool hadAccess = PlayerPrefs.HasKey(MetaProgressionManager.EscapeAccessKey);
        int savedAccess = PlayerPrefs.GetInt(MetaProgressionManager.EscapeAccessKey);
        float timeScale = Time.timeScale;
        var services = new GameObject("Bunker HUD test services");
        services.SetActive(false);
        var currency = services.AddComponent<CurrencyManager>();
        var meta = services.AddComponent<MetaProgressionManager>();
        var localization = services.AddComponent<LocalizationService>();
        var localizationData = new SerializedObject(localization);
        localizationData.FindProperty("table").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
            "Assets/_Project/Data/Localization/LocalizationTable.asset");
        localizationData.ApplyModifiedPropertiesWithoutUndo();
        CurrencyManager.Instance = currency;
        metaInstance.SetValue(null, meta);
        localizationInstance.SetValue(null, localization);
        PlayerPrefs.SetInt(MetaProgressionManager.EscapeAccessKey, 0);
        var root = PrefabUtility.LoadPrefabContents("Assets/_Project/prefabs/UI/Bunker/BunkerHUD.prefab");
        var view = root.GetComponent<BunkerHudView>();
        var gameRoot = new GameObject("Bunker HUD minigame probe");
        var game = gameRoot.AddComponent<BunkerHudProbeMinigame>();
        var viewData = new SerializedObject(view);
        var games = viewData.FindProperty("minigames");
        games.arraySize = 1;
        games.GetArrayElementAtIndex(0).objectReferenceValue = game;
        viewData.ApplyModifiedPropertiesWithoutUndo();
        var number = root.GetComponentInChildren<HudIconNumber>().GetComponentInChildren<TMP_Text>();
        var group = root.GetComponent<CanvasGroup>();
        var goal = (GameObject)viewData.FindProperty("objectiveRoot").objectReferenceValue;
        void Invoke(string name) => typeof(BunkerHudView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
        try
        {
            Invoke("OnEnable");
            Assert.That(number.text, Is.EqualTo("0"));
            Assert.That(goal.activeSelf, Is.True);
            Time.timeScale = 1f;
            Invoke("LateUpdate");
            Assert.That(group.alpha, Is.EqualTo(1f));
            game.StartGame();
            Invoke("LateUpdate");
            Assert.That(group.alpha, Is.Zero);
            currency.AddGoldExact(1000);
            currency.SpendGold(250);
            Assert.That(number.text, Is.EqualTo("750"), "Hidden HUD still observes the real balance.");
            game.CompleteGame();
            Invoke("LateUpdate");
            Assert.That(group.alpha, Is.EqualTo(1f));
            Time.timeScale = 0f;
            Invoke("LateUpdate");
            Assert.That(group.alpha, Is.Zero);
            Time.timeScale = 1f;
            Invoke("LateUpdate");
            Assert.That(group.alpha, Is.EqualTo(1f));
            meta.AcquireGuardianAccess();
            Assert.That(goal.activeSelf, Is.False, "Completing the authored stage hides the undefined next goal.");
            currency.AddGoldExact(int.MaxValue);
            Assert.That(number.text, Is.EqualTo(int.MaxValue.ToString()), "The complete supported balance must remain exact.");
            Invoke("OnDisable");
            currency.SpendGold(1);
            Assert.That(number.text, Is.EqualTo(int.MaxValue.ToString()), "Disabled presenters must unsubscribe.");
        }
        finally
        {
            Invoke("OnDisable");
            PrefabUtility.UnloadPrefabContents(root);
            Object.DestroyImmediate(gameRoot);
            Object.DestroyImmediate(services);
            CurrencyManager.Instance = oldCurrency;
            metaInstance.SetValue(null, oldMeta);
            localizationInstance.SetValue(null, oldLocalization);
            if (hadGold) PlayerPrefs.SetInt("TOTAL_GOLD", savedGold); else PlayerPrefs.DeleteKey("TOTAL_GOLD");
            if (hadAccess) PlayerPrefs.SetInt(MetaProgressionManager.EscapeAccessKey, savedAccess); else PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeAccessKey);
            PlayerPrefs.Save();
            Time.timeScale = timeScale;
        }
    }

    [Test]
    public void ProductionBunkerHasBoundNonInteractiveHud()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        try
        {
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .SingleOrDefault(component => component != null && component.GetType().Name == "BunkerHudView");
            Assert.That(hud, Is.Not.Null, "The working bunker scene must contain the HUD prefab.");
            var data = new SerializedObject(hud);
            foreach (var field in new[] { "content", "gold", "objectiveRoot", "objectiveTitle", "objectiveText", "depths", "panels", "currencyContainer" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var games = data.FindProperty("minigames");
            int sceneGames = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<BunkerMinigame>(true).Length);
            Assert.That(games.arraySize, Is.EqualTo(sceneGames));
            for (int i = 0; i < games.arraySize; i++)
                Assert.That(games.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var group = hud.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts || group.interactable, Is.False);
            Assert.That(hud.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget), Is.False);
            Assert.That(hud.GetComponentInParent<CanvasScaler>().uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(hud.GetComponentsInChildren<HudIconNumber>(true).Length, Is.EqualTo(1), "No future currency placeholders.");
            var text = (TMP_Text)data.FindProperty("objectiveText").objectReferenceValue;
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void EscapeStagesOnlyExposeAuthoredGlobalObjectives()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DepthCatalog>("Assets/_Project/Data/Stages/DepthCatalog.asset");
        var resolve = typeof(DepthCatalog).GetMethod("GetGlobalObjectiveKey");
        Assert.That(resolve, Is.Not.Null, "Resolve the goal from the existing depth progression data.");
        string first = (string)resolve.Invoke(catalog, new object[] { 0 });
        Assert.That(first, Is.Not.Null.And.Not.Empty);
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset");
        Assert.That(table.ContainsKey(first), Is.True);
        Assert.That((string)resolve.Invoke(catalog, new object[] { 1 }), Is.Null.Or.Empty,
            "There is no authored next global goal in the current demo.");
        Assert.That((string)resolve.Invoke(catalog, new object[] { MetaProgressionManager.EscapeAccessRequired }), Is.Null.Or.Empty);
    }
}

public sealed class BunkerHudProbeMinigame : BunkerMinigame { }

public sealed class BunkerHudPlayTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator ProductionBunkerShowsHudAndRestoresAfterClosingSettingsAndPause()
    {
        PlayerPrefs.SetInt(MetaProgressionManager.EscapeAccessKey, 0);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return CoreTestSupport.LoadBunker();
        var hud = Object.FindFirstObjectByType<BunkerHudView>();
        Assert.That(hud, Is.Not.Null);
        yield return null;
        var group = hud.GetComponent<CanvasGroup>();
        Assert.That(group.alpha, Is.EqualTo(1f));
        var data = new SerializedObject(hud);
        var goal = (GameObject)data.FindProperty("objectiveRoot").objectReferenceValue;
        Assert.That(goal.activeInHierarchy, Is.True);
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        panels.OpenSettings();
        yield return null;
        Assert.That(group.alpha, Is.Zero);
        panels.CloseAll(false);
        yield return null;
        Assert.That(group.alpha, Is.EqualTo(1f));
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        yield return null;
        Assert.That(group.alpha, Is.Zero);
        pause.Resume();
        yield return null;
        Assert.That(group.alpha, Is.EqualTo(1f));
        Directory.CreateDirectory("Artifacts/GeneratedQA/BunkerHud");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/BunkerHud/bunker-hud.png");
        yield return new WaitForSecondsRealtime(.5f);
    }
}
#endif
