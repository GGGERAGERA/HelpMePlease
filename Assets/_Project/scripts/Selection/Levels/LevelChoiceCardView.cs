using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum LevelChoiceCardPresentationMode
{
    Default = 0,
    SectorChoice = 1
}

public sealed class LevelChoiceCardView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private LevelChoiceCardPresentationMode presentationMode;

    [Header("Selection")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private Image accentHeaderImage;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI tagText;
    [SerializeField] private TextMeshProUGUI rewardText;

    private RunSector displayedSector;
    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }
    private void RefreshLanguage(GameLanguage language)
    {
        if (worldRuleData != null) ApplySectorPresentation(worldRuleData, displayedSector);
    }
    private WorldRuleData worldRuleData;
    private Action<WorldRuleData> onRuleClicked;
    private UICardHoverAnimation hoverAnimation;
    private Color sectorAccentColor = Color.white;

    public WorldRuleData Rule => worldRuleData;
    public LevelChoiceCardPresentationMode PresentationMode =>
        presentationMode;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        hoverAnimation = GetComponent<UICardHoverAnimation>();

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);

        ConfigureText(titleText, 26f, 38f, 1);
        ConfigureText(descriptionText, 17f, 22f, 2);
        ConfigureText(tagText, 15f, 19f, 2);
        ConfigureText(rewardText, 15f, 19f, 3);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void SetupSectorChoice(
        WorldRuleData rule,
        RunSector sector,
        Action<WorldRuleData> clickCallback)
    {
        presentationMode = LevelChoiceCardPresentationMode.SectorChoice;
        worldRuleData = rule;
        displayedSector = sector;
        onRuleClicked = clickCallback;
        gameObject.SetActive(rule != null);

        if (rule == null)
            return;

        ApplySectorPresentation(rule, sector);
        SetSelected(false);
    }

    private void ApplySectorPresentation(
        WorldRuleData rule,
        RunSector sector)
    {
        sectorAccentColor = rule.PresentationColor;
        SetText(titleText, rule.DisplayName);
        SetText(descriptionText, rule.ShortDescription);
        SetText(tagText, BuildEffectText(rule));
        SetText(rewardText, BuildRewardText(rule, sector));
        SetIcon(rule.Icon, sectorAccentColor);

        if (titleText != null)
            titleText.color = sectorAccentColor;

        if (accentHeaderImage != null)
            accentHeaderImage.color = sectorAccentColor;

        if (glowImage != null)
            glowImage.enabled = false;
    }

    private void SetIcon(Sprite icon, Color tint)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = icon;
        iconImage.color = tint;
        iconImage.preserveAspect = true;
        iconImage.gameObject.SetActive(icon != null);
    }

    private void HandleClick()
    {
        if (worldRuleData != null)
            onRuleClicked?.Invoke(worldRuleData);
    }

    public void SetSelected(bool selected)
    {
        if (hoverAnimation != null)
            hoverAnimation.SetRestingScale(Vector3.one);
        else
            transform.localScale = Vector3.one;

        if (frameImage != null)
        {
            Color sectorFrameColor = sectorAccentColor;
            sectorFrameColor.a = selected ? 1f : 0.72f;
            frameImage.color = sectorFrameColor;
        }
    }

    private static string BuildEffectText(WorldRuleData rule)
    {
        List<string> lines = new(2);

        switch (rule.RuleType)
        {
            case WorldRuleType.Rain:
                AddLine(lines, LocalizationService.EnsureExists().Get("sector.choice.fasterEnemies"));
                AddLine(lines, LocalizationService.EnsureExists().Get("sector.choice.moreEnemies"));
                break;

            case WorldRuleType.Snow:
                AddLine(lines, LocalizationService.EnsureExists().Get("sector.choice.slowed"));
                AddLine(lines, LocalizationService.EnsureExists().Get("sector.choice.moreEnemies"));
                break;

            case WorldRuleType.Wind:
                AddLine(lines, LocalizationService.EnsureExists().Get("sector.choice.wind"));
                break;
        }

        return string.Join("\n", lines);
    }

    private static void AddLine(List<string> lines, string value)
    {
        if (lines.Count < 2 && !string.IsNullOrWhiteSpace(value))
            lines.Add(value);
    }

    private static string BuildRewardText(
        WorldRuleData rule,
        RunSector sector)
    {
        List<string> lines = new(3);

        if (rule.RuleType == WorldRuleType.Golden &&
            rule.GoldenEnemyRewardMultiplier > 1f)
        {
            lines.Add(
                LocalizationService.EnsureExists().Get("sector.choice.golden")
            );
        }

        float experienceMultiplier = sector != null
            ? sector.ExperienceGainMultiplier
            : 1f;
        float completionGoldMultiplier = sector != null
            ? sector.CompletionGoldMultiplier
            : 1f;

        if (!Mathf.Approximately(experienceMultiplier, 1f))
            lines.Add($"XP ×{FormatMultiplier(experienceMultiplier)}");

        if (!Mathf.Approximately(completionGoldMultiplier, 1f))
        {
            lines.Add(
                LocalizationService.EnsureExists().Get("sector.choice.gold") +
                FormatMultiplier(completionGoldMultiplier)
            );
        }

        if (lines.Count == 0)
            return LocalizationService.EnsureExists().Get("sector.choice.standard");

        return string.Join("\n", lines);
    }

    private static string FormatMultiplier(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        float minimumSize,
        float maximumSize,
        int maximumLines)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.maxVisibleLines = maximumLines;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text == null)
            return;

        text.text = value;
        text.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
    }
}
