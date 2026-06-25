using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponSelectionUI : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private MainMenuController mainMenuController;

    [Header("Right Info Panel")]
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI weaponClassText;
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Weapon Stats")]
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI fireRateText;
    [SerializeField] private TextMeshProUGUI specialText;

    [Header("Button Visual")]
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private WeaponData selectedWeapon;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);

        ClearSelection();
    }

    private void OnEnable()
    {
        ClearSelection();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);
    }

    public void SelectWeapon(WeaponData weapon)
    {
        if (weapon == null)
            return;

        selectedWeapon = weapon;
        RunSelectionManager.Instance?.SelectWeapon(weapon);

        RefreshDetails(weapon);
        SetConfirmButton(true);
    }

    private void ConfirmSelection()
    {
        if (selectedWeapon == null)
            return;

        if (RunSelectionManager.Instance == null)
        {
            Debug.LogError("[WeaponSelectionUI] RunSelectionManager is missing.");
            return;
        }

        RunSelectionManager.Instance.SelectWeapon(selectedWeapon);

        if (!RunSelectionManager.Instance.IsReady)
        {
            Debug.LogWarning("[WeaponSelectionUI] Cannot start game. Character or weapon is missing.");
            return;
        }

        if (mainMenuController == null)
        {
            Debug.LogError("[WeaponSelectionUI] MainMenuController is not assigned.");
            return;
        }

        mainMenuController.StartGame();
    }

    private void RefreshDetails(WeaponData weapon)
    {
        SetText(weaponNameText, weapon.weaponName);
        SetText(weaponClassText, weapon.weaponName.ToUpperInvariant());
        SetText(descriptionText, weapon.description);

        SetText(damageText, $"Damage: {weapon.damage}");
        SetText(fireRateText, $"Fire Rate: {weapon.fireRateRPM} RPM");
        SetText(specialText, $"Special: {GetSpecialText(weapon.specialDescription)}");

        SetWeaponIcon(weapon.icon);
    }

    private void ClearSelection()
    {
        selectedWeapon = null;

        SetText(weaponNameText, "Name");
        SetText(weaponClassText, "Class");
        SetText(descriptionText, "Description");

        SetText(damageText, "Damage");
        SetText(fireRateText, "Fire Rate");
        SetText(specialText, "Special");

        SetWeaponIcon(null);
        SetConfirmButton(false);
    }

    private void SetWeaponIcon(Sprite icon)
    {
        if (weaponIconImage == null)
            return;

        weaponIconImage.sprite = icon;
        weaponIconImage.enabled = icon != null;
    }

    private void SetConfirmButton(bool active)
    {
        if (confirmButton == null)
            return;

        confirmButton.interactable = active;

        Image image = confirmButton.GetComponent<Image>();
        if (image != null)
            image.color = active ? enabledColor : disabledColor;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private string GetSpecialText(string special)
    {
        return string.IsNullOrWhiteSpace(special) ? "No" : special;
    }
}
