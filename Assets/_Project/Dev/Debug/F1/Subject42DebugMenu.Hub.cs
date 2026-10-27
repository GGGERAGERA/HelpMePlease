#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subject42.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class Subject42DebugMenu
{
    private sealed class HubCommand
    {
        public string Id, Title, Description;
        public int Category;
        public Func<bool> Available;
        public Action Run;
    }

    [Serializable]
    private sealed class HubSnapshot
    {
        public string CapturedUtc, Project, Scene, UnityVersion, RunPhase;
        public float FrameMilliseconds, TimeScale;
        public int EditorOrPlayerPid, Enemies, Sector, Rings, Modules, Warnings, Errors;
        public Vector3 PlayerPosition;
        public string[] RecentLogs;
    }

    private const string HubFavoritesKey = "Subject42.DebugHub.Favorites.v1";
    private static readonly string[] HubCategories = { "Визуал", "Забег", "Бой и события", "Диагностика" };
    private readonly List<HubCommand> hubCommands = new();
    private readonly HashSet<string> hubFavorites = new(StringComparer.Ordinal);
    private readonly Queue<string> hubLogs = new();
    private readonly List<(Button Button, HubCommand Command)> hubCommandButtons = new();
    private GameObject hubRoot;
    private RectTransform hubPanel, hubContent;
    private TextMeshProUGUI hubTelemetry, hubStatusText;
    private readonly List<Image> hubCategoryImages = new();
    private TMP_InputField hubSearch;
    private int hubCategory, hubWarnings, hubErrors;
    private bool hubOnlyFavorites, hubPointerHeld, hubPreviousCursorVisible;
    private CursorLockMode hubPreviousCursorLock;
    private Coroutine hubTelemetryRoutine;
    // Legacy labs remain reachable from the new navigation in the production MVP.
    private bool hubLegacyLab;

    private bool IsHubOpen => hubRoot != null && hubRoot.activeSelf;

    public static bool HubBlocksGameplayPointer => activeInstance != null && activeInstance.HubOwnsPointer();
    public static bool HubBlocksKeyboardInput => activeInstance != null && activeInstance.IsHubOpen &&
        activeInstance.hubSearch != null && activeInstance.hubSearch.isFocused;

    private bool HubOwnsPointer()
    {
        bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
        if (!held) hubPointerHeld = false;
        bool over = IsHubOpen && RectTransformUtility.RectangleContainsScreenPoint(hubPanel, Input.mousePosition);
        if (over && held) hubPointerHeld = true;
        return over || hubPointerHeld || IsHubOpen && hubSearch != null && hubSearch.isFocused;
    }

    private void BuildHub()
    {
        if (hubRoot != null) return;
        foreach (string id in PlayerPrefs.GetString(HubFavoritesKey, "").Split('|'))
            if (!string.IsNullOrEmpty(id)) hubFavorites.Add(id);
        ConfigureHubCommands();
        hubRoot = new GameObject("Subject42 Debug Hub", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = hubRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;
        var scaler = hubRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = .5f;
        hubPanel = CreateRect("Debug Hub Panel", hubRoot.transform);
        hubPanel.anchorMin = hubPanel.anchorMax = new Vector2(.5f, .5f);
        hubPanel.sizeDelta = new Vector2(600f, 660f);
        hubPanel.gameObject.AddComponent<Image>().color = panelColor;
        var title = CreateText("Heading", hubPanel, "Отладка · F1", 23f, TextAlignmentOptions.MidlineLeft, Color.white);
        HubPlace(title.rectTransform, 18f, 10f, 465f, 42f);
        var close = CreateButton(hubPanel, "×", CloseHub, 36f);
        HubPlace(close.GetComponent<RectTransform>(), 548f, 16f, 36f, 32f);
        hubTelemetry = CreateText("Telemetry", hubPanel, "", 12f, TextAlignmentOptions.MidlineLeft, mutedColor);
        HubPlace(hubTelemetry.rectTransform, 18f, 52f, 565f, 24f);
        for (int i = 0; i < HubCategories.Length; i++)
        {
            int selected = i;
            var tab = CreateButton(hubPanel, HubCategories[i], () => { hubCategory = selected; RefreshHub(); }, 138f);
            HubPlace(tab.GetComponent<RectTransform>(), 18f + i * 142f, 84f, 138f, 36f);
            tab.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14f;
            hubCategoryImages.Add(tab.targetGraphic as Image);
        }
        var searchRect = CreateRect("Command Search", hubPanel);
        HubPlace(searchRect, 18f, 132f, 420f, 36f);
        searchRect.gameObject.AddComponent<Image>().color = rowColor;
        hubSearch = searchRect.gameObject.AddComponent<TMP_InputField>();
        var viewport = CreateRect("Text Area", searchRect);
        Stretch(viewport, 10f, 10f, 2f, 2f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var placeholder = CreateText("Placeholder", viewport, "Поиск по всем командам…", 15f,
            TextAlignmentOptions.MidlineLeft, mutedColor);
        Stretch(placeholder.rectTransform);
        var inputText = CreateText("Text", viewport, "", 15f, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(inputText.rectTransform);
        hubSearch.textViewport = viewport;
        hubSearch.textComponent = inputText;
        hubSearch.placeholder = placeholder;
        hubSearch.characterLimit = 128;
        hubSearch.onValueChanged.AddListener(_ => RefreshHub());
        var favorites = CreateButton(hubPanel, "Избранное", () => { hubOnlyFavorites = !hubOnlyFavorites; RefreshHub(); }, 138f);
        HubPlace(favorites.GetComponent<RectTransform>(), 446f, 132f, 138f, 36f);
        var scrollRect = CreateRect("Commands", hubPanel);
        Stretch(scrollRect, 18f, 18f, 70f, 180f);
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        var scrollViewport = CreateRect("Viewport", scrollRect);
        Stretch(scrollViewport);
        scrollViewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .01f);
        scrollViewport.gameObject.AddComponent<RectMask2D>();
        hubContent = CreateRect("Content", scrollViewport);
        hubContent.anchorMin = new Vector2(0f, 1f); hubContent.anchorMax = Vector2.one;
        hubContent.pivot = new Vector2(.5f, 1f); hubContent.sizeDelta = Vector2.zero;
        var layout = hubContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 7f; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = hubContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = scrollViewport; scroll.content = hubContent;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;
        hubStatusText = CreateText("Status", hubPanel, "Панель работает без паузы. F1 / Esc — закрыть.", 12f,
            TextAlignmentOptions.TopLeft, mutedColor);
        HubPlace(hubStatusText.rectTransform, 18f, 603f, 565f, 44f);
        hubStatusText.textWrappingMode = TextWrappingModes.Normal;
        Application.logMessageReceived += ObserveHubLog;
        hubRoot.SetActive(false);
    }

    private static void HubPlace(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void ConfigureHubCommands()
    {
        void Add(string id, int category, string title, string description, Action run, Func<bool> available = null)
            => hubCommands.Add(new HubCommand { Id = id, Category = category, Title = title,
                Description = description, Run = run, Available = available ?? (() => true) });
        void Studio() { CloseHub(); LiveVisualStudio.Open(); }
        Add("visual.studio", 0, "Live Visual Studio", "Выбор объекта, живые настройки и профили", Studio);
        Add("visual.original", 0, "Сравнить: Original", "Применить поддерживаемые значения из Git-снимка", () =>
            { LiveVisualRuntime.Instance.SetMode(VisualComparisonMode.Original); Studio(); }, () => LiveVisualRuntime.Instance != null);
        Add("visual.current", 0, "Сравнить: Current", "Вернуть текущее исходное состояние рабочей копии", () =>
            { LiveVisualRuntime.Instance.SetMode(VisualComparisonMode.Current); Studio(); }, () => LiveVisualRuntime.Instance != null);
        Add("visual.custom", 0, "Настройки Custom и пресеты", "Вернуться к своим настройкам; Save / Load в студии", () =>
            { LiveVisualRuntime.Instance.SetMode(VisualComparisonMode.Custom); Studio(); }, () => LiveVisualRuntime.Instance != null);
        Add("run.tools", 1, "Текущий забег", "Здоровье, опыт, награды, штатный выход в бункер", () => HubOpenDetails(3, DebugTab.PlayerBuild));
        Add("run.sectors", 1, "Секторы и босс", "Штатный production flow и диагностика этапов", () => HubOpenLegacyTool(DebugTab.WorldEvents), () => characterSpawner != null);
        Add("run.catalogue", 1, "Награды и характеристики", "Существующий каталог улучшений", () => HubOpenDetails(1, DebugTab.PlayerBuild), () => characterSpawner != null);
        Add("run.bunker", 1, "Инструменты бункера", "Двери, станции и существующие debug API", () => HubOpenLegacyTool(DebugTab.Bunker), () => IsProductionBunker);
        Add("combat.enemies", 2, "Враги и события", "Типы врагов, ручной спавн, AI и события сектора", () => HubOpenDetails(2, DebugTab.WorldEvents), () => enemySpawner != null);
        Add("combat.weapons", 2, "Орбиты и оружие", "Существующие инструменты станции и каталог оружия", () => HubOpenDetails(0, DebugTab.OrbitalProduction), () => characterSpawner != null);
        Add("combat.feel", 2, "FEEL LAB / Combat Lab", "Сохранённые боевые лаборатории и тестовые сценарии", () => HubOpenLegacyTool(DebugTab.FeelTest), () => characterSpawner != null);
        Add("diagnostics.snapshot", 3, "Диагностический снимок", "Состояние production-систем и последние 30 записей лога", SaveHubSnapshot);
        Add("diagnostics.logs", 3, "Последние логи и исключения", "Ограниченный журнал текущего запуска", () => { hubStatusText.text = hubLogs.Count == 0 ? "С момента создания меню записей нет." : hubLogs.Last(); ShowHubLogs(); });
        Add("diagnostics.tools", 3, "Существующие QA-инструменты", "Исходная навигация инструментов и тестов", () => HubOpenLegacyTool(DebugTab.Additional));
    }

    private void ToggleHub()
    {
        if (isOpen) { CloseMenu(); hubLegacyLab = false; return; }
        if (IsHubOpen) { CloseHub(); return; }
        hubLegacyLab = false;
        BuildHub();
        ResolveSceneReferences();
        hubPreviousCursorVisible = Cursor.visible; hubPreviousCursorLock = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        hubRoot.SetActive(true);
        RefreshHub();
        hubTelemetryRoutine = StartCoroutine(RefreshHubTelemetry());
    }

    private void CloseHub()
    {
        if (!IsHubOpen) return;
        hubSearch.DeactivateInputField();
        hubRoot.SetActive(false);
        if (hubTelemetryRoutine != null) StopCoroutine(hubTelemetryRoutine);
        hubTelemetryRoutine = null;
        Cursor.visible = hubPreviousCursorVisible; Cursor.lockState = hubPreviousCursorLock;
    }

    private void DisposeHub()
    {
        Application.logMessageReceived -= ObserveHubLog;
        CloseHub();
        if (hubRoot != null) Destroy(hubRoot);
    }

    private void RefreshHub()
    {
        if (hubContent == null) return;
        hubCommandButtons.Clear();
        foreach (Transform child in hubContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = 0; i < hubCategoryImages.Count; i++)
            hubCategoryImages[i].color = !hubOnlyFavorites && hubCategory == i ? new Color(.2f, .73f, .88f, 1f) : accentColor;
        string query = hubSearch.text.Trim();
        var commands = hubCommands.Where(command => (!hubOnlyFavorites || hubFavorites.Contains(command.Id)) &&
            (query.Length > 0 ? (command.Title + " " + command.Description).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 :
                hubOnlyFavorites || command.Category == hubCategory));
        int count = 0;
        foreach (var command in commands)
        {
            count++;
            var row = CreateRect(command.Id, hubContent);
            row.gameObject.AddComponent<Image>().color = rowColor;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 69f;
            var button = CreateButton(row, "", () => { if (command.Available()) command.Run(); }, 1f, true);
            Stretch(button.GetComponent<RectTransform>(), 44f, 0f);
            button.targetGraphic.color = rowColor;
            button.interactable = command.Available();
            hubCommandButtons.Add((button, command));
            var caption = button.GetComponentInChildren<TextMeshProUGUI>();
            caption.text = "<b>" + command.Title + "</b>\n<size=12>" + command.Description + "</size>";
            caption.fontSize = 17f; caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.margin = new Vector4(8f, 0f, 8f, 0f);
            bool favorite = hubFavorites.Contains(command.Id);
            var star = CreateButton(row, favorite ? "*" : "+", () =>
            {
                if (!hubFavorites.Add(command.Id)) hubFavorites.Remove(command.Id);
                PlayerPrefs.SetString(HubFavoritesKey, string.Join("|", hubFavorites.OrderBy(id => id, StringComparer.Ordinal)));
                PlayerPrefs.Save(); RefreshHub();
            }, 36f);
            HubPlace(star.GetComponent<RectTransform>(), 4f, 17f, 34f, 34f);
        }
        if (count == 0)
        {
            var empty = CreateText("Empty", hubContent, hubOnlyFavorites ? "Добавьте команды кнопкой +." : "Команды не найдены.", 16f, TextAlignmentOptions.Center, mutedColor);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 65f;
        }
        hubStatusText.text = hubOnlyFavorites ? "Избранное · нажатие * убирает команду." : "F1 / Esc — закрыть. Live Visual Studio открывается отдельно.";
    }

    private IEnumerator RefreshHubTelemetry()
    {
        var interval = new WaitForSecondsRealtime(.25f);
        while (IsHubOpen)
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            string phase = runFlowController != null ? runFlowController.Phase.ToString() : "—";
            hubTelemetry.text = $"{(ms > 0f ? 1000f / ms : 0f):0} FPS · {ms:0.0} ms · {SceneManager.GetActiveScene().name} · {phase} · W:{hubWarnings} E:{hubErrors}";
            foreach (var item in hubCommandButtons) item.Button.interactable = item.Command.Available();
            yield return interval;
        }
    }

    private void HubOpenDetails(int mvpTab, DebugTab otherTab)
    {
        CloseHub(); hubLegacyLab = false;
        SelectTab(IsMvpDebugMenu ? (DebugTab)mvpTab : otherTab);
        OpenMenu();
        HubApplyDetailsLayout();
    }

    private void HubOpenLegacyTool(DebugTab tab)
    {
        CloseHub(); hubLegacyLab = true;
        if (IsRuntimeLabTab(tab)) EnsureProductionSectorDebug();
        // Additional/QA pages can navigate to any existing lab internally.
        // Create the small page shells together; rows are still built only on demand.
        for (int i = 0; i < TabLabels.Length; i++)
        {
            if (tabRoots[i] != null) continue;
            tabRoots[i] = CreateTabPage(TabLabels[i], menuPages, out _, true);
            if (i == (int)DebugTab.FeelTest) BuildFeelActionBar(tabRoots[i].transform);
            if (i == (int)DebugTab.VisualTest) BuildVisualActionBar(tabRoots[i].transform);
        }
        SelectTab(tab);
        OpenMenu();
        HubApplyDetailsLayout();
    }

    private void HubApplyDetailsLayout()
    {
        if (!isOpen || menuPanel == null || mvpOrbitSurfaceOpen) return;
        // These are details launched by the hub, not a second competing navigation.
        menuTabBar.gameObject.SetActive(false);
        menuPages.offsetMax = new Vector2(menuPages.offsetMax.x, IsRuntimeLabTab(activeTab) ? -46f : -70f);
        visualBackButton.SetActive(true);
        var back = visualBackButton.GetComponent<Button>();
        var rect = back.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(-78f, 0f);
        rect.sizeDelta = new Vector2(72f, 30f);
        Stretch(menuTitle.rectTransform, 18f, 166f);
        back.onClick.RemoveAllListeners();
        back.onClick.AddListener(() => { CloseMenu(); hubLegacyLab = false; ToggleHub(); });
    }

    private void ObserveHubLog(string message, string stack, LogType type)
    {
        if (type == LogType.Warning) hubWarnings++;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) hubErrors++;
        string line = type + ": " + message;
        if (line.Length > 2000) line = line.Substring(0, 2000) + "…";
        hubLogs.Enqueue(line);
        while (hubLogs.Count > 30) hubLogs.Dequeue();
    }

    private void ShowHubLogs()
    {
        hubCommandButtons.Clear();
        foreach (Transform child in hubContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var back = CreateButton(hubContent, "← К командам", RefreshHub, 1f);
        back.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        foreach (string log in hubLogs.Reverse())
        {
            var text = CreateText("Log", hubContent, log, 13f, TextAlignmentOptions.TopLeft, Color.white);
            text.richText = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Clamp(text.GetPreferredValues(log, 540f, 0f).y + 12f, 32f, 180f);
        }
    }

    private void SaveHubSnapshot()
    {
        try
        {
#if UNITY_EDITOR
            string directory = Path.GetFullPath("Artifacts/GeneratedQA/LiveVisualStudio");
#else
            string directory = Path.Combine(Application.persistentDataPath, "DebugSnapshots");
#endif
            Directory.CreateDirectory(directory);
            var station = MvpStation;
            var player = PlayerRuntimeReference.CachedPlayer;
            var snapshot = new HubSnapshot
            {
                CapturedUtc = DateTime.UtcNow.ToString("O"), Project = Path.GetDirectoryName(Application.dataPath),
                EditorOrPlayerPid = System.Diagnostics.Process.GetCurrentProcess().Id,
                Scene = SceneManager.GetActiveScene().path, UnityVersion = Application.unityVersion,
                RunPhase = runFlowController != null ? runFlowController.Phase.ToString() : "none",
                FrameMilliseconds = Time.unscaledDeltaTime * 1000f, TimeScale = Time.timeScale,
                Enemies = EnemyHealth.ActiveInstances.Count, Sector = RunStateManager.Instance?.CurrentSector?.SectorNumber ?? 0,
                Rings = station?.Rings.Count ?? 0, Modules = station?.Modules.Count ?? 0,
                PlayerPosition = player != null ? player.transform.position : Vector3.zero,
                Warnings = hubWarnings, Errors = hubErrors, RecentLogs = hubLogs.ToArray()
            };
            string path = Path.Combine(directory, "snapshot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
            hubStatusText.text = "Снимок: " + path;
        }
        catch (Exception exception) { hubStatusText.text = "Снимок не сохранён: " + exception.Message; }
    }
}
#endif
