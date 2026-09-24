#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RewardStylePresentationTests
{
    private const string Output = "Artifacts/GeneratedQA/RewardStyle";
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator BeginRun() => CoreTestSupport.BeginRun();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator AllProductionCardsAndButtonStatesAt1080p()
    {
        Application.runInBackground = true;
        // This fixture captures reward presentation only; exclude guidance from its screenshots.
        if (TutorialController.Active != null) TutorialController.Active.enabled = false;
        Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(1920, 1080)));
        LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        Time.timeScale = 0;
        var panel = Object.FindFirstObjectByType<UpgradePanelView>(FindObjectsInactive.Include);
        var cards = panel.GetComponentsInChildren<UpgradeCardView>(true);
        Assert.That(cards.Length, Is.EqualTo(3));
        using var provider = new OrbitalRewardProvider(UpgradeManager.Instance.AllUpgrades.ToArray());
        var state = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(RunStateManager.Instance.OrbitalStationState));
        var rewards = new List<UpgradeData>();
        foreach (OrbitalRewardKind kind in Enum.GetValues(typeof(OrbitalRewardKind)))
            if (OrbitalRewardProvider.IsDemoReward(kind) && kind != OrbitalRewardKind.CoreUpgrade)
            {
                var presentationState = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
                // Capacity cards are offered only after all mounts have been built.
                if (kind == OrbitalRewardKind.RingCapacity)
                    presentationState.Rings[0].MountCount = presentationState.Rings[0].MountCapacity;
                rewards.Add(Object.Instantiate(provider.GetDefinition(kind, presentationState)));
            }
        for (int level = 0; level < 3; level++)
        {
            state.CoreState.Level = level;
            rewards.Add(Object.Instantiate(provider.GetDefinition(OrbitalRewardKind.CoreUpgrade, state)));
        }
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("docs");
        File.WriteAllLines("docs/reward-card-texts.ru.txt", rewards.Select(reward =>
            Plain(LocalizationService.Instance.Get(reward.upgradeName)) + " — " +
            Plain(ProductionUpgradePresentation.GetCardDescription(reward))));

        UpgradeData clicked = null;
        var samples = new[] { rewards.First(r => ((OrbitalRewardData)r).RewardKind == OrbitalRewardKind.Pistol),
            rewards.First(r => ((OrbitalRewardData)r).RewardKind == OrbitalRewardKind.RingPower),
            rewards.First(r => ((OrbitalRewardData)r).RewardKind == OrbitalRewardKind.MoveSpeed) };
        panel.Show(2, samples, reward => clicked = reward);
        yield return new WaitForSecondsRealtime(.4f);
        yield return Capture(panel, "01-normal");
        var button = cards[0].GetComponent<Button>();
        var eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(cards[0].gameObject, eventData, ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(.25f);
        yield return Capture(panel, "02-hover");
        ExecuteEvents.Execute(cards[0].gameObject, eventData, ExecuteEvents.pointerExitHandler);
        EventSystem.current.SetSelectedGameObject(cards[0].gameObject);
        yield return new WaitForSecondsRealtime(.25f);
        yield return Capture(panel, "03-selected");
        button.interactable = false;
        yield return new WaitForSecondsRealtime(.25f);
        yield return Capture(panel, "04-unavailable");
        button.interactable = true;
        EventSystem.current.SetSelectedGameObject(null);
        button.onClick.Invoke();
        Assert.That(clicked, Is.SameAs(samples[0]), "Presentation must retain the selected card callback.");
        for (int offset = 0; offset < rewards.Count; offset += 3)
        {
            panel.Show(2, rewards.Skip(offset).Take(3).ToArray(), _ => { });
            yield return new WaitForSecondsRealtime(.3f);
            yield return Capture(panel, "catalog-" + (offset / 3 + 1));
        }
        panel.ShowWorldEventReward("reward.panel.title", "reward.panel.choose", samples, _ => { });
        yield return new WaitForSecondsRealtime(1f);
        yield return Capture(panel, "05-event-reward");
        panel.Hide();
        foreach (var reward in rewards) Object.Destroy(reward);
    }

    private static string Plain(string text) => Regex.Replace(Regex.Replace(text, "<[^>]+>", ""), @"\s+", " ").Trim();

    private static IEnumerator Capture(UpgradePanelView panel, string name)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing, Is.False, name + ": " + text.text);
            Assert.That(text.isTextTruncated, Is.False, name + ": truncated " + text.text);
        }
        yield return null;
        ScreenCapture.CaptureScreenshot(Output + "/" + name + ".png");
        yield return null;
    }
}
#endif
