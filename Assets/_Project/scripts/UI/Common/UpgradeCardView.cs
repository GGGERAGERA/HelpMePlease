using System;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeCardView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;

    [Header("Content")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rewardTypeLabel;
    [SerializeField] private TextMeshProUGUI rewardDetailLabel;
    [SerializeField] private Vector4 compactDescriptionMargins;

    [Header("Category Colors")]
    [SerializeField] private Color numericColor = new Color(0.239216f, 0.552941f, 1f, 1f);
    [SerializeField] private Color behaviorColor = new Color(0.94902f, 0.662745f, 0.231373f, 1f);

    [SerializeField] private Image categoryFrameImage;

    [Header("Choice-only Decorations")]
    [SerializeField] private GameObject rarityDecorationRoot;
    [SerializeField] private Image rarityGlowImage;

    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }
    private void RefreshLanguage(GameLanguage language)
    {
        if (currentUpgrade is OrbitalRewardData orbital)
            orbital.PresentationOwner?.RefreshPresentation(RunStateManager.Instance?.OrbitalStationState);
        if (currentUpgrade != null) Setup(currentUpgrade, onClicked);
    }
    private UpgradeData currentUpgrade;
    private Action<UpgradeData> onClicked;
    private UICardHoverAnimation hoverAnimation;
    [SerializeField] private Image iconFrameImage;
    private Color defaultIconFrameColor;
    private bool defaultIconFrameEnabled;
    private bool defaultRarityGlowEnabled;
    private bool defaultRarityDecorationActive;
    private bool visualDefaultsCaptured;
    private bool defaultIconPreserveAspect;
    private Image.Type defaultIconType;
    private Vector2 defaultIconOffsetMin;
    private Vector2 defaultIconOffsetMax;
    private Vector4 defaultDescriptionMargins;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        hoverAnimation = GetComponent<UICardHoverAnimation>();
        CaptureVisualDefaults();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Setup(UpgradeData upgrade, Action<UpgradeData> clickCallback)
    {
        // A card can be reused after an interrupted world-reward reveal.
        var group = GetComponent<CanvasGroup>();
        if (group != null) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; }
        if (button != null) button.interactable = upgrade != null;
        RestoreChoiceVisuals();
        currentUpgrade = upgrade;
        onClicked = clickCallback;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        if (upgrade == null)
        {
            SetText(titleText, string.Empty);
            SetText(descriptionText, string.Empty);
            SetCompactLabels(null);
            SetIcon(null, Color.white);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        var orbitalReward = upgrade as OrbitalRewardData;
        bool compact = orbitalReward != null && orbitalReward.CardType != RigRewardCardType.None;
        if (descriptionText != null)
            descriptionText.margin = compact ? compactDescriptionMargins : defaultDescriptionMargins;
        SetText(titleText, LocalizationService.EnsureExists().Get(upgrade.upgradeName));
        SetText(
            descriptionText,
            (!compact && orbitalReward != null ? OrbitalCategories.Label(orbitalReward.EffectCategory) + "\n\n" : "") +
            ProductionUpgradePresentation.GetCardDescription(upgrade));

        SetCompactLabels(orbitalReward);
        if (button is RewardCardButton rewardButton) rewardButton.SetReward(upgrade);
        if (categoryFrameImage != null)
            categoryFrameImage.color = compact && button != null ? button.colors.normalColor : GetCategoryColor(upgrade.category);

        if (orbitalReward != null)
            SetOrbitalIcon(OrbitalRewardIconResolver.Resolve(orbitalReward));
        else if (upgrade is TelekineticRankRewardData rankReward)
            SetOrbitalIcon(OrbitalRewardIconResolver.Resolve(rankReward.Part));
        else
            SetIcon(upgrade.icon, GetCategoryColor(upgrade.category));
        hoverAnimation?.RefreshRestingState();
    }

    private void HandleClick()
    {
        if (currentUpgrade == null)
            return;

        onClicked?.Invoke(currentUpgrade);
    }

    private void SetIcon(Sprite icon, Color tint)
    {
        if (iconImage != null) OrbitalRewardIconView.Set(iconImage, new OrbitalRewardIconResolver.Icon(icon, tint));
    }

    private void SetOrbitalIcon(OrbitalRewardIconResolver.Icon icon)
    {
        if (iconImage == null) return;
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        // Inset only the image inside the existing upper icon frame.
        iconImage.rectTransform.offsetMin = defaultIconOffsetMin + Vector2.one * 16f;
        iconImage.rectTransform.offsetMax = defaultIconOffsetMax - Vector2.one * 16f;
        OrbitalRewardIconView.Set(iconImage, icon);
    }

    private void CaptureVisualDefaults()
    {
        if (visualDefaultsCaptured)
            return;

        if (iconFrameImage == null && iconImage != null && iconImage.transform.parent != null)
            iconFrameImage = iconImage.transform.parent.GetComponent<Image>();

        if (descriptionText != null) defaultDescriptionMargins = descriptionText.margin;

        if (iconImage != null)
        {
            defaultIconPreserveAspect = iconImage.preserveAspect;
            defaultIconType = iconImage.type;
            defaultIconOffsetMin = iconImage.rectTransform.offsetMin;
            defaultIconOffsetMax = iconImage.rectTransform.offsetMax;
        }

        if (iconFrameImage != null)
        {
            defaultIconFrameColor = iconFrameImage.color;
            defaultIconFrameEnabled = iconFrameImage.enabled;
        }

        if (rarityGlowImage != null)
            defaultRarityGlowEnabled = rarityGlowImage.enabled;

        if (rarityDecorationRoot != null)
            defaultRarityDecorationActive = rarityDecorationRoot.activeSelf;

        visualDefaultsCaptured = true;
    }

    private void RestoreChoiceVisuals()
    {
        CaptureVisualDefaults();

        if (iconImage != null)
        {
            iconImage.preserveAspect = defaultIconPreserveAspect;
            iconImage.type = defaultIconType;
            iconImage.rectTransform.offsetMin = defaultIconOffsetMin;
            iconImage.rectTransform.offsetMax = defaultIconOffsetMax;
        }

        if (iconFrameImage != null)
        {
            iconFrameImage.color = defaultIconFrameColor;
            iconFrameImage.enabled = defaultIconFrameEnabled;
        }

        if (rarityGlowImage != null)
            rarityGlowImage.enabled = defaultRarityGlowEnabled;

        if (rarityDecorationRoot != null)
            rarityDecorationRoot.SetActive(defaultRarityDecorationActive);
    }

    private void SetCompactLabels(OrbitalRewardData reward)
    {
        var type = reward?.CardType ?? RigRewardCardType.None;
        if (rewardTypeLabel != null)
        {
            rewardTypeLabel.gameObject.SetActive(type != RigRewardCardType.None);
            if (type != RigRewardCardType.None)
            {
                var (key, accent) = type switch
                {
                    RigRewardCardType.Weapon => ("reward.rig.type.weapon", new Color32(0xE6, 0xA2, 0x65, 255)),
                    RigRewardCardType.Module => ("reward.rig.type.module", new Color32(0xB3, 0x96, 0xF5, 255)),
                    RigRewardCardType.Upgrade => ("reward.rig.type.upgrade", new Color32(0x74, 0xC9, 0xA6, 255)),
                    _ => ("reward.rig.type.construction", new Color32(0x68, 0xCB, 0xD9, 255))
                };
                SetText(rewardTypeLabel, LocalizationService.EnsureExists().Get(key));
                rewardTypeLabel.color = accent;
            }
        }

        if (rewardDetailLabel != null)
        {
            bool visible = type != RigRewardCardType.None && !string.IsNullOrEmpty(reward.CardDetail);
            rewardDetailLabel.gameObject.SetActive(visible);
            if (visible) SetText(rewardDetailLabel, reward.CardDetail);
        }
    }

    private Color GetCategoryColor(UpgradeCategory category)
    {
        return category switch
        {
            UpgradeCategory.Numeric => numericColor,
            UpgradeCategory.Behavior => behaviorColor,
            _ => numericColor
        };
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.enabled = true;
            text.alpha = 1f;
            text.text = value ?? string.Empty;
        }
    }
}
