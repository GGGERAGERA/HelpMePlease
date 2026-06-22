using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterClickHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CharacterData character;
    [SerializeField] private CharacterSelectionUI selectionUI;
    [SerializeField] private CharacterCardView cardView;

    private void Awake()
    {
        if (cardView == null)
            cardView = GetComponent<CharacterCardView>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (character == null || selectionUI == null)
            return;

        selectionUI.SelectCharacter(character);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardView != null)
            cardView.SetHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (cardView != null)
            cardView.SetHover(false);
    }
}