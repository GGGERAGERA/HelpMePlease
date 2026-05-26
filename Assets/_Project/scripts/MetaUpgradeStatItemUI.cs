using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaUpgradeStatItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI levelText;

    public void Setup(Sprite icon, string upgradeName, int level)
    {
        if (iconImage != null)
            iconImage.sprite = icon;

        if (levelText != null)
            levelText.text = $"{upgradeName}: {level}";
    }
}