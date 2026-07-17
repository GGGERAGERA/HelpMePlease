using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponSelectionUI : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private WeaponCardView[] cards;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private BunkerPanelManager panelManager;

    [Header("Right Info Panel")]
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Weapon Stats")]
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI fireRateText;
    [SerializeField] private TextMeshProUGUI specialText;

    [Header("Button Visual")]
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new(0.45f, 0.45f, 0.45f, 1f);

    private WeaponData selectedWeapon;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);

        if (backButton != null)
            backButton.onClick.AddListener(Close);

        BindCards();
        ClearSelection();
    }

    private void OnEnable()
    {
        ClearSelection();

        if (cards == null)
            return;

        foreach (WeaponCardView card in cards)
        {
            if (card != null)
                card.Refresh();
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);

        if (backButton != null)
            backButton.onClick.RemoveListener(Close);

        UnbindCards();
    }

    public void SelectWeapon(WeaponData weapon)
    {
        if (weapon == null)
            return;

        selectedWeapon = weapon;

        RefreshDetails(weapon);
        RefreshCards(weapon);

        bool isUnlocked = IsWeaponUnlocked(weapon);
        SetConfirmButton(isUnlocked);
    }

    private void ConfirmSelection()
    {
        if (selectedWeapon == null)
            return;

        if (!IsWeaponUnlocked(selectedWeapon))
            return;

        if (RunSelectionManager.Instance == null)
        {
            Debug.LogError("[WeaponSelectionUI] RunSelectionManager is missing.");
            return;
        }

        RunSelectionManager.Instance.SelectWeapon(selectedWeapon);
        AudioService.Instance?.Play(AudioCueId.UIConfirm);
        Close(false);
    }

    private void Close()
    {
        Close(true);
    }

    private void Close(bool playSound)
    {
        if (panelManager != null)
        {
            panelManager.CloseAll(playSound);
            return;
        }

        if (BunkerContext.Instance != null && BunkerContext.Instance.Panels != null)
        {
            BunkerContext.Instance.Panels.CloseAll(playSound);
            return;
        }

        Debug.LogError("[WeaponSelectionUI] No BunkerPanelManager found.");
    }

    private void BindCards()
    {
        if (cards == null)
            return;

        foreach (WeaponCardView card in cards)
        {
            if (card == null)
                continue;

            card.Clicked -= SelectWeapon;
            card.Clicked += SelectWeapon;
            card.Refresh();
        }
    }

    private void UnbindCards()
    {
        if (cards == null)
            return;

        foreach (WeaponCardView card in cards)
        {
            if (card != null)
                card.Clicked -= SelectWeapon;
        }
    }

    private void RefreshCards(WeaponData weapon)
    {
        if (cards == null)
            return;

        foreach (WeaponCardView card in cards)
        {
            if (card == null)
                continue;

            card.SetSelected(card.Weapon == weapon);
        }
    }

    private void RefreshDetails(WeaponData weapon)
    {
        bool isUnlocked = IsWeaponUnlocked(weapon);

        if (!isUnlocked && weapon.unlockData != null)
        {
            SetText(descriptionText, weapon.unlockData.lockedDescription);
            SetText(damageText, "");
            SetText(fireRateText, "");
            SetText(specialText, "LOCKED");

            SetWeaponIcon(weapon.icon, Color.gray);
            return;
        }

        SetText(descriptionText, weapon.description);
        SetText(damageText, $"Damage: {weapon.damage}");
        SetText(fireRateText, $"Fire Rate: {weapon.fireRateRPM} RPM");
        SetText(specialText, $"Special: {GetSpecialText(weapon.specialDescription)}");

        SetWeaponIcon(weapon.icon, Color.white);
    }

    private void ClearSelection()
    {
        selectedWeapon = null;
        SetText(descriptionText, "Choose a weapon.");
        SetText(damageText, "Damage: -");
        SetText(fireRateText, "Fire Rate: -");
        SetText(specialText, "Special: -");

        SetWeaponIcon(null, Color.white);
        RefreshCards(null);
        SetConfirmButton(false);
    }

    private void SetWeaponIcon(Sprite icon, Color color)
    {
        if (weaponIconImage == null)
            return;

        weaponIconImage.sprite = icon;
        weaponIconImage.color = color;
        weaponIconImage.enabled = icon != null;
    }

    private void SetConfirmButton(bool active)
    {
        if (confirmButton == null)
            return;

        confirmButton.interactable = active;
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

    private bool IsWeaponUnlocked(WeaponData weapon)
    {
        if (weapon == null || weapon.unlockData == null)
            return true;

        if (UnlockProgressService.Instance == null)
            return weapon.unlockData.unlockedByDefault;

        return UnlockProgressService.Instance.IsUnlocked(weapon.unlockData);
    }
}
