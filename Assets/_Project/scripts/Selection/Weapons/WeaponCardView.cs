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
    [SerializeField] private TextMeshProUGUI conditionText;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Colors")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField]
    private Color lockedColor =
        new Color(0.35f, 0.35f, 0.35f, 1f);

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

        // Закрытую карточку можно нажать, чтобы увидеть условие справа.
        // Но выбрать оружие кнопкой Confirm всё равно нельзя.
        Clicked?.Invoke(weapon);
    }

    public void Refresh()
    {
        if (weapon == null)
        {
            SetCardAvailable(false);
            return;
        }

        bool isUnlocked = IsUnlocked();

        RefreshMainVisuals(isUnlocked);
        RefreshLockedState(isUnlocked);

        if (button != null)
        {
            // Оставляем кликабельной даже закрытую карточку,
            // чтобы игрок мог посмотреть условия открытия.
            button.interactable = true;
        }
    }

    public void SetSelected(bool selected)
    {
        // Пока оставляем выбор через существующие состояния Button/Animator.
        // Позже сюда можно добавить отдельную selected-frame.
    }

    private void RefreshMainVisuals(bool isUnlocked)
    {
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
    }

    private void RefreshLockedState(bool isUnlocked)
    {
        if (lockedOverlay != null)
            lockedOverlay.SetActive(!isUnlocked);

        if (isUnlocked)
        {
            SetText(conditionText, string.Empty);
            SetText(progressText, string.Empty);
            return;
        }

        UnlockableContentData unlockData = weapon.unlockData;

        if (unlockData == null || unlockData.condition == null)
        {
            SetText(conditionText, "Условия открытия не заданы");
            SetText(progressText, string.Empty);
            return;
        }

        UnlockConditionData condition = unlockData.condition;

        int required = Mathf.Max(1, condition.requiredAmount);
        int current = GetCurrentProgress(unlockData);
        int clampedCurrent = Mathf.Clamp(current, 0, required);
        int remaining = Mathf.Max(0, required - clampedCurrent);

        SetText(conditionText, BuildConditionText(condition));
        SetText(
            progressText,
            $"{clampedCurrent} / {required}\nОсталось: {remaining}"
        );
    }

    private int GetCurrentProgress(UnlockableContentData unlockData)
    {
        if (UnlockProgressService.Instance == null)
            return 0;

        return UnlockProgressService.Instance.GetProgress(unlockData);
    }

    private static string BuildConditionText(UnlockConditionData condition)
    {
        if (condition == null)
            return "Условия открытия не заданы";

        return condition.type switch
        {
            UnlockConditionType.KillEnemyType =>
                $"Убить врагов: {condition.targetId}",

            UnlockConditionType.KillTotalEnemies =>
                "Убить любых врагов",

            UnlockConditionType.CompleteLevelModifier =>
                $"Пройти уровень: {condition.targetId}",

            UnlockConditionType.CompleteRun =>
                "Завершить забег",

            _ =>
                "Выполнить условие открытия"
        };
    }

    private bool IsUnlocked()
    {
        if (weapon == null || weapon.unlockData == null)
            return true;

        if (UnlockProgressService.Instance == null)
            return weapon.unlockData.unlockedByDefault;

        return UnlockProgressService.Instance.IsUnlocked(weapon.unlockData);
    }

    private void SetCardAvailable(bool available)
    {
        if (button != null)
            button.interactable = available;

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!available);
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value;
    }
}