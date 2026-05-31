using TMPro;
using UnityEngine;

public class GameUpgradeDescriptionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text descriptionText;

    public void Show(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        if (descriptionText != null)
            descriptionText.text = upgrade.description;
    }

    public void Clear()
    {
        if (descriptionText != null)
            descriptionText.text = "";
    }
}