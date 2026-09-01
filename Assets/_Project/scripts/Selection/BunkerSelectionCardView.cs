using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class BunkerSelectionCardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private TextMeshProUGUI lockedText;
    [SerializeField] private Button button;

    private BunkerSelectionEntryModel entry;
    private bool selected;
    private bool hovered;

    public event Action<BunkerSelectionEntryModel> Clicked;
    public string EntryId => entry?.Id;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        button?.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        button?.onClick.RemoveListener(HandleClick);
    }

    public void Bind(BunkerSelectionEntryModel value)
    {
        entry = value;
        gameObject.SetActive(value != null);
        if (value == null)
            return;

        if (icon != null)
        {
            icon.sprite = value.Icon;
            icon.enabled = value.Icon != null;
            icon.preserveAspect = true;
            icon.color = value.Locked ? StationPixelVisuals.Disabled : Color.white;
        }

        if (nameText != null)
        {
            nameText.text = value.DisplayName;
            nameText.color = value.Locked
                ? StationPixelVisuals.MutedText
                : StationPixelVisuals.Text;
        }

        if (lockedOverlay != null)
            lockedOverlay.SetActive(value.Locked);
        if (lockedText != null)
            lockedText.text = value.Locked ? "ЗАКРЫТО" : string.Empty;
        if (button != null)
            button.interactable = value.Enabled;

        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (selectedFrame != null)
            selectedFrame.SetActive(value);
        RefreshBackground();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshBackground();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshBackground();
    }

    private void HandleClick()
    {
        if (entry != null && entry.Enabled)
            Clicked?.Invoke(entry);
    }

    private void RefreshBackground()
    {
        if (background == null)
            return;
        background.color = selected
            ? new Color(0.055f, 0.22f, 0.25f, 1f)
            : hovered
                ? new Color(0.05f, 0.13f, 0.15f, 1f)
                : StationPixelVisuals.PanelRaised;
    }
}
