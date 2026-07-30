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

    private UpgradeData currentUpgrade;
    private Action<UpgradeData> onClicked;
    private UICardHoverAnimation hoverAnimation;
    private Image iconFrameImage;
    private Color defaultIconFrameColor;
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

        SetIcon(upgrade.icon);
        SetCategory(upgrade.category);
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
        SetIcon(icon);
        SetCategory(category);

        if (iconFrameImage != null)
            iconFrameImage.color = headerTint;

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

    private void SetIcon(Sprite icon)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = icon;
        iconImage.color = Color.white;
        iconImage.enabled = icon != null;
    }

    private void CaptureVisualDefaults()
    {
        if (visualDefaultsCaptured)
            return;

        if (iconImage != null && iconImage.transform.parent != null)
            iconFrameImage = iconImage.transform.parent.GetComponent<Image>();

        if (iconFrameImage != null)
            defaultIconFrameColor = iconFrameImage.color;

        visualDefaultsCaptured = true;
    }

    private void RestoreChoiceVisuals()
    {
        CaptureVisualDefaults();

        if (iconFrameImage != null)
            iconFrameImage.color = defaultIconFrameColor;
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
