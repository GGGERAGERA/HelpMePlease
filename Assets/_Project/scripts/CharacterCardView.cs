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
    [SerializeField] private TextMeshProUGUI lockedText;

    [Header("Colors")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new(0.35f, 0.35f, 0.35f, 1f);

    public CharacterData Character => character;

    private void Awake()
    {
        Refresh();
        SetSelected(false);
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

        if (lockedText != null)
            lockedText.text = "LOCKED";
    }

    public void SetSelected(bool selected)
    {
        // пока ничего, у тебя выделение, похоже, живёт отдельно в префабе/анимации
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