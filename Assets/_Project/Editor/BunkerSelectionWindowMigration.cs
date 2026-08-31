#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BunkerSelectionWindowMigration
{
    private const string Folder = "Assets/_Project/prefabs/UI/Stations";
    private const string BaseWindowPath = Folder + "/StationWindow.prefab";
    private const string WindowPath = Folder + "/BunkerSelectionWindow.prefab";
    private const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleMissingProductionMigration()
    {
        if (Application.isBatchMode || IsMainMenuMigrated())
            return;
        EditorApplication.delayCall += TryRunMissingProductionMigration;
    }

    private static void TryRunMissingProductionMigration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunMissingProductionMigration;
            return;
        }

        if (!IsMainMenuMigrated())
            BuildAndMigrate();
    }

    private static bool IsMainMenuMigrated()
    {
        string sourceGuid = AssetDatabase.AssetPathToGUID(
            "Assets/_Project/scripts/Selection/BunkerSelectionSourceHub.cs");
        string holdInputGuid = AssetDatabase.AssetPathToGUID(
            "Assets/_Project/scripts/Selection/Characters/HoldInvestmentInput.cs");
        return !string.IsNullOrWhiteSpace(sourceGuid) &&
            !string.IsNullOrWhiteSpace(holdInputGuid) &&
            System.IO.File.Exists(MainMenuPath) &&
            System.IO.File.ReadAllText(MainMenuPath).Contains(sourceGuid) &&
            System.IO.File.ReadAllText(MainMenuPath).Contains("RequiredStationLevel:") &&
            System.IO.File.Exists(WindowPath) &&
            System.IO.File.ReadAllText(WindowPath).Contains("itemProgression:") &&
            System.IO.File.ReadAllText(WindowPath).Contains(holdInputGuid);
    }

    [MenuItem("Tools/Subject42/Build and Migrate Shared Bunker Selection")]
    public static void BuildAndMigrate()
    {
        GameObject card = BuildCard();
        GameObject window = BuildWindow(card);
        MigrateMainMenu(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BunkerSelectionMigration] Shared selection prefab and MainMenu migration complete.");
    }

    private static GameObject BuildCard()
    {
        GameObject root = Ui("BunkerSelectionCard");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(270f, 300f);
        Image background = root.AddComponent<Image>();
        background.color = StationPixelVisuals.PanelRaised;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        ConfigureButton(button);

        RectTransform iconRect = Ui("Icon", root.transform).GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.22f);
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(14f, 12f);
        iconRect.offsetMax = new Vector2(-14f, -14f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        RectTransform band = Ui("NameBand", root.transform).GetComponent<RectTransform>();
        band.anchorMin = Vector2.zero;
        band.anchorMax = new Vector2(1f, 0.22f);
        band.offsetMin = new Vector2(8f, 8f);
        band.offsetMax = new Vector2(-8f, -4f);
        Image bandImage = band.gameObject.AddComponent<Image>();
        bandImage.color = StationPixelVisuals.Window;
        bandImage.raycastTarget = false;
        TextMeshProUGUI name = Text("Name", band, "ITEM", 22f, StationPixelVisuals.Text,
            TextAlignmentOptions.Center);
        Stretch(name.rectTransform, 6f, 3f);
        name.fontStyle = FontStyles.Bold;

        GameObject selectedFrame = Ui("SelectedFrame", root.transform);
        Stretch(selectedFrame.GetComponent<RectTransform>(), 1f, 1f);
        Image selectedGraphic = selectedFrame.AddComponent<Image>();
        selectedGraphic.color = new Color(0f, 0f, 0f, 0f);
        selectedGraphic.raycastTarget = false;
        Outline selectedOutline = selectedFrame.AddComponent<Outline>();
        selectedOutline.effectColor = StationPixelVisuals.Cyan;
        selectedOutline.effectDistance = new Vector2(3f, -3f);
        selectedFrame.SetActive(false);

        GameObject lockedOverlay = Ui("LockedOverlay", root.transform);
        Stretch(lockedOverlay.GetComponent<RectTransform>(), 0f, 0f);
        Image lockedGraphic = lockedOverlay.AddComponent<Image>();
        lockedGraphic.color = new Color(0.01f, 0.02f, 0.025f, 0.68f);
        lockedGraphic.raycastTarget = false;
        TextMeshProUGUI lockedText = Text("LockedText", lockedOverlay.transform,
            "ЗАКРЫТО", 20f, StationPixelVisuals.MutedText, TextAlignmentOptions.Center);
        Stretch(lockedText.rectTransform, 10f, 10f);
        lockedText.fontStyle = FontStyles.Bold;

        BunkerSelectionCardView view = root.AddComponent<BunkerSelectionCardView>();
        Assign(view, "background", background);
        Assign(view, "icon", icon);
        Assign(view, "nameText", name);
        Assign(view, "selectedFrame", selectedFrame);
        Assign(view, "lockedOverlay", lockedOverlay);
        Assign(view, "lockedText", lockedText);
        Assign(view, "button", button);

        // Cards are a reusable template inside the one production window.
        // Keeping the template embedded avoids a second production prefab.
        root.name = "BunkerSelectionCardTemplate";
        root.SetActive(false);
        return root;
    }

    private static GameObject BuildWindow(GameObject cardPrefab)
    {
        GameObject baseWindow = AssetDatabase.LoadAssetAtPath<GameObject>(BaseWindowPath);
        GameObject root = baseWindow != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(baseWindow)
            : BuildFallbackStationShell();
        root.name = "BunkerSelectionWindow";
        StationUIShell shell = root.GetComponent<StationUIShell>();
        if (shell == null)
            throw new InvalidOperationException("StationWindow.prefab has no StationUIShell.");

        ClearChildren(shell.MainContent);
        ClearChildren(shell.InfoPanel);

        TextMeshProUGUI title = shell.Header.Find("Title").GetComponent<TextMeshProUGUI>();
        title.text = "ВЫБОР";

        VerticalLayoutGroup mainLayout = shell.MainContent.gameObject.AddComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(28, 28, 20, 24);
        mainLayout.spacing = 10f;
        mainLayout.childControlWidth = true;
        mainLayout.childControlHeight = true;
        mainLayout.childForceExpandWidth = true;
        mainLayout.childForceExpandHeight = false;
        TextMeshProUGUI sectionTitle = Text("SectionTitle", shell.MainContent,
            "ДОСТУПНЫЕ ЭЛЕМЕНТЫ", 18f, StationPixelVisuals.Cyan,
            TextAlignmentOptions.MidlineLeft);
        sectionTitle.fontStyle = FontStyles.Bold;
        Layout(sectionTitle.rectTransform, -1f, 30f, 1f, 0f);

        RectTransform scrollRoot = Ui("CardsArea", shell.MainContent).GetComponent<RectTransform>();
        Layout(scrollRoot, -1f, -1f, 1f, 1f);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform viewport = Ui("Viewport", scrollRoot).GetComponent<RectTransform>();
        Stretch(viewport, 0f, 0f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform cardsRoot = Ui("Cards", viewport).GetComponent<RectTransform>();
        cardsRoot.anchorMin = new Vector2(0f, 1f);
        cardsRoot.anchorMax = new Vector2(1f, 1f);
        cardsRoot.pivot = new Vector2(0.5f, 1f);
        cardsRoot.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = cardsRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(270f, 300f);
        grid.spacing = new Vector2(20f, 18f);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        ContentSizeFitter fitter = cardsRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = cardsRoot;

        BunkerSelectionDetailView detail = BuildDetail(shell.InfoPanel, out BunkerProgressionView itemProgress);
        BunkerStationProgressView progress = BuildProgress(shell.StationProgressPanel);

        Button back = shell.Footer.Find("BackButton").GetComponent<Button>();
        Button confirm = shell.Footer.Find("PrimaryActionButton").GetComponent<Button>();
        TextMeshProUGUI confirmLabel = confirm.GetComponentInChildren<TextMeshProUGUI>();
        confirmLabel.text = "ВЫБРАТЬ";
        Button settings = Button("SettingsButton", shell.Footer, "НАСТРОЙКИ");
        SetAnchored((RectTransform)settings.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(240f, 60f), new Vector2(0.5f, 0.5f));

        BunkerSelectionWindow window = root.AddComponent<BunkerSelectionWindow>();
        if (!EditorUtility.IsPersistent(cardPrefab))
        {
            cardPrefab.transform.SetParent(cardsRoot, false);
            cardPrefab.SetActive(false);
        }
        Assign(window, "titleText", title);
        Assign(window, "sectionTitleText", sectionTitle);
        Assign(window, "cardsRoot", cardsRoot);
        Assign(window, "cardPrefab", cardPrefab.GetComponent<BunkerSelectionCardView>());
        Assign(window, "detailView", detail);
        Assign(window, "itemProgression", itemProgress);
        Assign(window, "stationProgress", progress);
        Assign(window, "backButton", back);
        Assign(window, "confirmButton", confirm);
        Assign(window, "confirmButtonText", confirmLabel);
        Assign(window, "settingsButton", settings);

        root.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            root,
            WindowPath,
            InteractionMode.AutomatedAction);
        if (prefab != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
        return root;
    }

    private static GameObject BuildFallbackStationShell()
    {
        GameObject root = Ui("StationWindow");
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
        Image rootImage = root.AddComponent<Image>();
        rootImage.color = StationPixelVisuals.Window;
        Outline rootOutline = root.AddComponent<Outline>();
        rootOutline.effectColor = StationPixelVisuals.SectionBorder;
        rootOutline.effectDistance = new Vector2(2f, -2f);
        VerticalLayoutGroup windowLayout = root.AddComponent<VerticalLayoutGroup>();
        windowLayout.padding = new RectOffset(48, 48, 28, 28);
        windowLayout.spacing = 12f;
        windowLayout.childAlignment = TextAnchor.UpperCenter;
        windowLayout.childControlWidth = true;
        windowLayout.childControlHeight = true;
        windowLayout.childForceExpandWidth = true;
        windowLayout.childForceExpandHeight = false;

        RectTransform header = Section("Header", root.transform);
        Layout(header, -1f, 104f, 1f, 0f);
        TextMeshProUGUI title = Text("Title", header, "НАЗВАНИЕ СТАНЦИИ", 48f,
            StationPixelVisuals.Text, TextAlignmentOptions.Center);
        Stretch(title.rectTransform, 24f, 14f);
        title.fontStyle = FontStyles.Bold;

        RectTransform body = Ui("Body", root.transform).GetComponent<RectTransform>();
        Layout(body, -1f, -1f, 1f, 1f);
        HorizontalLayoutGroup bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 20f;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = true;
        RectTransform main = Section("MainContent", body);
        Layout(main, -1f, -1f, 60f, 1f);
        RectTransform info = Section("InfoPanel", body);
        Layout(info, -1f, -1f, 40f, 1f);

        RectTransform progress = Section("StationProgressPanel", root.transform);
        Layout(progress, -1f, 136f, 1f, 0f);
        TextMeshProUGUI stationName = Text("StationName", progress, "СТАНЦИЯ", 22f,
            StationPixelVisuals.Cyan, TextAlignmentOptions.MidlineLeft);
        stationName.fontStyle = FontStyles.Bold;
        SetAnchored(stationName.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f),
            new Vector2(20f, -12f), new Vector2(-10f, 28f), new Vector2(0f, 1f));
        TextMeshProUGUI currency = Text("CurrencyText", progress, "GOLD: 0", 17f,
            StationPixelVisuals.Gold, TextAlignmentOptions.MidlineRight);
        SetAnchored(currency.rectTransform, new Vector2(0.72f, 1f), Vector2.one,
            new Vector2(0f, -12f), new Vector2(-20f, 28f), new Vector2(1f, 1f));
        TextMeshProUGUI level = Text("StationLevel", progress, "УРОВЕНЬ СТАНЦИИ 1 / 3", 18f,
            StationPixelVisuals.Text, TextAlignmentOptions.MidlineLeft);
        SetAnchored(level.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f),
            new Vector2(20f, -42f), new Vector2(-10f, 24f), new Vector2(0f, 1f));

        RectTransform bar = Ui("ProgressBar", progress).GetComponent<RectTransform>();
        Image barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = StationPixelVisuals.Window;
        HorizontalLayoutGroup barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 4f;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = true;
        barLayout.childForceExpandHeight = true;
        SetAnchored(bar, new Vector2(0f, 0f), new Vector2(0.64f, 0f),
            new Vector2(20f, 14f), new Vector2(-10f, 36f), new Vector2(0f, 0f));
        for (int i = 0; i < 10; i++)
        {
            RectTransform segment = Ui($"Segment_{i + 1:00}", bar).GetComponent<RectTransform>();
            Image segmentImage = segment.gameObject.AddComponent<Image>();
            segmentImage.color = StationPixelVisuals.Window;
            LayoutElement segmentLayout = segment.gameObject.AddComponent<LayoutElement>();
            segmentLayout.flexibleWidth = 1f;
            RectTransform fill = Ui("Fill", segment).GetComponent<RectTransform>();
            Stretch(fill, 0f, 0f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = StationPixelVisuals.Cyan;
        }
        TextMeshProUGUI progressText = Text("ProgressText", progress, "0 / 500", 17f,
            StationPixelVisuals.Text, TextAlignmentOptions.Center);
        SetAnchored(progressText.rectTransform, new Vector2(0.64f, 0f), new Vector2(0.77f, 0f),
            new Vector2(6f, 14f), new Vector2(-6f, 36f), new Vector2(0.5f, 0f));
        Button invest = Button("UpgradeStationButton", progress, "УЛУЧШИТЬ СТАНЦИЮ");
        SetAnchored((RectTransform)invest.transform, new Vector2(0.78f, 0f), Vector2.right,
            new Vector2(0f, 14f), new Vector2(-20f, 44f), new Vector2(1f, 0f));
        invest.gameObject.AddComponent<HoldInvestmentInput>();

        RectTransform footer = Section("Footer", root.transform);
        Layout(footer, -1f, 88f, 1f, 0f);
        Button back = Button("BackButton", footer, "НАЗАД");
        SetAnchored((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(20f, 0f), new Vector2(220f, 60f), new Vector2(0f, 0.5f));
        Button primary = Button("PrimaryActionButton", footer, "ДЕЙСТВИЕ");
        SetAnchored((RectTransform)primary.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20f, 0f), new Vector2(260f, 60f), new Vector2(1f, 0.5f));

        StationUIShell shell = root.AddComponent<StationUIShell>();
        Assign(shell, "header", header);
        Assign(shell, "mainContent", main);
        Assign(shell, "infoPanel", info);
        Assign(shell, "stationProgressPanel", progress);
        Assign(shell, "footer", footer);
        return root;
    }

    private static BunkerSelectionDetailView BuildDetail(
        RectTransform parent,
        out BunkerProgressionView itemProgression)
    {
        GameObject content = Ui("DetailContent", parent);
        Stretch(content.GetComponent<RectTransform>(), 0f, 0f);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RectTransform identity = Ui("Identity", content.transform).GetComponent<RectTransform>();
        Layout(identity, -1f, 170f, 1f, 0f);
        HorizontalLayoutGroup identityLayout = identity.gameObject.AddComponent<HorizontalLayoutGroup>();
        identityLayout.spacing = 16f;
        identityLayout.childControlWidth = true;
        identityLayout.childControlHeight = true;
        identityLayout.childForceExpandWidth = false;
        identityLayout.childForceExpandHeight = true;
        RectTransform identityText = Ui("IdentityText", identity).GetComponent<RectTransform>();
        Layout(identityText, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup textLayout = identityText.gameObject.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 8f;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;
        TextMeshProUGUI name = Text("Name", identityText, "ITEM", 32f,
            StationPixelVisuals.Text, TextAlignmentOptions.MidlineLeft);
        name.fontStyle = FontStyles.Bold;
        Layout(name.rectTransform, -1f, 52f, 1f, 0f);
        TextMeshProUGUI category = Text("Category", identityText, "TYPE", 18f,
            StationPixelVisuals.Cyan, TextAlignmentOptions.MidlineLeft);
        category.fontStyle = FontStyles.Bold;
        Layout(category.rectTransform, -1f, 34f, 1f, 0f);
        RectTransform portraitRect = Ui("Portrait", identity).GetComponent<RectTransform>();
        Layout(portraitRect, 150f, 170f, 0f, 0f);
        Image portrait = portraitRect.gameObject.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        GameObject featureBlock = DetailBlock("FeatureBlock", content.transform, "ОСОБЕННОСТЬ", out TextMeshProUGUI feature);
        GameObject statsBlock = DetailBlock("StatsBlock", content.transform, "ХАРАКТЕРИСТИКИ", out TextMeshProUGUI stats);
        GameObject descriptionBlock = DetailBlock("DescriptionBlock", content.transform, "ОПИСАНИЕ", out TextMeshProUGUI description);
        Layout(descriptionBlock.GetComponent<RectTransform>(), -1f, -1f, 1f, 1f);
        itemProgression = BuildItemProgress(content.transform);
        GameObject lockBlock = DetailBlock("LockBlock", content.transform, "УСЛОВИЕ ОТКРЫТИЯ", out TextMeshProUGUI lockText);
        Image lockBackground = lockBlock.AddComponent<Image>();
        lockBackground.color = new Color(0.16f, 0.07f, 0.045f, 0.65f);
        lockBackground.raycastTarget = false;

        TextMeshProUGUI empty = Text("EmptyState", parent, "ВЫБЕРИТЕ ЭЛЕМЕНТ", 28f,
            StationPixelVisuals.MutedText, TextAlignmentOptions.Center);
        Stretch(empty.rectTransform, 28f, 28f);
        empty.fontStyle = FontStyles.Bold;
        content.SetActive(false);

        BunkerSelectionDetailView view = parent.gameObject.AddComponent<BunkerSelectionDetailView>();
        Assign(view, "contentRoot", content);
        Assign(view, "emptyText", empty);
        Assign(view, "portrait", portrait);
        Assign(view, "nameText", name);
        Assign(view, "categoryText", category);
        Assign(view, "featureBlock", featureBlock);
        Assign(view, "featureText", feature);
        Assign(view, "statsBlock", statsBlock);
        Assign(view, "statsText", stats);
        Assign(view, "descriptionBlock", descriptionBlock);
        Assign(view, "descriptionText", description);
        Assign(view, "lockBlock", lockBlock);
        Assign(view, "lockText", lockText);
        return view;
    }

    private static BunkerProgressionView BuildItemProgress(Transform parent)
    {
        RectTransform root = Section("ItemProgressionPanel", parent);
        Layout(root, -1f, 230f, 1f, 0f);
        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = Text("Title", root, "ПРОГРЕССИЯ", 18f,
            StationPixelVisuals.Cyan, TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        Layout(title.rectTransform, -1f, 26f, 1f, 0f);
        TextMeshProUGUI level = Text("Level", root, "УРОВЕНЬ 1 / 3", 17f,
            StationPixelVisuals.Text, TextAlignmentOptions.MidlineLeft);
        Layout(level.rectTransform, -1f, 24f, 1f, 0f);
        TextMeshProUGUI bonus = Text("Bonus", root, string.Empty, 15f,
            StationPixelVisuals.Text, TextAlignmentOptions.MidlineLeft);
        Layout(bonus.rectTransform, -1f, 22f, 1f, 0f);
        TextMeshProUGUI context = Text("Context", root, string.Empty, 13f,
            StationPixelVisuals.MutedText, TextAlignmentOptions.MidlineLeft);
        Layout(context.rectTransform, -1f, 20f, 1f, 0f);

        RectTransform bar = Ui("ProgressBar", root).GetComponent<RectTransform>();
        Layout(bar, -1f, 22f, 1f, 0f);
        HorizontalLayoutGroup barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 3f;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = true;
        barLayout.childForceExpandHeight = true;
        var segments = new List<Image>();
        var fills = new List<Image>();
        for (int i = 0; i < 10; i++)
        {
            RectTransform segment = Ui($"Segment_{i + 1:00}", bar).GetComponent<RectTransform>();
            Image segmentImage = segment.gameObject.AddComponent<Image>();
            segmentImage.color = StationPixelVisuals.Window;
            segment.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform fill = Ui("Fill", segment).GetComponent<RectTransform>();
            Stretch(fill, 0f, 0f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = StationPixelVisuals.Cyan;
            segments.Add(segmentImage);
            fills.Add(fillImage);
        }

        TextMeshProUGUI progress = Text("Progress", root, string.Empty, 13f,
            StationPixelVisuals.Text, TextAlignmentOptions.Center);
        Layout(progress.rectTransform, -1f, 18f, 1f, 0f);
        TextMeshProUGUI state = Text("State", root, string.Empty, 14f,
            StationPixelVisuals.Gold, TextAlignmentOptions.Center);
        state.fontStyle = FontStyles.Bold;
        Layout(state.rectTransform, -1f, 20f, 1f, 0f);

        RectTransform actionRow = Ui("ActionRow", root).GetComponent<RectTransform>();
        Layout(actionRow, -1f, 42f, 1f, 0f);
        TextMeshProUGUI currency = Text("Currency", actionRow, "GOLD: 0", 14f,
            StationPixelVisuals.Gold, TextAlignmentOptions.MidlineLeft);
        SetAnchored(currency.rectTransform, Vector2.zero, new Vector2(0.35f, 1f),
            new Vector2(4f, 0f), new Vector2(-4f, 0f), Vector2.zero);
        Button button = Button("UpgradeButton", actionRow, "УЛУЧШИТЬ");
        button.gameObject.AddComponent<HoldInvestmentInput>();
        SetAnchored((RectTransform)button.transform, new Vector2(0.36f, 0f), Vector2.one,
            new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero);

        BunkerProgressionView view = root.gameObject.AddComponent<BunkerProgressionView>();
        Assign(view, "titleText", title);
        Assign(view, "levelText", level);
        AssignArray(view, "progressSegments", segments.Cast<UnityEngine.Object>().ToArray());
        AssignArray(view, "progressFills", fills.Cast<UnityEngine.Object>().ToArray());
        Assign(view, "progressText", progress);
        Assign(view, "currencyText", currency);
        Assign(view, "bonusText", bonus);
        Assign(view, "contextText", context);
        Assign(view, "stateText", state);
        Assign(view, "upgradeButton", button);
        Assign(view, "upgradeButtonText", button.GetComponentInChildren<TextMeshProUGUI>());
        root.gameObject.SetActive(false);
        return view;
    }

    private static GameObject DetailBlock(string name, Transform parent, string heading,
        out TextMeshProUGUI value)
    {
        GameObject root = Ui(name, parent);
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        TextMeshProUGUI label = Text("Heading", root.transform, heading, 16f,
            StationPixelVisuals.Cyan, TextAlignmentOptions.MidlineLeft);
        label.fontStyle = FontStyles.Bold;
        Layout(label.rectTransform, -1f, 24f, 1f, 0f);
        value = Text("Value", root.transform, string.Empty, 17f,
            StationPixelVisuals.Text, TextAlignmentOptions.TopLeft);
        value.textWrappingMode = TextWrappingModes.Normal;
        Layout(value.rectTransform, -1f, 50f, 1f, 0f);
        return root;
    }

    private static BunkerStationProgressView BuildProgress(RectTransform parent)
    {
        TextMeshProUGUI stationName = parent.Find("StationName").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI level = parent.Find("StationLevel").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI progressText = parent.Find("ProgressText").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI currency = parent.Find("CurrencyText").GetComponent<TextMeshProUGUI>();
        RectTransform progressBar = parent.Find("ProgressBar") as RectTransform;
        Button invest = parent.Find("UpgradeStationButton").GetComponent<Button>();
        TextMeshProUGUI investLabel = invest.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI nextUnlock = Text("NextUnlockText", parent, string.Empty, 13f,
            StationPixelVisuals.MutedText, TextAlignmentOptions.MidlineLeft);
        SetAnchored(nextUnlock.rectTransform, new Vector2(0f, 0f), new Vector2(0.64f, 0f),
            new Vector2(20f, 52f), new Vector2(-10f, 18f), new Vector2(0f, 0f));

        var segments = new List<Image>();
        var fills = new List<Image>();
        foreach (Transform segment in progressBar)
        {
            Image segmentImage = segment.GetComponent<Image>();
            Image fill = segment.Find("Fill")?.GetComponent<Image>();
            if (segmentImage != null)
                segments.Add(segmentImage);
            if (fill != null)
                fills.Add(fill);
        }

        BunkerStationProgressView view = parent.gameObject.AddComponent<BunkerStationProgressView>();
        TextMeshProUGUI state = Text("StateText", parent, string.Empty, 14f,
            StationPixelVisuals.Gold, TextAlignmentOptions.Center);
        SetAnchored(state.rectTransform, new Vector2(0.64f, 0f), new Vector2(0.77f, 0f),
            new Vector2(6f, 42f), new Vector2(-6f, 22f), new Vector2(0.5f, 0f));

        Assign(view, "titleText", stationName);
        Assign(view, "levelText", level);
        AssignArray(view, "progressSegments", segments.Cast<UnityEngine.Object>().ToArray());
        AssignArray(view, "progressFills", fills.Cast<UnityEngine.Object>().ToArray());
        Assign(view, "progressText", progressText);
        Assign(view, "currencyText", currency);
        Assign(view, "contextText", nextUnlock);
        Assign(view, "stateText", state);
        Assign(view, "upgradeButton", invest);
        Assign(view, "upgradeButtonText", investLabel);
        return view;
    }

    private static void MigrateMainMenu(GameObject windowPrefab)
    {
        Scene scene = SceneManager.GetSceneByPath(MainMenuPath);
        bool openedForMigration = !scene.IsValid() || !scene.isLoaded;
        if (openedForMigration)
            scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive);
        SelectionPanelController controller = FindSceneComponent<SelectionPanelController>(scene);
        BunkerPanelManager manager = FindSceneComponent<BunkerPanelManager>(scene);
        AudioSettingsPanel settings = FindSceneComponent<AudioSettingsPanel>(scene);
        if (controller == null || manager == null)
            throw new InvalidOperationException("MainMenu selection controller or panel manager is missing.");

        SerializedObject controllerObject = new(controller);
        GameObject selectionRoot = controllerObject.FindProperty("root").objectReferenceValue as GameObject;
        if (selectionRoot == null)
            throw new InvalidOperationException("SelectionPanel root is missing.");

        RemoveLegacySelectionChildren(selectionRoot, controllerObject);

        GameObject instance;
        if (EditorUtility.IsPersistent(windowPrefab))
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab, scene);
        }
        else if (PrefabUtility.IsPartOfPrefabInstance(windowPrefab))
        {
            instance = windowPrefab;
            SceneManager.MoveGameObjectToScene(instance, scene);
        }
        else
        {
            instance = UnityEngine.Object.Instantiate(windowPrefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            UnityEngine.Object.DestroyImmediate(windowPrefab);
        }
        if (instance == null)
            throw new InvalidOperationException("Could not instantiate the shared selection window.");
        instance.name = "BunkerSelectionWindow";
        instance.transform.SetParent(selectionRoot.transform, false);
        Stretch(instance.GetComponent<RectTransform>(), 0f, 0f);
        instance.SetActive(false);
        BunkerSelectionWindow window = instance.GetComponent<BunkerSelectionWindow>();

        Assign(controller, "sharedSelectionWindow", window);

        BunkerSelectionSourceHub hub = manager.GetComponent<BunkerSelectionSourceHub>();
        if (hub == null)
            hub = manager.gameObject.AddComponent<BunkerSelectionSourceHub>();
        ConfigureSources(hub);
        Assign(manager, "selectionSources", hub);
        Assign(manager, "audioSettingsPanel", settings);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForMigration)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static void ConfigureSources(BunkerSelectionSourceHub hub)
    {
        CharacterData[] characters =
        {
            Load<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset"),
            Load<CharacterData>("Assets/_Project/Scriptable Objects/Characters/02_Di-mag.asset"),
            Load<CharacterData>("Assets/_Project/Scriptable Objects/Characters/03_Vika.asset")
        };
        WeaponData[] weapons =
        {
            Load<WeaponData>("Assets/_Project/Scriptable Objects/Weapon/Pistol.asset"),
            Load<WeaponData>("Assets/_Project/Scriptable Objects/Weapon/LaserCannon.asset")
        };
        AnomalyStabilizerData[] anomalies = AssetDatabase
            .FindAssets("t:AnomalyStabilizerData", new[] { "Assets/_Project/Resources/AnomalyStabilizers" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(Load<AnomalyStabilizerData>)
            .Where(value => value != null)
            .ToArray();

        SerializedObject serialized = new(hub);
        SetObjectArray(serialized.FindProperty("characters"), characters);
        SetObjectArray(serialized.FindProperty("weapons"), weapons);
        SetObjectArray(serialized.FindProperty("anomalies"), anomalies);

        (MetaUpgradeType type, string title, string description, string category, int tier)[] upgrades =
        {
            (MetaUpgradeType.Hp, "ЗАПАС ЗДОРОВЬЯ", "Увеличивает базовое здоровье в каждом забеге.", "ВЫЖИВАЕМОСТЬ", 1),
            (MetaUpgradeType.Damage, "УРОН", "Увеличивает урон всего оружия.", "БОЙ", 1),
            (MetaUpgradeType.MoveSpeed, "СКОРОСТЬ ДВИЖЕНИЯ", "Повышает скорость перемещения персонажа.", "МОБИЛЬНОСТЬ", 2),
            (MetaUpgradeType.XpGain, "ПОЛУЧЕНИЕ ОПЫТА", "Увеличивает количество получаемого опыта.", "ПРОГРЕСС", 2),
            (MetaUpgradeType.GoldGain, "ПОЛУЧЕНИЕ ЗОЛОТА", "Увеличивает количество получаемого золота.", "ЭКОНОМИКА", 3),
            (MetaUpgradeType.PickupRadius, "РАДИУС ПОДБОРА", "Увеличивает радиус подбора ресурсов.", "УТИЛИТА", 3)
        };
        SerializedProperty upgradeArray = serialized.FindProperty("upgrades");
        upgradeArray.arraySize = upgrades.Length;
        for (int i = 0; i < upgrades.Length; i++)
        {
            SerializedProperty item = upgradeArray.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("Type").enumValueIndex = (int)upgrades[i].type;
            item.FindPropertyRelative("Title").stringValue = upgrades[i].title;
            item.FindPropertyRelative("Description").stringValue = upgrades[i].description;
            item.FindPropertyRelative("Category").stringValue = upgrades[i].category;
            item.FindPropertyRelative("RequiredStationLevel").intValue = upgrades[i].tier;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveLegacySelectionChildren(
        GameObject selectionRoot,
        SerializedObject controllerObject)
    {
        GameObject shop = controllerObject.FindProperty("shopPanel")?.objectReferenceValue as GameObject;
        GameObject scenes = controllerObject.FindProperty("sceneSelectPanel")?.objectReferenceValue as GameObject;
        for (int i = selectionRoot.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = selectionRoot.transform.GetChild(i).gameObject;
            if (child == shop || child == scenes)
                continue;
            UnityEngine.Object.DestroyImmediate(child);
        }
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path);

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static GameObject Ui(string name, Transform parent = null)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.layer = 5;
        if (parent != null)
            result.transform.SetParent(parent, false);
        return result;
    }

    private static RectTransform Section(string name, Transform parent)
    {
        GameObject root = Ui(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = StationPixelVisuals.Panel;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = StationPixelVisuals.SectionBorder;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        return root.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value,
        float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = Ui(name, parent).AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Button Button(string name, Transform parent, string label)
    {
        GameObject root = Ui(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = StationPixelVisuals.PanelRaised;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = StationPixelVisuals.CyanMuted;
        outline.effectDistance = new Vector2(2f, -2f);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureButton(button);
        TextMeshProUGUI text = Text("Label", root.transform, label, 18f,
            StationPixelVisuals.Text, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f, 4f);
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static void ConfigureButton(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.7f, 0.78f, 0.8f, 1f);
        colors.disabledColor = StationPixelVisuals.Disabled;
        colors.fadeDuration = 0.04f;
        button.colors = colors;
    }

    private static void Stretch(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Layout(RectTransform rect, float width, float height,
        float flexibleWidth, float flexibleHeight)
    {
        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = flexibleHeight;
    }

    private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing property {target.GetType().Name}.{propertyName}");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignArray(UnityEngine.Object target, string propertyName,
        UnityEngine.Object[] values)
    {
        SerializedObject serialized = new(target);
        SetObjectArray(serialized.FindProperty(propertyName), values);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray<T>(SerializedProperty property, T[] values)
        where T : UnityEngine.Object
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif
