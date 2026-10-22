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
    [SerializeField] private GameObject hoverFrame;
    [SerializeField] private GameObject equippedIndicator;
    [SerializeField] private GameObject concealedSubject;
    [SerializeField] private Image[] selectionAccents;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private TextMeshProUGUI lockedText;
    [SerializeField] private Button button;
    [SerializeField] private float nameInsetWithIcon = 108f;
    [SerializeField] private bool animateSelection;

    private BunkerSelectionEntryModel entry;
    private bool selected;
    private bool hovered;
    private float selectionReaction;

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

        bool concealed = value.Locked;
        if (concealedSubject != null)
            concealedSubject.SetActive(concealed);

        if (icon != null)
        {
            icon.sprite = concealed ? null : value.IsCharacter ? value.CharacterVisual : value.Icon;
            icon.gameObject.SetActive(icon.sprite != null);
            icon.enabled = icon.sprite != null;
            icon.rectTransform.localScale = Vector3.one;
            icon.preserveAspect = true;
            icon.color = value.IconColor;
        }

        if (nameText != null)
        {
            Vector2 inset = nameText.rectTransform.offsetMin;
            inset.x = (icon != null && icon.sprite != null) || (concealed && concealedSubject != null)
                ? nameInsetWithIcon : 16f;
            nameText.rectTransform.offsetMin = inset;
            nameText.text = value.PresentationName;
            nameText.color = value.Locked
                ? StationPixelVisuals.MutedText
                : StationPixelVisuals.Text;
        }

        if (lockedOverlay != null)
            lockedOverlay.SetActive(value.Locked);
        if (lockedText != null)
            lockedText.text = !value.Locked ? string.Empty : concealed && concealedSubject != null
                ? string.Format(LocalizationService.Instance.Get("bunker.subject.card_level"), value.RequiredStationLevel)
                : LocalizationService.Instance.Get("bunker.locked");
        if (button != null)
            button.interactable = value.Enabled;

        if (animateSelection)
            RefreshBackground();
        else
            SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        if (animateSelection && value && !selected)
            selectionReaction = 1f;
        selected = value;
        if (selectedFrame != null)
            selectedFrame.SetActive(value);
        RefreshBackground();
    }

    public void SetEquipped(bool value)
    {
        if (equippedIndicator != null)
            equippedIndicator.SetActive(value);
    }

    private void LateUpdate()
    {
        if (animateSelection && selectionAccents != null)
        {
            selectionReaction = Mathf.MoveTowards(selectionReaction, 0f, Time.unscaledDeltaTime * 5f);
            Color accent = entry != null && entry.Locked
                ? new Color(0.85f, 0.31f, 0.25f, 1f) : StationPixelVisuals.Cyan;
            foreach (Image image in selectionAccents)
                image.color = Color.Lerp(accent, Color.white, selectionReaction * 0.35f);
        }
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
        if (hoverFrame != null)
            hoverFrame.SetActive(hovered && !selected);
        if (selectionAccents != null)
        {
            Color accent = entry != null && entry.Locked
                ? new Color(0.85f, 0.31f, 0.25f, 1f) : StationPixelVisuals.Cyan;
            foreach (Image image in selectionAccents)
                image.color = accent;
        }
        if (background == null)
            return;
        if (concealedSubject != null && entry != null && entry.Locked)
        {
            background.color = selected ? new Color(0.13f, 0.06f, 0.065f, 1f)
                : hovered ? new Color(0.075f, 0.09f, 0.1f, 1f) : StationPixelVisuals.Panel;
            return;
        }
        background.color = selected
            ? new Color(0.055f, 0.22f, 0.25f, 1f)
            : hovered
                ? new Color(0.05f, 0.13f, 0.15f, 1f)
                : StationPixelVisuals.PanelRaised;
    }
}
