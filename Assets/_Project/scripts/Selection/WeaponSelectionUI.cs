using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSelectionUI : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Button nextButton;

    [Header("Top")]
    [SerializeField] private TextMeshProUGUI selectedWeaponText;

    [Header("Right Info Panel")]
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Weapon Stats")]
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI fireRateText;
    [SerializeField] private TextMeshProUGUI bulletsText;
    [SerializeField] private TextMeshProUGUI pierceText;
    [SerializeField] private TextMeshProUGUI specialText;

    [Header("Next Button Visual")]
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private void Start()
    {
        ClearSelection();
    }

    public void SelectWeapon(WeaponData weapon)
    {
        if (weapon == null)
            return;

        if (WeaponSelectionManager.Instance != null)
            WeaponSelectionManager.Instance.SelectWeapon(weapon);

        SetText(selectedWeaponText, "Selected Weapon: " + weapon.weaponName);

        if (weaponIconImage != null)
        {
            weaponIconImage.sprite = weapon.icon;
            weaponIconImage.enabled = weapon.icon != null;
        }

        SetText(descriptionText, weapon.description);
        SetText(damageText, "Damage: " + weapon.damage);
        SetText(fireRateText, "Fire Rate: " + weapon.fireRateRPM + " RPM");
        SetText(bulletsText, "Bullets: " + weapon.bulletsPerShot);
        SetText(pierceText, "Pierce: " + weapon.pierce);
        SetText(specialText, "Special: " + weapon.specialDescription);

        SetNextButton(true);
    }

    private void ClearSelection()
    {
        SetText(selectedWeaponText, "Selected Weapon: none");

        if (weaponIconImage != null)
        {
            weaponIconImage.sprite = null;
            weaponIconImage.enabled = false;
        }

        SetText(descriptionText, "Select a weapon.");
        SetText(damageText, "Damage: -");
        SetText(fireRateText, "Fire Rate: -");
        SetText(bulletsText, "Bullets: -");
        SetText(pierceText, "Pierce: -");
        SetText(specialText, "Special: -");

        SetNextButton(false);
    }

    private void SetNextButton(bool active)
    {
        if (nextButton == null)
            return;

        nextButton.interactable = active;

        Image img = nextButton.GetComponent<Image>();
        if (img != null)
            img.color = active ? enabledColor : disabledColor;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}