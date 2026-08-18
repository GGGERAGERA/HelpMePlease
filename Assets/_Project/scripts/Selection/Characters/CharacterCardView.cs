using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CharacterCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Data")]
    [SerializeField] private CharacterData character;

    [Header("Prefab Visuals")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image characterImage;
    [SerializeField] private Sprite characterSprite;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;

    private bool isHovered;
    private bool isSelected;

    public CharacterData Character => character;
    public event Action<CharacterData> Clicked;

    private void Awake()
    {
        if (button == null)
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

    public void Refresh()
    {
        bool isUnlocked = character != null && IsUnlocked();
        gameObject.SetActive(isUnlocked);
        if (!isUnlocked)
            return;

        if (characterImage != null)
        {
            characterImage.sprite = characterSprite;
            characterImage.color = Color.white;
            characterImage.enabled = characterSprite != null;
        }

        if (nameText != null)
        {
            nameText.text = character.characterName;
            nameText.color = StationPixelVisuals.Text;
        }

        if (button != null)
            button.interactable = true;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshVisualState();
    }

    private void HandleClick()
    {
        if (character != null && IsUnlocked())
            Clicked?.Invoke(character);
    }

    private bool IsUnlocked()
    {
        return character.unlockData == null || UnlockProgressService.IsUnlockedNow(character.unlockData);
    }

    private void RefreshVisualState()
    {
        if (backgroundImage == null)
            return;

        backgroundImage.color = isSelected
            ? new Color(0.055f, 0.22f, 0.25f, 1f)
            : isHovered
                ? new Color(0.05f, 0.13f, 0.15f, 1f)
                : StationPixelVisuals.PanelRaised;
    }
}
