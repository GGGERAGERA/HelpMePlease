using System;
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
    [SerializeField] private Image modeFrameImage;

    private UpgradeData currentUpgrade;
    private Action<UpgradeData> onClicked;
    private UICardHoverAnimation hoverAnimation;
    private Image iconFrameImage;
    private Color defaultIconFrameColor;
    private Color defaultModeFrameColor;
    private bool defaultModeFrameEnabled;
    private bool defaultIconFrameEnabled;
    private bool defaultRarityGlowEnabled;
    private bool defaultRarityDecorationActive;
    private bool visualDefaultsCaptured;

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
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.RemoveListener(HandleChoiceClick);
        }
    }

    public void Setup(UpgradeData upgrade, Action<UpgradeData> clickCallback)
    {
        RestoreChoiceVisuals();
        currentUpgrade = upgrade;
        onClicked = clickCallback;
        choiceClicked = null;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleChoiceClick);
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
        SetText(descriptionText, upgrade.description);

        SetCategory(upgrade.category);
        SetIcon(upgrade.icon, GetCategoryColor(upgrade.category));
        hoverAnimation?.RefreshRestingState();
    }

    public void SetupChoice(
        string title,
        string description,
        UpgradeCategory category,
        Sprite icon,
        Color headerTint,
        Action clickCallback
    )
    {
        RestoreChoiceVisuals();
        currentUpgrade = null;
        onClicked = null;
        gameObject.SetActive(true);

        SetText(titleText, title);
        SetText(descriptionText, description);
        SetCategory(category);
        SetIcon(icon, headerTint);
        ApplyChoiceVisuals(headerTint);

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.RemoveListener(HandleChoiceClick);
            button.onClick.AddListener(HandleChoiceClick);
        }

        choiceClicked = clickCallback;
        hoverAnimation?.RefreshRestingState();
    }

    private Action choiceClicked;

    private void HandleChoiceClick()
    {
        choiceClicked?.Invoke();
    }

    public void ClearChoiceCallback()
    {
        choiceClicked = null;

        if (button != null)
            button.onClick.RemoveListener(HandleChoiceClick);

        RestoreChoiceVisuals();
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

        if (iconFrameImage != null)
        {
            defaultIconFrameColor = iconFrameImage.color;
            defaultIconFrameEnabled = iconFrameImage.enabled;
        }

        if (modeFrameImage != null)
        {
            defaultModeFrameColor = modeFrameImage.color;
            defaultModeFrameEnabled = modeFrameImage.enabled;
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

        if (iconFrameImage != null)
        {
            iconFrameImage.color = defaultIconFrameColor;
            iconFrameImage.enabled = defaultIconFrameEnabled;
        }

        if (modeFrameImage != null)
        {
            modeFrameImage.color = defaultModeFrameColor;
            modeFrameImage.enabled = defaultModeFrameEnabled;
        }

        if (rarityGlowImage != null)
            rarityGlowImage.enabled = defaultRarityGlowEnabled;

        if (rarityDecorationRoot != null)
            rarityDecorationRoot.SetActive(defaultRarityDecorationActive);
    }

    private void ApplyChoiceVisuals(Color frameColor)
    {
        if (rarityDecorationRoot != null)
            rarityDecorationRoot.SetActive(false);

        if (rarityGlowImage != null)
            rarityGlowImage.enabled = false;

        if (iconFrameImage != null)
            iconFrameImage.enabled = false;

        if (modeFrameImage != null)
        {
            modeFrameImage.color = frameColor;
            modeFrameImage.enabled = true;
        }
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
