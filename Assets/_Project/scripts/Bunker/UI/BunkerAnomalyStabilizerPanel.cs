using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerAnomalyStabilizerPanel : MonoBehaviour
{
    [System.Serializable]
    private sealed class CardView
    {
        public GameObject Root;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Description;
        public Button SelectButton;
        public TextMeshProUGUI SelectLabel;
        [System.NonSerialized]
        public AnomalyStabilizerData Data;
    }

    private readonly List<AnomalyStabilizerData> available = new();
    private readonly List<AnomalyStabilizerData> choices = new();

    [Header("Prefab View")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI selectedText;
    [SerializeField] private CardView[] cards = new CardView[3];
    [SerializeField] private Button clearButton;
    [SerializeField] private Button closeButton;

    private BunkerPanelManager panelManager;

    public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;

    private void Awake()
    {
        clearButton?.onClick.AddListener(ClearSelection);
        closeButton?.onClick.AddListener(CloseFromButton);

        for (int i = 0; i < cards.Length; i++)
        {
            CardView card = cards[i];
            if (card?.SelectButton != null)
                card.SelectButton.onClick.AddListener(() => Select(card.Data));
        }
    }

    public void Configure(BunkerPanelManager manager)
    {
        panelManager = manager;
    }

    public void Show()
    {
        if (canvasRoot == null)
        {
            Debug.LogError(
                "[BunkerAnomalyStabilizerPanel] Prefab view is not assigned.",
                this);
            return;
        }

        canvasRoot.SetActive(true);
        BindEvents();
        RollChoices();
        Refresh();
    }

    public void Hide()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        clearButton?.onClick.RemoveListener(ClearSelection);
        closeButton?.onClick.RemoveListener(CloseFromButton);
    }

    private void BindEvents()
    {
        UnbindEvents();

        if (BunkerStationProgressionService.Instance != null)
        {
            BunkerStationProgressionService.Instance.StationLevelChanged +=
                HandleStationLevelChanged;
        }
    }

    private void UnbindEvents()
    {
        if (BunkerStationProgressionService.Instance != null)
        {
            BunkerStationProgressionService.Instance.StationLevelChanged -=
                HandleStationLevelChanged;
        }
    }

    private void HandleStationLevelChanged(BunkerStationId stationId, int level)
    {
        if (stationId != BunkerStationId.Anomaly || !IsVisible)
            return;

        RollChoices();
        Refresh();
    }

    private void RollChoices()
    {
        available.Clear();
        choices.Clear();

        int stationLevel = BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Anomaly)
            : BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Anomaly);
        AnomalyStabilizerData[] all =
            Resources.LoadAll<AnomalyStabilizerData>("AnomalyStabilizers");

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].RequiredStationLevel <= stationLevel)
                available.Add(all[i]);
        }

        AnomalyStabilizerData selected = RunSelectionManager.Instance != null
            ? RunSelectionManager.Instance.SelectedAnomalyStabilizer
            : null;

        if (selected != null && selected.RequiredStationLevel > stationLevel)
        {
            RunSelectionManager.Instance?.SelectAnomalyStabilizer(null);
            selected = null;
        }

        if (selected != null && available.Remove(selected))
            choices.Add(selected);

        int choiceCount = stationLevel <= 1 ? 2 : 3;

        while (choices.Count < choiceCount && available.Count > 0)
        {
            int index = Random.Range(0, available.Count);
            choices.Add(available[index]);
            available.RemoveAt(index);
        }
    }

    private void Select(AnomalyStabilizerData data)
    {
        if (data == null || RunSelectionManager.Instance == null)
            return;

        RunSelectionManager.Instance.SelectAnomalyStabilizer(data);
        Refresh();
    }

    private void ClearSelection()
    {
        RunSelectionManager.Instance?.SelectAnomalyStabilizer(null);
        Refresh();
    }

    private void CloseFromButton() => panelManager?.CloseAll();

    private void Refresh()
    {
        int stationLevel = BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Anomaly)
            : BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Anomaly);
        levelText.text = $"STATION LV.{stationLevel}";

        AnomalyStabilizerData selected = RunSelectionManager.Instance != null
            ? RunSelectionManager.Instance.SelectedAnomalyStabilizer
            : null;
        selectedText.text = selected != null
            ? $"SELECTED: {selected.DisplayName}"
            : "SELECTED: NONE";

        for (int i = 0; i < cards.Length; i++)
        {
            CardView card = cards[i];
            bool active = i < choices.Count;
            card.Root.SetActive(active);

            if (!active)
                continue;

            AnomalyStabilizerData data = choices[i];
            card.Data = data;
            card.Title.text = data.DisplayName;
            card.Description.text = data.Description;
            bool isSelected = data == selected;
            card.SelectLabel.text = isSelected ? "SELECTED" : "SELECT";
            card.SelectButton.interactable = !isSelected;
        }
    }

}
