using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeButtonUI : MonoBehaviour
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

        if (descriptionText != null)
            descriptionText.text = upgrade.description;

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

    private void ChooseUpgrade()
    {
        if (upgradeManager != null && currentUpgrade != null)
        {
            upgradeManager.SelectUpgrade(currentUpgrade);
        }
    }
}
