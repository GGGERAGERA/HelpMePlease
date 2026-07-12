using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCardView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData character;

    [Header("Visuals")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject lockedOverlay;

    [Header("Colors")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new(0.35f, 0.35f, 0.35f, 1f);

    private Button button;

    public CharacterData Character => character;

    public event Action<CharacterData> Clicked;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
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
        if (character == null)
            return;

        Clicked?.Invoke(character);
    }

    public void Refresh()
    {
        if (character == null)
            return;

        bool isUnlocked = IsUnlocked();

        if (portraitImage != null)
        {
            portraitImage.sprite = character.portrait;
            portraitImage.color = isUnlocked ? unlockedColor : lockedColor;
        }

        if (nameText != null)
        {
            nameText.text = character.characterName;
            nameText.color = isUnlocked ? Color.white : Color.gray;
        }

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!isUnlocked);
    }

    public void SetSelected(bool selected)
    {
        // Пока оставляем пустым.
        // У тебя выделение, похоже, живет через Animator/Button states.
    }

    private bool IsUnlocked()
    {
        if (character == null || character.unlockData == null)
            return true;

        if (UnlockProgressService.Instance == null)
            return character.unlockData.unlockedByDefault;

        return UnlockProgressService.Instance.IsUnlocked(character.unlockData);
    }
}