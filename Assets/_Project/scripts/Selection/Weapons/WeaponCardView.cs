using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class WeaponCardView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WeaponData weapon;

    [Header("Visuals")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject lockedOverlay;

    [Header("Colors")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new(0.35f, 0.35f, 0.35f, 1f);

    private Button button;

    public WeaponData Weapon => weapon;

    public event Action<WeaponData> Clicked;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);

        Refresh();
        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (weapon == null)
            return;

        Clicked?.Invoke(weapon);
    }

    public void Refresh()
    {
        if (weapon == null)
            return;

        bool isUnlocked = IsUnlocked();

        if (iconImage != null)
        {
            iconImage.sprite = weapon.icon;
            iconImage.color = isUnlocked ? unlockedColor : lockedColor;
            iconImage.enabled = weapon.icon != null;
        }

        if (nameText != null)
        {
            nameText.text = weapon.weaponName;
            nameText.color = isUnlocked ? Color.white : Color.gray;
        }

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!isUnlocked);
    }

    public void SetSelected(bool selected)
    {
        // Пока пусто.
        // Позже сюда можно добавить рамку выбранной карточки.
    }

    private bool IsUnlocked()
    {
        if (weapon == null || weapon.unlockData == null)
            return true;

        if (UnlockProgressService.Instance == null)
            return weapon.unlockData.unlockedByDefault;

        return UnlockProgressService.Instance.IsUnlocked(weapon.unlockData);
    }
}