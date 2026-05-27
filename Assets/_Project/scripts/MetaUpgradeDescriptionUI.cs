using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaUpgradeDescriptionUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI descriptionText;

    public void Show(Sprite icon, string description)
    {
        if (iconImage != null)
            iconImage.sprite = icon;

        if (descriptionText != null)
            descriptionText.text = description;
    }
}
