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

    [Header("Rarity Colors")]
    [SerializeField] private Color grayColor = new Color(0.654902f, 0.705882f, 0.760784f, 1f);
    [SerializeField] private Color blueColor = new Color(0.239216f, 0.552941f, 1f, 1f);
    [SerializeField] private Color purpleColor = new Color(0.658824f, 0.333333f, 0.968627f, 1f);
    [SerializeField] private Color legendaryColor = new Color(0.94902f, 0.662745f, 0.231373f, 1f);

    [SerializeField] private Image rarityFrameImage;

    private UpgradeData currentUpgrade;
    private Action<UpgradeData> onClicked;
    private UICardHoverAnimation hoverAnimation;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        hoverAnimation = GetComponent<UICardHoverAnimation>();

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
        currentUpgrade = upgrade;
        onClicked = clickCallback;

        if (upgrade == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        SetText(titleText, upgrade.upgradeName);
        SetText(descriptionText, upgrade.description);

        SetIcon(upgrade.icon);
        SetRarity(upgrade.rarity);
        hoverAnimation?.RefreshRestingState();
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
        iconImage.enabled = icon != null;
    }

    private void SetRarity(UpgradeRarity rarity)
    {
        Color color = GetRarityColor(rarity);

        if (rarityFrameImage != null)
            rarityFrameImage.color = color;
    }

    private Color GetRarityColor(UpgradeRarity rarity)
    {
        return rarity switch
        {
            UpgradeRarity.Gray => grayColor,
            UpgradeRarity.Blue => blueColor,
            UpgradeRarity.Purple => purpleColor,
            UpgradeRarity.Legendary => legendaryColor,
            _ => grayColor
        };
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
