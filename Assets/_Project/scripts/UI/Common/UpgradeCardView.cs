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

    [Header("Category Colors")]
    [SerializeField] private Color numericColor = new Color(0.239216f, 0.552941f, 1f, 1f);
    [SerializeField] private Color behaviorColor = new Color(0.94902f, 0.662745f, 0.231373f, 1f);

    [SerializeField] private Image categoryFrameImage;

    [Header("Choice-only Decorations")]
    [SerializeField] private GameObject rarityDecorationRoot;
    [SerializeField] private Image rarityGlowImage;

    private UpgradeData currentUpgrade;
    private Action<UpgradeData> onClicked;
    private UICardHoverAnimation hoverAnimation;
    private Image iconFrameImage;
    private Color defaultIconFrameColor;
    private bool defaultIconFrameEnabled;
    private bool defaultRarityGlowEnabled;
    private bool defaultRarityDecorationActive;
    private bool visualDefaultsCaptured;
    private bool defaultIconPreserveAspect;
    private Image.Type defaultIconType;
    private Vector2 defaultIconOffsetMin;
    private Vector2 defaultIconOffsetMax;

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
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        SetText(titleText, upgrade.upgradeName);
        SetText(
            descriptionText,
            ProductionUpgradePresentation.GetCardDescription(upgrade));

        SetCategory(upgrade.category);
        if (upgrade is OrbitalRewardData orbitalReward)
        {
            OrbitalRewardIconResolver.Icon icon = OrbitalRewardIconResolver.Resolve(orbitalReward);
            SetIcon(icon.Sprite, icon.Tint);
            if (iconImage != null)
            {
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
                // Inset only the image inside the existing upper icon frame.
                iconImage.rectTransform.offsetMin = defaultIconOffsetMin + Vector2.one * 16f;
                iconImage.rectTransform.offsetMax = defaultIconOffsetMax - Vector2.one * 16f;
            }
        }
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
        if (iconImage == null)
            return;

        iconImage.sprite = icon;
        iconImage.color = tint;
        iconImage.enabled = icon != null;
    }

    private void CaptureVisualDefaults()
    {
        if (visualDefaultsCaptured)
            return;

        if (iconImage != null && iconImage.transform.parent != null)
            iconFrameImage = iconImage.transform.parent.GetComponent<Image>();

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

    private void SetCategory(UpgradeCategory category)
    {
        Color color = GetCategoryColor(category);

        if (categoryFrameImage != null)
            categoryFrameImage.color = color;
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
            text.text = value;
    }
}
