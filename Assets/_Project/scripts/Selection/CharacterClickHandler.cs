using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterClickHandler : MonoBehaviour, IPointerClickHandler
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
}