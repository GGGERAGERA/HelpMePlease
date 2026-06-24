using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterCardView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData character;

    private bool isSelected;

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
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
    }
}