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

}
#endif
