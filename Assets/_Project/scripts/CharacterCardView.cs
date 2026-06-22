using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterCardView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData character;

    [Header("UI")]
    [SerializeField] private Image portraitIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject hoverFrame;
    [SerializeField] private GameObject selectedFrame;

    private bool isSelected;

    public CharacterData Character => character;

    private void Awake()
    {
        Refresh();
        SetHover(false);
        SetSelected(false);
    }

    public void Refresh()
    {
        if (character == null)
            return;

        if (portraitIcon != null)
            portraitIcon.sprite = character.portrait;

        if (nameText != null)
            nameText.text = character.characterName;

        if (lockOverlay != null)
            lockOverlay.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectedFrame != null)
            selectedFrame.SetActive(selected);

        if (hoverFrame != null)
            hoverFrame.SetActive(!selected && false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);
    }

    public void SetHover(bool value)
    {
        if (hoverFrame != null)
            hoverFrame.SetActive(value && !isSelected);
    }
}