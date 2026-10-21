using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerSelectionWindow : MonoBehaviour
{
    [Header("Header and Cards")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI sectionTitleText;
    [SerializeField] private RectTransform cardsRoot;
    [SerializeField] private BunkerSelectionCardView cardPrefab;
    [SerializeField] private float itemCardHeight = 90f;

    [Header("Reusable Regions")]
    [SerializeField] private BunkerSelectionDetailView detailView;
    [SerializeField] private BunkerProgressionView itemProgression;
    [SerializeField] private BunkerProgressionView stationProgress;

    [Header("Footer")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;
    [SerializeField] private Button settingsButton;

    private readonly List<BunkerSelectionCardView> cards = new();
    private IBunkerSelectionSource source;
    private BunkerSelectionWindowModel model;
    private BunkerSelectionEntryModel selected;
    private BunkerPanelManager panelManager;
    private Vector2 authoredCardSize;

    public bool IsOpen => gameObject.activeInHierarchy && source != null;

    private void OnEnable() => LocalizationService.Instance.LanguageChanged += HandleLanguageChanged;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
    }
    private void HandleLanguageChanged(GameLanguage language)
    {
        Rebuild();
    }

    private void Awake()
    {
        var grid = cardsRoot != null ? cardsRoot.GetComponent<GridLayoutGroup>() : null;
        if (grid != null) authoredCardSize = grid.cellSize;
        backButton?.onClick.AddListener(Back);
        confirmButton?.onClick.AddListener(Confirm);
        settingsButton?.onClick.AddListener(OpenSettings);
    }

    private void OnDestroy()
    {
        UnbindSource();
        backButton?.onClick.RemoveListener(Back);
        confirmButton?.onClick.RemoveListener(Confirm);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        ClearCards();
    }

    public void Open(IBunkerSelectionSource newSource, BunkerPanelManager manager)
    {
        bool sameSource = ReferenceEquals(source, newSource);
        itemProgression?.CancelInvestment();
        stationProgress?.CancelInvestment();
        UnbindSource();
        if (!sameSource)
            selected = null;
        source = newSource;
        panelManager = manager;
        if (source == null)
        {
            Debug.LogError("[BunkerSelectionWindow] Selection source is missing.", this);
            return;
        }

        source.Prepare();
        source.Changed += Rebuild;
        gameObject.SetActive(true);
        Rebuild();
    }

    public void CloseView()
    {
        itemProgression?.CancelInvestment();
        stationProgress?.CancelInvestment();
        UnbindSource();
        model = null;
        selected = null;
        ClearCards();
        gameObject.SetActive(false);
    }

    public void Refresh() => Rebuild();

    private void Rebuild()
    {
        if (source == null)
            return;

        string previousId = selected?.Id;
        model = source.BuildModel();
        if (model == null)
            return;

        SetText(titleText, model.Title);
        SetText(sectionTitleText, model.SectionTitle);
        SetText(confirmButtonText, model.ConfirmText);
        stationProgress?.Bind(model.Station);
        ApplyLayout(model.CardsOnly, model.ShowConfirmButton);
        var grid = cardsRoot != null ? cardsRoot.GetComponent<GridLayoutGroup>() : null;
        if (grid != null && authoredCardSize.y > 0)
            grid.cellSize = new Vector2(authoredCardSize.x,
                model.Entries.Exists(entry => entry != null && entry.IsCharacter) ? authoredCardSize.y : itemCardHeight);

        for (int i = 0; i < model.Entries.Count; i++)
        {
            BunkerSelectionEntryModel entry = model.Entries[i];
            if (entry == null || cardPrefab == null || cardsRoot == null)
                continue;
            BunkerSelectionCardView card;
            if (i < cards.Count)
            {
                card = cards[i];
            }
            else
            {
                card = Instantiate(cardPrefab, cardsRoot);
                card.Clicked += Select;
                cards.Add(card);
            }
            card.name = $"SelectionCard_{entry.Id}";
            card.Bind(entry);
        }
        TrimCards(model.Entries.Count);

        string desiredId = !string.IsNullOrWhiteSpace(previousId)
            ? previousId
            : model.SelectedId;
        selected = FindEntry(desiredId) ?? FindEntry(model.SelectedId)
            ?? model.Entries.Find(entry => entry != null && !entry.Locked)
            ?? model.Entries.Find(entry => entry != null);
        RefreshSelection();
    }

    private void Select(BunkerSelectionEntryModel entry)
    {
        selected = entry;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (BunkerSelectionCardView card in cards)
        {
            card.SetSelected(selected != null && card.EntryId == selected.Id);
            card.SetEquipped(model.ShowConfirmButton && card.EntryId == model.SelectedId);
        }

        if (selected == null)
        {
            detailView?.ShowEmpty(model?.EmptyText);
            itemProgression?.Bind(null);
        }
        else
        {
            detailView?.Bind(selected);
            itemProgression?.Bind(selected.Progression);
        }

        if (selected != null)
        {
            bool current = selected.Id == model.SelectedId;
            if (confirmButtonText != null)
                confirmButtonText.color = selected.Locked
                    ? StationPixelVisuals.MutedText : StationPixelVisuals.Window;
            // Selection and closing are separate actions in every station.
            if (confirmButton != null)
                confirmButton.gameObject.SetActive(model.ShowConfirmButton && !current && !selected.Locked);
        }

        if (confirmButton != null)
            confirmButton.interactable = selected != null &&
                selected.Enabled && !selected.Locked && selected.CanConfirm;
    }

    private void ApplyLayout(bool cardsOnly, bool showConfirmButton)
    {
        if (detailView != null)
            detailView.gameObject.SetActive(!cardsOnly);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(!cardsOnly && showConfirmButton);
    }

    private void Confirm()
    {
        if (source == null || selected == null || selected.Locked ||
            !selected.Enabled || !selected.CanConfirm)
            return;

        source.Confirm(selected.Id);
        if (model != null && model.CloseOnConfirm)
            panelManager?.CloseAll(false);
        else
            Rebuild();
    }

    private void Back() => panelManager?.CloseAll();
    private void OpenSettings() => panelManager?.OpenSettings();

    private BunkerSelectionEntryModel FindEntry(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || model == null)
            return null;
        return model.Entries.Find(entry => entry != null && entry.Id == id);
    }

    private void ClearCards()
    {
        foreach (BunkerSelectionCardView card in cards)
        {
            if (card == null)
                continue;
            card.Clicked -= Select;
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
        cards.Clear();
    }

    private void TrimCards(int count)
    {
        for (int i = cards.Count - 1; i >= count; i--)
        {
            BunkerSelectionCardView card = cards[i];
            cards.RemoveAt(i);
            if (card == null)
                continue;
            card.Clicked -= Select;
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
    }

    private void UnbindSource()
    {
        if (source != null)
            source.Changed -= Rebuild;
        source = null;
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}
