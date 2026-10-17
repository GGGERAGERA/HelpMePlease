using TMPro;
using UnityEngine;

/// <summary>Scene-owned presentation of existing bunker state; owns no progression or balances.</summary>
[DisallowMultipleComponent]
public sealed class BunkerHudView : MonoBehaviour
{
    [SerializeField] private CanvasGroup content;
    [SerializeField] private HudIconNumber gold;
    [Tooltip("Add future currency views here only when their existing data source is available.")]
    [SerializeField] private RectTransform currencyContainer;
    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private TMP_Text objectiveTitle;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private DepthCatalog depths;

    [Header("Bunker scene context")]
    [SerializeField] private BunkerPanelManager panels;
    [SerializeField] private BunkerIntroController intro;
    [SerializeField] private BunkerMinigame[] minigames;

    private CurrencyManager currency;
    private MetaProgressionManager progression;
    private LocalizationService localization;

    public RectTransform CurrencyContainer => currencyContainer;

    private void OnEnable()
    {
        content.alpha = 0f;
        content.interactable = content.blocksRaycasts = false;
        // ProductionSceneComposition initializes these shared services before scene UI.
        currency = CurrencyManager.Instance;
        progression = MetaProgressionManager.Instance;
        localization = LocalizationService.Instance;
        if (currency == null || progression == null || localization == null)
        {
            Debug.LogError("[BunkerHudView] Bunker composition services are missing.", this);
            enabled = false;
            return;
        }

        currency.OnGoldUpdated += RefreshGold;
        progression.ProgressChanged += RefreshObjective;
        localization.LanguageChanged += HandleLanguageChanged;
        RefreshGold(currency.TotalGold);
        RefreshObjective();
    }

    private void OnDisable()
    {
        if (currency != null) currency.OnGoldUpdated -= RefreshGold;
        if (progression != null) progression.ProgressChanged -= RefreshObjective;
        if (localization != null) localization.LanguageChanged -= HandleLanguageChanged;
        content.alpha = 0f;
    }

    private void RefreshGold(int value) => gold.SetValue(value);
    private void HandleLanguageChanged(GameLanguage language) => RefreshObjective();

    private void RefreshObjective()
    {
        string key = depths.GetGlobalObjectiveKey(progression.EscapeAccess);
        bool hasGoal = !string.IsNullOrWhiteSpace(key) && localization.HasKey(key);
        string text = hasGoal ? localization.Get(key) : string.Empty;
        objectiveRoot.SetActive(!string.IsNullOrWhiteSpace(text));
        objectiveTitle.text = localization.Get("bunker.hud.objective_title");
        objectiveText.text = text;
    }

    private void LateUpdate()
    {
        bool visible = Time.timeScale > 0f && !SceneTransitionOverlay.IsTransitioning &&
            (panels == null || !panels.IsAnyPanelOpen) && (intro == null || !intro.IsPlaying);
        if (visible && minigames != null)
            foreach (var game in minigames)
                if (game != null && game.isActiveAndEnabled && game.IsRunning)
                {
                    visible = false;
                    break;
                }
        // Keep the presenter enabled while hidden so modes can restore it and
        // currency/progression events keep the next visible frame up to date.
        content.alpha = visible ? 1f : 0f;
    }
}
