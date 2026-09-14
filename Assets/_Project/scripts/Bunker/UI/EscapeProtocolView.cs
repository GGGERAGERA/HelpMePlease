using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EscapeProtocolView : MonoBehaviour
{
    [Serializable]
    private sealed class DepthCard
    {
        public int depthId;
        public Button button;
        public TMP_Text label;
        public Image background;
    }

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text accessText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text terminalStatusText;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text startButtonText;
    [SerializeField] private DepthCard[] cards;
    [SerializeField] private GameObject pathLine;
    [SerializeField] private GameObject detailPanel;

    private DepthCatalog depths;
    private BunkerPanelManager launchOwner;
    private Transform launchTarget;
    public int SelectedDepthId { get; private set; }

    public void Show(int access, DepthCatalog catalog)
    {
        depths = catalog;
        launchOwner = null;
        launchTarget = null;
        SelectedDepthId = 0;
        SetMode(false, access);
        DepthCatalog.Entry next = depths.Find(access + 1);
        terminalStatusText.text = access == 0
            ? "CURRENT OBJECTIVE\n<size=26>SECTOR GUARDIAN</size>\n\nПолучите уровень доступа, чтобы открыть путь из комплекса."
            : $"ACCESS {DepthCatalog.Numeral(access)} ACQUIRED\n\nNEXT: {next?.displayName}\nUNLOCKED\n<size=18>NOT AVAILABLE IN DEMO</size>";
        gameObject.SetActive(true);
    }

    public void ShowDepthSelect(int access, DepthCatalog catalog, BunkerPanelManager owner, Transform transitionTarget)
    {
        depths = catalog;
        launchOwner = owner;
        launchTarget = transitionTarget;
        SetMode(true, access);
        SelectedDepthId = depths.Entries[0].id;
        RefreshSelection(access);
        gameObject.SetActive(true);
    }

    // Locked cards remain selectable; selecting is never a launch action.
    public void SelectDepth(int depthId)
    {
        if (launchOwner == null || depths.Find(depthId) == null) return;
        SelectedDepthId = depthId;
        RefreshSelection(MetaProgressionManager.EnsureExists().EscapeAccess);
    }

    public void StartSelectedRun()
    {
        if (!isActiveAndEnabled || launchOwner == null || SceneTransitionOverlay.IsTransitioning) return;
        DepthCatalog.Entry selected = depths.Find(SelectedDepthId);
        if (selected == null || selected.GetAvailability(MetaProgressionManager.EnsureExists().EscapeAccess) != DepthAvailability.Available) return;
        launchOwner.StartRun(launchTarget, SelectedDepthId);
    }

    private void OnDisable()
    {
        launchOwner = null;
        launchTarget = null;
        SelectedDepthId = 0;
    }

    private void SetMode(bool selecting, int access)
    {
        titleText.text = selecting ? "ESCAPE PROTOCOL / DEPTH SELECT" : "ESCAPE PROTOCOL";
        accessText.text = $"ACCESS: {access} / {MetaProgressionManager.EscapeAccessRequired}";
        startButton.gameObject.SetActive(selecting);
        detailPanel.SetActive(selecting);
        detailText.gameObject.SetActive(selecting);
        pathLine.SetActive(selecting);
        terminalStatusText.gameObject.SetActive(!selecting);
        foreach (DepthCard card in cards) card.button.gameObject.SetActive(selecting);
    }

    private void RefreshSelection(int access)
    {
        foreach (DepthCard card in cards)
        {
            DepthCatalog.Entry depth = depths.Find(card.depthId);
            DepthAvailability state = depth.GetAvailability(access);
            bool selected = depth.id == SelectedDepthId;
            string status = state == DepthAvailability.Locked ? "LOCKED"
                : state == DepthAvailability.Available ? "AVAILABLE" : "UNLOCKED\n<size=13>NOT IN DEMO</size>";
            card.label.text = $"<size=32>{DepthCatalog.Numeral(depth.id)}</size>\n<size=21>{depth.displayName}</size>\n\n<size=15>{status}</size>\n<size=14>{(selected ? "SELECTED" : " ")}</size>";
            card.label.color = state == DepthAvailability.Locked ? new Color(.48f, .62f, .65f) : new Color(.1f, .86f, .9f);
            card.background.color = selected ? new Color(.08f, .28f, .32f) : new Color(.025f, .07f, .09f);
        }
        DepthCatalog.Entry choice = depths.Find(SelectedDepthId);
        DepthAvailability availability = choice.GetAvailability(access);
        detailText.text = availability switch
        {
            DepthAvailability.Locked => $"<size=28>{choice.displayName}</size>\n\nREQUIRES ACCESS {DepthCatalog.Numeral(choice.requiredAccess)}\nSTATUS: LOCKED",
            DepthAvailability.UnavailableInBuild => $"<size=28>{choice.displayName}</size>\n\nSTATUS: UNLOCKED\nNOT AVAILABLE IN DEMO",
            _ => $"<size=28>{choice.displayName}</size>\n{choice.description}\n\nSTATUS: AVAILABLE"
        };
        startButton.interactable = availability == DepthAvailability.Available;
        startButtonText.text = availability == DepthAvailability.Locked ? "LOCKED"
            : availability == DepthAvailability.UnavailableInBuild ? "UNAVAILABLE" : "START RUN";
    }
}
