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
        public Outline border;
        public Outline glow;
        public GameObject lockIcon;
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

    [Header("Presentation")]
    [SerializeField] private Image reachedPath;
    [SerializeField] private Image pathGlow;
    [SerializeField] private CanvasGroup detailGroup;
    [SerializeField] private RectTransform detailMotion;
    private Vector2 detailRestPosition;
    private float detailFadeTime = 1f;

    private void Awake() => detailRestPosition = detailMotion.anchoredPosition;

    private void Update()
    {
        if (launchOwner == null) return;
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.8f);
        foreach (DepthCard card in cards)
        {
            bool selected = card.depthId == SelectedDepthId;
            card.glow.effectColor = new Color(.08f, .82f, .86f, selected ? .08f + .07f * pulse : 0f);
            if (selected) card.border.effectColor = new Color(.15f, .92f, .96f, .78f + .22f * pulse);
        }
        pathGlow.color = new Color(.08f, .82f, .86f, .12f + .05f * pulse);
        detailFadeTime = Mathf.Min(.16f, detailFadeTime + Time.unscaledDeltaTime);
        float t = 1f - Mathf.Pow(1f - detailFadeTime / .16f, 3f);
        detailGroup.alpha = Mathf.Lerp(.4f, 1f, t);
        detailMotion.anchoredPosition = detailRestPosition + Vector2.down * (6f * (1f - t));
    }

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
        RefreshTerminal(access);
        gameObject.SetActive(true);
    }

    private void RefreshTerminal(int access)
    {
        DepthCatalog.Entry next = depths.Find(access + 1);
        terminalStatusText.text = access == 0
            ? LocalizationService.Instance.Get("bunker.depth.objective")
            : string.Format(LocalizationService.Instance.Get("bunker.depth.acquired"), DepthCatalog.Numeral(access), next?.LocalizedName);
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

    private void OnEnable() => LocalizationService.Instance.LanguageChanged += HandleLanguageChanged;
    private void HandleLanguageChanged(GameLanguage language)
    {
        if (depths == null) return;
        int access = MetaProgressionManager.EnsureExists().EscapeAccess;
        SetMode(launchOwner != null, access);
        if (launchOwner != null) RefreshSelection(access);
        else RefreshTerminal(access);
    }

    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
        launchOwner = null;
        launchTarget = null;
        SelectedDepthId = 0;
        detailGroup.alpha = 1f;
        detailMotion.anchoredPosition = detailRestPosition;
    }

    private void SetMode(bool selecting, int access)
    {
        titleText.text = selecting ? LocalizationService.Instance.Get("bunker.depth.select_title") : LocalizationService.Instance.Get("bunker.depth.title");
        accessText.text = string.Format(LocalizationService.Instance.Get("bunker.depth.access"), access, MetaProgressionManager.EscapeAccessRequired);
        startButton.gameObject.SetActive(selecting);
        detailPanel.SetActive(selecting);
        detailText.gameObject.SetActive(selecting);
        pathLine.SetActive(selecting);
        reachedPath.gameObject.SetActive(selecting);
        pathGlow.gameObject.SetActive(selecting);
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
            bool locked = state == DepthAvailability.Locked;
            string status = locked ? LocalizationService.Instance.Get("bunker.depth.locked") : state == DepthAvailability.Available ? LocalizationService.Instance.Get("bunker.depth.available")
                : LocalizationService.Instance.Get("bunker.depth.demo_status");
            string accent = locked ? "#40575E" : "#16D2DB";
            card.label.text = $"<color={accent}><size=40>{DepthCatalog.Numeral(depth.id)}</size></color>\n<size=22>{depth.LocalizedName}</size>\n\n<color={accent}><size=13>{status}</size></color>";
            card.label.color = locked ? new Color(.34f, .43f, .47f) : StationPixelVisuals.Text;
            card.background.color = locked ? new Color(.009f, .018f, .025f)
                : selected ? StationPixelVisuals.PanelRaised : StationPixelVisuals.Panel;
            card.border.effectColor = selected ? StationPixelVisuals.Cyan
                : locked ? new Color(.04f, .1f, .13f) : StationPixelVisuals.CyanMuted;
            card.lockIcon.SetActive(locked);

        }
        DepthCatalog.Entry choice = depths.Find(SelectedDepthId);
        DepthAvailability availability = choice.GetAvailability(access);
        string heading = $"<size=32><color=#E6F0F2>{choice.LocalizedName}</color></size>";
        detailText.text = availability switch
        {
            DepthAvailability.Locked => string.Format(LocalizationService.Instance.Get("bunker.depth.requires"), heading, DepthCatalog.Numeral(choice.requiredAccess)),
            DepthAvailability.UnavailableInBuild => string.Format(LocalizationService.Instance.Get("bunker.depth.demo_detail"), heading),
            _ => string.Format(LocalizationService.Instance.Get("bunker.depth.available_detail"), heading, choice.LocalizedDescription.Replace("\n", "  ·  "), DepthCatalog.Numeral(choice.requiredAccess + 1))
        };
        detailFadeTime = 0f;
        reachedPath.fillAmount = pathGlow.fillAmount = Mathf.Clamp01(access / (float)(cards.Length - 1));
        startButton.targetGraphic.color = availability == DepthAvailability.Available
            ? StationPixelVisuals.Cyan : StationPixelVisuals.PanelRaised;
        startButtonText.color = availability == DepthAvailability.Available
            ? new Color(.015f, .09f, .12f) : StationPixelVisuals.MutedText;
        startButton.interactable = availability == DepthAvailability.Available;
        startButtonText.text = availability == DepthAvailability.Locked ? LocalizationService.Instance.Get("bunker.depth.locked")
            : availability == DepthAvailability.UnavailableInBuild ? LocalizationService.Instance.Get("bunker.depth.unavailable") : LocalizationService.Instance.Get("bunker.depth.start");
    }
}
