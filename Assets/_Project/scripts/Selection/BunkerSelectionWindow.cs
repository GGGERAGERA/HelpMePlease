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

    public bool IsOpen => gameObject.activeInHierarchy && source != null;

    private void Awake()
    {
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
        itemProgression?.CancelInvestment();
        stationProgress?.CancelInvestment();
        UnbindSource();
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
        selected = FindEntry(desiredId);
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
            card.SetSelected(selected != null && card.EntryId == selected.Id);

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

        if (confirmButton != null)
            confirmButton.interactable = selected != null &&
                selected.Enabled && !selected.Locked && selected.CanConfirm;
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
