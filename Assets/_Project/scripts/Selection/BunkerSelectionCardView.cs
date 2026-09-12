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
            icon.sprite = value.IsCharacter ? value.CharacterVisual : value.Icon;
            icon.enabled = icon.sprite != null;
            icon.rectTransform.localScale = Vector3.one;
            icon.preserveAspect = true;
            icon.color = value.Locked
                ? StationPixelVisuals.Disabled
                : value.IconColor;
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

    private void LateUpdate()
    {
        if (icon == null || entry?.CharacterVisual == null)
            return;

        // Quantize to physical screen pixels after the CanvasScaler/layout pass.
        // Production body textures are imported with Point filtering.
        Rect rect = icon.rectTransform.rect;
        Vector2 pixels = entry.CharacterVisual.rect.size;
        float canvasScale = icon.canvas != null ? icon.canvas.scaleFactor : 1f;
        float fit = Mathf.Min(rect.width / pixels.x, rect.height / pixels.y) * canvasScale;
        float scale = fit >= 1f ? Mathf.Floor(fit) / fit : 1f;
        icon.rectTransform.localScale = new Vector3(scale, scale, 1f);
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
