using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UpgradeButtonUI : MonoBehaviour, IPointerEnterHandler
{
    [Header("UI References")]
    public Button button;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public Image iconImage;

    private UpgradeData currentUpgrade;
    private UpgradeManager upgradeManager;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Setup(UpgradeData upgrade, UpgradeManager manager)
    {
        currentUpgrade = upgrade;
        upgradeManager = manager;

        if (titleText != null)
            titleText.text = upgrade.upgradeName;

        if (iconImage != null)
        {
            iconImage.sprite = upgrade.icon;
            iconImage.enabled = upgrade.icon != null;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ChooseUpgrade);
        }

        gameObject.SetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowDescription();
    }

    public void ShowDescription()
    {
        if (upgradeManager == null)
            return;

        if (upgradeManager.descriptionUI == null)
            return;

        if (currentUpgrade == null)
            return;

        upgradeManager.descriptionUI.Show(currentUpgrade);
    }

    private void ChooseUpgrade()
    {
        if (upgradeManager != null && currentUpgrade != null)
            upgradeManager.SelectUpgrade(currentUpgrade);
    }
}