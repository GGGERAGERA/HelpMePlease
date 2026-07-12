using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CharacterCardView))]
public class CharacterClickHandler : MonoBehaviour
{
    [SerializeField] private CharacterSelectionUI selectionUI;

    private Button button;
    private CharacterCardView cardView;

    private void Awake()
    {
        button = GetComponent<Button>();
        cardView = GetComponent<CharacterCardView>();

        button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (selectionUI == null || cardView == null || cardView.Character == null)
            return;

        selectionUI.SelectCharacter(cardView.Character);
    }
}