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
    private static readonly string[] HubCategories =
    {
        "ПЕСОЧНИЦА", "Билд и улучшения", "Враги и бой",
        "Мир и прогресс", "Визуал", "QA / Лаборатории"
    };
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
    // Utility pages share the same menu shell as the indexed MVP catalogue.
    private bool hubUtilityPage;
    private readonly Stack<(bool IsHub, bool Legacy, DebugTab Tab, int Category)> hubHistory = new();
    private GameObject hubSearchButton;

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
            if (!string.IsNullOrEmpty(id))
                hubFavorites.Add(id is "visual.original" or "visual.current" or "visual.custom" ? "visual.studio" :
                    id == "run.sectors" ? "run.tools" : id);
        ConfigureHubCommands();
        hubFavorites.RemoveWhere(id => hubCommands.All(command => command.Id != id));
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
        HubPlace(title.rectTransform, 18f, 10f, 420f, 42f);
        var back = CreateButton(hubPanel, "← Назад", HubGoBack, 88f);
        HubPlace(back.GetComponent<RectTransform>(), 452f, 16f, 88f, 32f);
        var close = CreateButton(hubPanel, "×", CloseHub, 36f);
        HubPlace(close.GetComponent<RectTransform>(), 548f, 16f, 36f, 32f);
        hubTelemetry = CreateText("Telemetry", hubPanel, "", 12f, TextAlignmentOptions.MidlineLeft, mutedColor);
        HubPlace(hubTelemetry.rectTransform, 18f, 52f, 565f, 24f);
        for (int i = 0; i < HubCategories.Length; i++)
        {
            int selected = i;
            const float width = 186f;
            var tab = CreateButton(hubPanel, HubCategories[i], () =>
            {
                hubCategory = selected;
                RefreshHub();
            }, width, i != 0 || IsSandboxScene);
            HubPlace(tab.GetComponent<RectTransform>(), 18f + i % 3 * 190f, 84f + i / 3 * 40f, width, 36f);
            tab.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14f;
            hubCategoryImages.Add(tab.targetGraphic as Image);
        }
        var searchRect = CreateRect("Command Search", hubPanel);
        HubPlace(searchRect, 18f, 172f, 420f, 36f);
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
        HubPlace(favorites.GetComponent<RectTransform>(), 446f, 172f, 138f, 36f);
        var scrollRect = CreateRect("Commands", hubPanel);
        Stretch(scrollRect, 18f, 18f, 70f, 220f);
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
        Add("sandbox.open", 0, "ПЕСОЧНИЦА", "Быстрая подготовка билда и боя", OpenSandbox, () => IsSandboxScene);
        Add("combat.weapons", 1, "Оружие, модули и орбиты", "Полный каталог Segmented: запас, открытие орбит и лимит оружия", () => HubOpenDetails(0, DebugTab.OrbitalProduction), () => characterSpawner != null);
        Add("run.catalogue", 1, "Карточки и улучшения", "Каталог характеристик игрока и оружия, штатный выбор награды", () => HubOpenDetails(1, DebugTab.PlayerBuild), () => characterSpawner != null);
        Add("combat.enemies", 2, "Враги и бой", "Ручной спавн, плотность, AI, специальные враги и тестовый босс", () => HubOpenDetails(2, DebugTab.WorldEvents), () => enemySpawner != null);
        Add("run.tools", 3, "Мир и прогресс", "Здоровье, XP, pickups, события, World Rules и переходы", () => HubOpenDetails(3, DebugTab.PlayerBuild));
        if (IsProductionBunker)
            Add("run.bunker", 3, "Инструменты бункера", "Двери, станции и существующие debug API", () => HubOpenUtilityPage(DebugTab.Bunker));
        Add("visual.studio", 4, "Live Visual Studio", "Живые настройки, Original / Current / Custom и пресеты", Studio, () => LiveVisualRuntime.Instance != null);
        Add("visual.segmented", 4, "Внешний вид Segmented · F9", "Действующие настройки линий и оформления орбит", () => { CloseHub(); ToggleMvpOrbitSurface(); }, () => IsSandboxScene && MvpCanEdit);
        Add("diagnostics.snapshot", 5, "Диагностический снимок", "Состояние production-систем и последние 30 записей лога", SaveHubSnapshot);
        Add("diagnostics.logs", 5, "Последние логи и исключения", "Ограниченный журнал текущего запуска", ShowHubLogs);
        Add("diagnostics.tools", 5, "Боты, эксперименты и расширенные инструменты", "Боты, Golden Path, варианты пистолета и снайперки, восемь экспериментов врагов и XP-лаборатория", () => HubOpenUtilityPage(DebugTab.Additional));
    }

    private void ToggleHub()
    {
        if (isOpen) { CloseMenu(); hubUtilityPage = false; return; }
        if (IsHubOpen) { CloseHub(); return; }
        hubHistory.Clear();
        if (IsSandboxScene) OpenSandbox();
        else ShowHub();
    }

    private void ShowHub()
    {
        HubShowCommands(true);
    }

    private void HubShowCommands(bool remember)
    {
        if (remember && isOpen)
            hubHistory.Push((false, hubUtilityPage, activeTab, hubCategory));
        CloseMenu();
        hubUtilityPage = false;
        BuildHub();
        ResolveSceneReferences();
        if (IsHubOpen) { RefreshHub(); return; }
        hubPreviousCursorVisible = Cursor.visible; hubPreviousCursorLock = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        hubRoot.SetActive(true);
        RefreshHub();
        if (hubTelemetryRoutine == null) hubTelemetryRoutine = StartCoroutine(RefreshHubTelemetry());
    }

    private void HubOpenSection(int category)
    {
        switch (category)
        {
            case 0: OpenSandbox(); break;
            case 1: HubOpenDetails(0, DebugTab.OrbitalProduction); break;
            case 2: HubOpenDetails(2, DebugTab.WorldEvents); break;
            case 3:
                if (IsProductionBunker) HubOpenUtilityPage(DebugTab.Bunker);
                else HubOpenDetails(3, DebugTab.PlayerBuild);
                break;
            case 4: ShowHub(); hubCategory = category; RefreshHub(); break;
            case 5: HubOpenUtilityPage(DebugTab.Additional); break;
        }
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
        int category = mvpTab <= 1 ? 1 : mvpTab;
        HubShowDetails(false, SceneManager.GetActiveScene().name == "MVP" ? (DebugTab)mvpTab : otherTab, category);
    }

    private void HubOpenUtilityPage(DebugTab tab)
    {
        int category = tab == DebugTab.Sandbox ? 0 : tab == DebugTab.Bunker || tab == DebugTab.WorldEvents ? 3 : 5;
        HubShowDetails(true, tab, category);
    }

    private void HubShowDetails(bool legacy, DebugTab tab, int category, bool remember = true)
    {
        if (isOpen && !mvpOrbitSurfaceOpen && hubUtilityPage == legacy && activeTab == tab) return;
        if (remember && isOpen && !mvpOrbitSurfaceOpen)
            hubHistory.Push((false, hubUtilityPage, activeTab, hubCategory));
        else if (remember && IsHubOpen)
            hubHistory.Push((true, false, default, hubCategory));
        CloseMenu();
        CloseHub();
        hubUtilityPage = legacy;
        hubCategory = category;
        // Reuse page shells; content is built only when the page is selected.
        for (int i = 0; i < TabLabels.Length; i++)
        {
            if (tabRoots[i] != null) continue;
            tabRoots[i] = CreateTabPage(TabLabels[i], menuPages, out _, true);
        }
        SelectTab(tab, false);
        OpenMenu();
        HubApplyDetailsLayout();
    }

    private void HubGoBack()
    {
        if (hubHistory.Count == 0) { HubShowCommands(false); return; }
        var page = hubHistory.Pop();
        if (page.IsHub)
        {
            hubCategory = page.Category;
            HubShowCommands(false);
        }
        else HubShowDetails(page.Legacy, page.Tab, page.Category, false);
    }

    private void HubApplyDetailsLayout()
    {
        if (!isOpen || menuPanel == null || mvpOrbitSurfaceOpen) return;
        menuTabBar.gameObject.SetActive(true);
        menuPages.offsetMax = new Vector2(menuPages.offsetMax.x, -120f);
        hubSearchButton.SetActive(true);
        visualBackButton.SetActive(true);
        var back = visualBackButton.GetComponent<Button>();
        var rect = back.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(-192f, 0f);
        rect.sizeDelta = new Vector2(72f, 30f);
        back.GetComponentInChildren<TextMeshProUGUI>().text = "НАЗАД";
        Stretch(menuTitle.rectTransform, 18f, 278f);
        back.onClick.RemoveAllListeners();
        back.onClick.AddListener(HubGoBack);
        for (int i = 0; i < HubCategories.Length; i++)
            if (tabButtonImages[i] != null)
                tabButtonImages[i].color = hubCategory == i ? new Color(.2f, .73f, .88f, 1f) : accentColor;
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
