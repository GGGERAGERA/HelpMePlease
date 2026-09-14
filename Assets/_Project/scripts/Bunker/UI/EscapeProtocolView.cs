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
        detailGroup.alpha = 1f;
        detailMotion.anchoredPosition = detailRestPosition;
    }

    private void SetMode(bool selecting, int access)
    {
        titleText.text = selecting ? "ESCAPE PROTOCOL <size=15><color=#739296>/ DEPTH SELECT</color></size>" : "ESCAPE PROTOCOL";
        accessText.text = $"ACCESS: {access} / {MetaProgressionManager.EscapeAccessRequired}";
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
            string status = locked ? "LOCKED" : state == DepthAvailability.Available ? "AVAILABLE"
                : "UNLOCKED\n<size=12>NOT AVAILABLE IN DEMO</size>";
            string accent = locked ? "#40575E" : "#16D2DB";
            card.label.text = $"<color={accent}><size=40>{DepthCatalog.Numeral(depth.id)}</size></color>\n<size=22>{depth.displayName}</size>\n\n<color={accent}><size=13>{status}</size></color>";
            card.label.color = locked ? new Color(.34f, .43f, .47f) : StationPixelVisuals.Text;
            card.background.color = locked ? new Color(.009f, .018f, .025f)
                : selected ? StationPixelVisuals.PanelRaised : StationPixelVisuals.Panel;
            card.border.effectColor = selected ? StationPixelVisuals.Cyan
                : locked ? new Color(.04f, .1f, .13f) : StationPixelVisuals.CyanMuted;
            card.lockIcon.SetActive(locked);

        }
        DepthCatalog.Entry choice = depths.Find(SelectedDepthId);
        DepthAvailability availability = choice.GetAvailability(access);
        string heading = $"<size=32><color=#E6F0F2>{choice.displayName}</color></size>";
        detailText.text = availability switch
        {
            DepthAvailability.Locked => $"{heading}\n\n<size=17><color=#77939A>REQUIRES ACCESS {DepthCatalog.Numeral(choice.requiredAccess)}</color></size>",
            DepthAvailability.UnavailableInBuild => $"{heading}\n<size=16><color=#16D2DB>UNLOCKED</color></size>\n\n<size=18><color=#77939A>NOT AVAILABLE IN DEMO</color></size>",
            _ => $"{heading}\n<size=16><color=#16D2DB>{choice.description.Replace("\n", "  ·  ")}  ·  ACCESS {DepthCatalog.Numeral(choice.requiredAccess + 1)}</color></size>\n\n<size=18><color=#94ADB3>Defeat the Guardian. Unlock the next depth.</color></size>"
        };
        detailFadeTime = 0f;
        reachedPath.fillAmount = pathGlow.fillAmount = Mathf.Clamp01(access / (float)(cards.Length - 1));
        startButton.targetGraphic.color = availability == DepthAvailability.Available
            ? StationPixelVisuals.Cyan : StationPixelVisuals.PanelRaised;
        startButtonText.color = availability == DepthAvailability.Available
            ? new Color(.015f, .09f, .12f) : StationPixelVisuals.MutedText;
        startButton.interactable = availability == DepthAvailability.Available;
        startButtonText.text = availability == DepthAvailability.Locked ? "LOCKED"
            : availability == DepthAvailability.UnavailableInBuild ? "UNAVAILABLE" : "START RUN";
    }
}
