#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static partial class MvpF1Verification
{
    public const string Output = "Artifacts/MvpDebugQA";
    static MvpF1Verification()
    {
        EditorApplication.update += Poll;
        EditorApplication.update += PollPlay;
        EditorApplication.playModeStateChanged += PlayState;
        Application.logMessageReceived += PlayLog;
    }
    private static void Poll()
    {
        if (!File.Exists(Output + "/audit.request") || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Output + "/audit.request");
        try { Audit(); }
        catch (Exception e) { File.WriteAllText(Output + "/audit.txt", e.ToString()); }
    }
    public static void Audit()
    {
        Directory.CreateDirectory(Output);
        OrbitalPresentationConfig.Configure(AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset"));
        var previousLocalization = LocalizationService.Instance;
        var localization = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bootstrap/LocalizationService.prefab"));
        typeof(LocalizationService).GetProperty("Instance").SetValue(null, localization.GetComponent<LocalizationService>());
        var state = OrbitalRunState.CreateDefault(71);
        var body = new[] {
            AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/_Project/Data/Upgrades/Gray/HP.asset"),
            AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/_Project/Data/Upgrades/Gray/Move Speed.asset") };
        var lines = new List<string>();
        try
        {
        foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
        {
        typeof(LocalizationService).GetProperty("CurrentLanguage").SetValue(localization.GetComponent<LocalizationService>(), language);
        using (var provider = new OrbitalRewardProvider(body))
            foreach (OrbitalRewardKind kind in Enum.GetValues(typeof(OrbitalRewardKind)))
            {
                if (!OrbitalRewardProvider.IsDemoReward(kind)) continue;
                var data = provider.GetDefinition(kind, state);
                string title = LocalizationService.EnsureExists().Get(data.upgradeName);
                string description = ProductionUpgradePresentation.GetCardDescription(data);
                bool valid = !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(description) &&
                    !title.StartsWith("reward.") && !description.StartsWith("reward.");
                lines.Add(language + " " + (valid ? "TEXT_OK " : "TEXT_FAIL ") + kind + ": " + title + " / " + description.Replace("\n", " | "));
                lines.Add((OrbitalRewardIconResolver.Resolve(data).Sprite == null ? "MISSING_ICON " : "ICON_OK ") + kind);
            }
        }
        CheckCardReuseAndLimits(body, lines);
        File.WriteAllLines(Output + "/audit.txt", lines);
        }
        finally
        {
        UnityEngine.Object.DestroyImmediate(localization);
        typeof(LocalizationService).GetProperty("Instance").SetValue(null, previousLocalization);
        }
    }

    private static void CheckCardReuseAndLimits(UpgradeData[] body, List<string> lines)
    {
        typeof(LocalizationService).GetProperty("CurrentLanguage").SetValue(LocalizationService.Instance, GameLanguage.Russian);
        using var provider = new OrbitalRewardProvider(body);
        var state = OrbitalRunState.CreateDefault(72);
        var data = provider.GetActiveCatalogue(state);
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/_UpdateUI/MVPUpgradePanel.prefab"));
        try
        {
            var panel = instance.GetComponentInChildren<UpgradePanelView>(true);
            var card = panel.CardViews[0];
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            var title = (TMPro.TextMeshProUGUI)Field(card, "titleText");
            title.enabled = false; title.alpha = 0f; title.text = "stale";
            card.Setup(data[0], null);
            if (group.alpha != 1f || !group.blocksRaycasts || !title.enabled || title.alpha != 1f || title.text != LocalizationService.Instance.Get(data[0].upgradeName))
                throw new Exception("Card did not reset interrupted alpha/text state");
            card.Setup(null, null);
            if (card.gameObject.activeSelf || !string.IsNullOrEmpty(title.text)) throw new Exception("Null card retained old content");
            Call(panel, "SetupUpgradeCards", new UpgradeData[] { data[1], null, data[0] }, null);
            if (panel.CardViews.Count(c => c.gameObject.activeSelf) != 2 || !panel.CardViews[1].gameObject.activeSelf)
                throw new Exception("Null gap was not compacted");
            Call(panel, "PrepareRewardCards"); Call(panel, "StopRewardReveal");
            card.Setup(data[2], null);
            if (group.alpha != 1f || !group.interactable || card.transform.localScale != Vector3.one)
                throw new Exception("Interrupted reveal state was not restored");
            lines.Add("EDIT_PASS: actual prefab resets hidden text/CanvasGroup, clears null content, compacts two cards and completes interrupted reveal");
            RenderCoreCard(provider.GetDefinition(OrbitalRewardKind.CoreUpgrade, state));
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        while (state.CanAddRing(out _)) state.AddRing();
        foreach (var ring in state.Rings)
        {
            while (true)
            {
                while (state.CanAddMount(ring.StableRingId, out _)) state.AddMount(ring.StableRingId, out _);
                if (!state.UpgradeRingCapacity(ring.StableRingId)) break;
            }
            while (state.UpgradeRingSpeed(ring.StableRingId)) { }
            while (state.UpgradeRingPower(ring.StableRingId)) { }
            for (int mount = 0; mount < ring.MountCount; mount++)
                if (state.CanInstallModule(OrbitalModuleKind.Pistol, ring.StableRingId, mount, out _)) state.InstallModule(OrbitalModuleKind.Pistol, ring.StableRingId, mount, out _);
        }
        while (state.UpgradeCore()) { }
        state.ShootingDamageMultiplier = state.ShootingRateMultiplier = state.ContactDamageMultiplier = state.ContactSizeMultiplier = float.MaxValue;
        var slots = new RunItemSlots();
        var choices = provider.BuildChoices(3, state, slots);
        if (choices.Count != 2 || choices.Any(d => d == null) || choices.Cast<OrbitalRewardData>().Any(d => d.BodyUpgrade == null))
            throw new Exception("Exhausted pool must return only two real body upgrades");
        if (provider.GetUnavailableReason(OrbitalRewardKind.RingSpeed, state, slots) == null ||
            provider.GetUnavailableReason(OrbitalRewardKind.NewRing, state, slots) == null ||
            provider.GetUnavailableReason(OrbitalRewardKind.AddMount, state, slots) == null)
            throw new Exception("Catalogue did not respect exhausted caps");
        lines.Add("EDIT_PASS: runtime orbit/mount/speed/power/core caps; exhausted source pool returns two real choices without placeholders");
    }

    private static void RenderCoreCard(OrbitalRewardData data)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var canvasObject = new GameObject("Card visual audit", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.position = Vector3.zero; canvas.transform.localScale = Vector3.one;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(350f, 550f);
        var card = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/_UpdateUI/PF_UpgradeCard.prefab"), canvas.transform, false).GetComponent<UpgradeCardView>();
        card.Setup(data, null);
        var rect = (RectTransform)card.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(300f, 500f); rect.localScale = Vector3.one;
        foreach (var t in canvasObject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var cameraObject = new GameObject("Card audit camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 275f;
        camera.scene = scene;
        camera.transform.position = new Vector3(0f, 0f, -10f); camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .08f, .1f);
        var texture = new RenderTexture(350, 550, 24); var pixels = new Texture2D(350, 550, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            canvas.worldCamera = camera; camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            foreach (var text in card.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) text.ForceMeshUpdate();
            camera.Render(); RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 350, 550), 0, 0); pixels.Apply();
            File.WriteAllBytes(Output + "/core-card-final.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
