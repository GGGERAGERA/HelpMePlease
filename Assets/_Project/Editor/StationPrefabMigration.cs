#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StationPrefabMigration
{
    private const string Folder = "Assets/_Project/prefabs/UI/Stations";
    private const string StationWindowPath = Folder + "/StationWindow.prefab";
    private const string CharacterStationPath = Folder + "/CharacterStationPanel.prefab";
    private const string CharacterCardPath = Folder + "/CharacterCard.prefab";
    private const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";
    [MenuItem("Tools/Subject42/Rebuild Station Prefabs")]
    public static void RebuildFromMenu()
    {
        BuildPrefabsOnly();
    }

    [MenuItem("Tools/Subject42/Migrate MainMenu Character Station")]
    public static void MigrateMainMenuFromMenu()
    {
        GameObject characterStation = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterStationPath);
        if (characterStation == null)
            throw new System.InvalidOperationException("Build CharacterStationPanel.prefab before migrating MainMenu.");
        MigrateMainMenu(characterStation);
    }

    private static void BuildPrefabsOnly()
    {
        EnsureFolder();
        GameObject stationWindow = BuildStationWindowPrefab();
        GameObject characterCard = BuildCharacterCardPrefab();
        BuildCharacterStationPrefab(stationWindow, characterCard);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StationPrefabMigration] Clean Station prefabs created. MainMenu was not modified.");
    }

    private static GameObject BuildStationWindowPrefab()
    {
        GameObject root = CreateUi("StationWindow", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920f, 1080f);
        StyleWindow(root);

        VerticalLayoutGroup windowLayout = root.AddComponent<VerticalLayoutGroup>();
        windowLayout.padding = new RectOffset(48, 48, 28, 28);
        windowLayout.spacing = 12f;
        windowLayout.childAlignment = TextAnchor.UpperCenter;
        windowLayout.childControlWidth = true;
        windowLayout.childControlHeight = true;
        windowLayout.childForceExpandWidth = true;
        windowLayout.childForceExpandHeight = false;

        RectTransform header = CreateSection("Header", root.transform);
        SetLayout(header, -1f, 104f, 1f, 0f);
        TextMeshProUGUI title = CreateText("Title", header, "НАЗВАНИЕ СТАНЦИИ", 48f,
            StationPixelVisuals.Text, TextAlignmentOptions.Center);
        Stretch(title.rectTransform, 24f, 14f);
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 34f;
        title.fontSizeMax = 48f;

        RectTransform body = CreateUi("Body", root.transform).GetComponent<RectTransform>();
        SetLayout(body, -1f, -1f, 1f, 1f);
        HorizontalLayoutGroup bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 20f;
        bodyLayout.childAlignment = TextAnchor.MiddleCenter;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = true;

        RectTransform mainContent = CreateSection("MainContent", body);
        SetLayout(mainContent, -1f, -1f, 60f, 1f);
        RectTransform infoPanel = CreateSection("InfoPanel", body);
        SetLayout(infoPanel, -1f, -1f, 40f, 1f);

        RectTransform progressPanel = CreateSection("StationProgressPanel", root.transform);
        SetLayout(progressPanel, -1f, 136f, 1f, 0f);
        TextMeshProUGUI stationName = CreateText("StationName", progressPanel,
            "СТАНЦИЯ", 22f, StationPixelVisuals.Cyan, TextAlignmentOptions.MidlineLeft);
        SetAnchored(stationName.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f),
            new Vector2(20f, -12f), new Vector2(-10f, 28f), new Vector2(0f, 1f));
        stationName.fontStyle = FontStyles.Bold;

        TextMeshProUGUI currency = CreateText("CurrencyText", progressPanel,
            "GOLD: 0", 17f, StationPixelVisuals.Gold, TextAlignmentOptions.MidlineRight);
        SetAnchored(currency.rectTransform, new Vector2(0.72f, 1f), Vector2.one,
            new Vector2(0f, -12f), new Vector2(-20f, 28f), new Vector2(1f, 1f));

        TextMeshProUGUI stationLevel = CreateText("StationLevel", progressPanel,
            "УРОВЕНЬ СТАНЦИИ 1 / 3", 18f, StationPixelVisuals.Text,
            TextAlignmentOptions.MidlineLeft);
        SetAnchored(stationLevel.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f),
            new Vector2(20f, -42f), new Vector2(-10f, 24f), new Vector2(0f, 1f));

        RectTransform progressBar = CreateUi("ProgressBar", progressPanel).GetComponent<RectTransform>();
        Image progressBackground = progressBar.gameObject.AddComponent<Image>();
        progressBackground.color = StationPixelVisuals.Window;
        progressBackground.raycastTarget = false;
        Outline progressOutline = progressBar.gameObject.AddComponent<Outline>();
        progressOutline.effectColor = StationPixelVisuals.SectionBorder;
        progressOutline.effectDistance = new Vector2(1f, -1f);
        progressOutline.useGraphicAlpha = false;
        HorizontalLayoutGroup progressLayout = progressBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        progressLayout.spacing = 4f;
        progressLayout.childControlWidth = true;
        progressLayout.childControlHeight = true;
        progressLayout.childForceExpandWidth = true;
        progressLayout.childForceExpandHeight = true;
        SetAnchored(progressBar, new Vector2(0f, 0f), new Vector2(0.64f, 0f),
            new Vector2(20f, 14f), new Vector2(-10f, 36f), new Vector2(0f, 0f));

        for (int i = 0; i < 10; i++)
        {
            RectTransform segment = CreateUi($"Segment_{i + 1:00}", progressBar).GetComponent<RectTransform>();
            Image segmentImage = segment.gameObject.AddComponent<Image>();
            segmentImage.color = StationPixelVisuals.Window;
            segmentImage.raycastTarget = false;
            LayoutElement segmentLayout = segment.gameObject.AddComponent<LayoutElement>();
            segmentLayout.flexibleWidth = 1f;

            RectTransform fill = CreateUi("Fill", segment).GetComponent<RectTransform>();
            Stretch(fill, 0f, 0f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = StationPixelVisuals.Cyan;
            fillImage.raycastTarget = false;
            fill.gameObject.SetActive(i < 3);
        }

        TextMeshProUGUI progressText = CreateText("ProgressText", progressPanel,
            "0 / 500", 17f, StationPixelVisuals.Text, TextAlignmentOptions.Center);
        SetAnchored(progressText.rectTransform, new Vector2(0.64f, 0f), new Vector2(0.77f, 0f),
            new Vector2(6f, 14f), new Vector2(-6f, 36f), new Vector2(0.5f, 0f));
        SetLayout(progressText.rectTransform, 100f, -1f, 0f, 0f);

        Button upgrade = CreateButton("UpgradeStationButton", progressPanel,
            "УЛУЧШИТЬ СТАНЦИЮ", true);
        SetAnchored((RectTransform)upgrade.transform, new Vector2(0.78f, 0f), Vector2.right,
            new Vector2(0f, 14f), new Vector2(-20f, 44f), new Vector2(1f, 0f));
        upgrade.gameObject.AddComponent<HoldInvestmentInput>();

        RectTransform footer = CreateSection("Footer", root.transform);
        SetLayout(footer, -1f, 88f, 1f, 0f);
        Button back = CreateButton("BackButton", footer, "НАЗАД", false);
        SetAnchored((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(20f, 0f), new Vector2(220f, 60f), new Vector2(0f, 0.5f));
        Button primary = CreateButton("PrimaryActionButton", footer, "ДЕЙСТВИЕ", false);
        SetAnchored((RectTransform)primary.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20f, 0f), new Vector2(260f, 60f), new Vector2(1f, 0.5f));

        StationUIShell shell = root.AddComponent<StationUIShell>();
        Assign(shell, "header", header);
        Assign(shell, "mainContent", mainContent);
        Assign(shell, "infoPanel", infoPanel);
        Assign(shell, "stationProgressPanel", progressPanel);
        Assign(shell, "footer", footer);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StationWindowPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildCharacterCardPrefab()
    {
        GameObject root = CreateUi("CharacterCard", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(300f, 400f);
        Image background = root.AddComponent<Image>();
        background.color = StationPixelVisuals.PanelRaised;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        ConfigureButtonColors(button);

        RectTransform portraitRect = CreateUi("CharacterSprite", root.transform).GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.18f);
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(16f, 12f);
        portraitRect.offsetMax = new Vector2(-16f, -16f);
        Image characterImage = portraitRect.gameObject.AddComponent<Image>();
        characterImage.preserveAspect = true;
        characterImage.raycastTarget = false;

        RectTransform nameBand = CreateUi("NameBand", root.transform).GetComponent<RectTransform>();
        nameBand.anchorMin = Vector2.zero;
        nameBand.anchorMax = new Vector2(1f, 0.18f);
        nameBand.offsetMin = new Vector2(8f, 8f);
        nameBand.offsetMax = new Vector2(-8f, -4f);
        Image nameBackground = nameBand.gameObject.AddComponent<Image>();
        nameBackground.color = StationPixelVisuals.Window;
        nameBackground.raycastTarget = false;
        TextMeshProUGUI name = CreateText("Name", nameBand, "CHARACTER", 26f,
            StationPixelVisuals.Text, TextAlignmentOptions.Center);
        Stretch(name.rectTransform, 8f, 4f);
        name.fontStyle = FontStyles.Bold;

        CharacterCardView view = root.AddComponent<CharacterCardView>();
        Assign(view, "backgroundImage", background);
        Assign(view, "characterImage", characterImage);
        Assign(view, "nameText", name);
        Assign(view, "button", button);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterCardPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildCharacterStationPrefab(GameObject stationWindow, GameObject cardPrefab)
    {
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(stationWindow);
        root.name = "CharacterStationPanel";
        StationUIShell shell = root.GetComponent<StationUIShell>();

        TextMeshProUGUI title = shell.Header.Find("Title").GetComponent<TextMeshProUGUI>();
        title.text = "ВЫБЕРИТЕ ПЕРСОНАЖА";
        shell.MainContent.gameObject.AddComponent<VerticalLayoutGroup>();
        VerticalLayoutGroup mainLayout = shell.MainContent.GetComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(28, 28, 20, 24);
        mainLayout.spacing = 10f;
        mainLayout.childControlWidth = true;
        mainLayout.childControlHeight = true;
        mainLayout.childForceExpandWidth = true;
        mainLayout.childForceExpandHeight = false;

        TextMeshProUGUI rosterTitle = CreateText("RosterTitle", shell.MainContent,
            "ДОСТУПНЫЕ СУБЪЕКТЫ", 18f, StationPixelVisuals.Cyan,
            TextAlignmentOptions.MidlineLeft);
        rosterTitle.fontStyle = FontStyles.Bold;
        SetLayout(rosterTitle.rectTransform, -1f, 30f, 1f, 0f);

        RectTransform roster = CreateUi("CharacterRoster", shell.MainContent).GetComponent<RectTransform>();
        SetLayout(roster, -1f, -1f, 1f, 1f);
        ScrollRect scroll = roster.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CreateUi("Viewport", roster).GetComponent<RectTransform>();
        Stretch(viewport, 0f, 0f);
        Image viewportGraphic = viewport.gameObject.AddComponent<Image>();
        viewportGraphic.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform gridRect = CreateUi("CharacterCardsGrid", viewport).GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 1f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = new Vector2(0f, 400f);
        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(300f, 400f);
        grid.spacing = new Vector2(24f, 20f);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        ContentSizeFitter fitter = gridRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = gridRect;

        string[] dataPaths =
        {
            "Assets/_Project/Scriptable Objects/Characters/01_Gera.asset",
            "Assets/_Project/Scriptable Objects/Characters/02_Di-mag.asset",
            "Assets/_Project/Scriptable Objects/Characters/03_Vika.asset"
        };
        List<CharacterCardView> cards = new();
        foreach (string dataPath in dataPaths)
        {
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
            GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, gridRect);
            card.name = data != null ? data.characterName : "CharacterCard";
            CharacterCardView view = card.GetComponent<CharacterCardView>();
            Assign(view, "character", data);
            Sprite characterSprite = FindCharacterSprite(data != null ? data.characterPrefab : null);
            Assign(view, "characterSprite", characterSprite);
            Image characterImage = card.transform.Find("CharacterSprite").GetComponent<Image>();
            TextMeshProUGUI name = card.transform.Find("NameBand/Name").GetComponent<TextMeshProUGUI>();
            if (data != null)
            {
                characterImage.sprite = characterSprite;
                characterImage.enabled = characterSprite != null;
                name.text = data.characterName;
            }
            cards.Add(view);
        }

        RectTransform characterInfo = CreateUi("CharacterInfo", shell.InfoPanel).GetComponent<RectTransform>();
        Stretch(characterInfo, 0f, 0f);
        VerticalLayoutGroup infoLayout = characterInfo.gameObject.AddComponent<VerticalLayoutGroup>();
        infoLayout.padding = new RectOffset(28, 28, 28, 28);
        infoLayout.spacing = 12f;
        infoLayout.childAlignment = TextAnchor.UpperLeft;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        RectTransform identityRow = CreateUi("IdentityRow", characterInfo).GetComponent<RectTransform>();
        SetLayout(identityRow, -1f, 214f, 1f, 0f);
        HorizontalLayoutGroup identityLayout = identityRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        identityLayout.spacing = 20f;
        identityLayout.childAlignment = TextAnchor.UpperLeft;
        identityLayout.childControlWidth = true;
        identityLayout.childControlHeight = true;
        identityLayout.childForceExpandWidth = false;
        identityLayout.childForceExpandHeight = true;

        RectTransform identityText = CreateUi("IdentityText", identityRow).GetComponent<RectTransform>();
        SetLayout(identityText, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup identityTextLayout = identityText.gameObject.AddComponent<VerticalLayoutGroup>();
        identityTextLayout.spacing = 10f;
        identityTextLayout.childAlignment = TextAnchor.UpperLeft;
        identityTextLayout.childControlWidth = true;
        identityTextLayout.childControlHeight = true;
        identityTextLayout.childForceExpandWidth = true;
        identityTextLayout.childForceExpandHeight = false;

        TextMeshProUGUI characterName = CreateText("CharacterName", identityText,
            "GERA", 34f, StationPixelVisuals.Text, TextAlignmentOptions.MidlineLeft);
        characterName.fontStyle = FontStyles.Bold;
        SetLayout(characterName.rectTransform, -1f, 48f, 1f, 0f);
        TextMeshProUGUI combatType = CreateText("CombatType", identityText,
            "АВТОСТРЕЛЬБА", 18f, StationPixelVisuals.Cyan, TextAlignmentOptions.MidlineLeft);
        combatType.fontStyle = FontStyles.Bold;
        SetLayout(combatType.rectTransform, -1f, 30f, 1f, 0f);
        CreateDivider("IdentityDivider", identityText);
        TextMeshProUGUI feature = CreateText("FeatureText", identityText,
            "<color=#14D1DB><b>ОСОБЕННОСТЬ</b></color>\nОписание особенности персонажа.",
            18f, StationPixelVisuals.Text, TextAlignmentOptions.TopLeft);
        SetLayout(feature.rectTransform, -1f, 90f, 1f, 0f);

        RectTransform portraitRect = CreateUi("Portrait", identityRow).GetComponent<RectTransform>();
        SetLayout(portraitRect, 180f, 214f, 0f, 0f);
        Image portrait = portraitRect.gameObject.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.enabled = false;

        CreateDivider("StatsDivider", characterInfo);
        TextMeshProUGUI stats = CreateText("Stats", characterInfo,
            "<color=#14D1DB><b>ХАРАКТЕРИСТИКИ</b></color>\nЗДОРОВЬЕ   100   ■■■■□□□□\nСКОРОСТЬ     3   ■■■□□□□□",
            18f, StationPixelVisuals.Text, TextAlignmentOptions.TopLeft);
        SetLayout(stats.rectTransform, -1f, 108f, 1f, 0f);
        CreateDivider("DescriptionDivider", characterInfo);
        TextMeshProUGUI description = CreateText("Description", characterInfo,
            "<color=#14D1DB><b>ОПИСАНИЕ</b></color>\nОписание персонажа.",
            18f, StationPixelVisuals.Text, TextAlignmentOptions.TopLeft);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;
        SetLayout(description.rectTransform, -1f, -1f, 1f, 1f);
        characterInfo.gameObject.SetActive(false);

        TextMeshProUGUI emptyState = CreateText("EmptyState", shell.InfoPanel,
            "ВЫБЕРИТЕ ПЕРСОНАЖА", 28f, StationPixelVisuals.MutedText,
            TextAlignmentOptions.Center);
        Stretch(emptyState.rectTransform, 28f, 28f);
        emptyState.fontStyle = FontStyles.Bold;

        TextMeshProUGUI stationName = shell.StationProgressPanel.Find("StationName")
            .GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI stationLevel = shell.StationProgressPanel.Find("StationLevel")
            .GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI progressText = shell.StationProgressPanel.Find("ProgressText")
            .GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI currencyText = shell.StationProgressPanel.Find("CurrencyText")
            .GetComponent<TextMeshProUGUI>();
        RectTransform progressBar = shell.StationProgressPanel.Find("ProgressBar") as RectTransform;
        Button upgrade = shell.StationProgressPanel.Find("UpgradeStationButton").GetComponent<Button>();
        TextMeshProUGUI upgradeLabel = upgrade.GetComponentInChildren<TextMeshProUGUI>();
        stationName.text = "СТАНЦИЯ ПЕРСОНАЖЕЙ";

        Button back = shell.Footer.Find("BackButton").GetComponent<Button>();
        Button primary = shell.Footer.Find("PrimaryActionButton").GetComponent<Button>();
        primary.GetComponentInChildren<TextMeshProUGUI>().text = "ВЫБРАТЬ";

        CharacterStationEmbeddedView stationView = root.AddComponent<CharacterStationEmbeddedView>();
        Assign(stationView, "panelRect", shell.StationProgressPanel);
        Assign(stationView, "titleText", stationName);
        Assign(stationView, "levelText", stationLevel);
        Assign(stationView, "progressRoot", progressBar.gameObject);
        AssignArray(stationView, "progressSegments", CollectProgressImages(progressBar, false));
        AssignArray(stationView, "progressFills", CollectProgressImages(progressBar, true));
        Assign(stationView, "goldProgressText", progressText);
        Assign(stationView, "availableGoldText", currencyText);
        Assign(stationView, "upgradeButton", upgrade);
        Assign(stationView, "upgradeButtonText", upgradeLabel);

        CharacterSelectionUI selection = root.AddComponent<CharacterSelectionUI>();
        AssignArray(selection, "cards", cards.ToArray());
        Assign(selection, "characterInfoRoot", characterInfo.gameObject);
        Assign(selection, "emptyStateText", emptyState);
        Assign(selection, "portraitImage", portrait);
        Assign(selection, "characterNameText", characterName);
        Assign(selection, "combatTypeText", combatType);
        Assign(selection, "featureText", feature);
        Assign(selection, "statsText", stats);
        Assign(selection, "descriptionText", description);
        Assign(selection, "selectButton", primary);
        Assign(selection, "backButton", back);
        Assign(selection, "stationView", stationView);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterStationPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static bool MigrateMainMenu(GameObject characterStationPrefab)
    {
        Scene current = SceneManager.GetActiveScene();
        string previousPath = current.path;
        bool currentIsMainMenu = previousPath == MainMenuPath;
        bool openedAdditively = !currentIsMainMenu && current.isDirty;
        Scene scene = currentIsMainMenu
            ? current
            : EditorSceneManager.OpenScene(
                MainMenuPath,
                openedAdditively ? OpenSceneMode.Additive : OpenSceneMode.Single);
        GameObject oldRoot = FindInScene(scene, "PlayerSelectPanel");
        if (oldRoot == null)
            oldRoot = FindInScene(scene, "PlayerSelectPanel Variant");
        if (oldRoot == null)
            return false;

        string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldRoot);
        if (sourcePath == CharacterStationPath)
            return true;

        Transform parent = oldRoot.transform.parent;
        int siblingIndex = oldRoot.transform.GetSiblingIndex();
        bool active = oldRoot.activeSelf;
        RectTransform oldRect = oldRoot.transform as RectTransform;
        BunkerPanelManager panelManager = null;
        CharacterSelectionUI oldSelection = oldRoot.GetComponent<CharacterSelectionUI>();
        if (oldSelection != null)
        {
            SerializedProperty managerProperty = new SerializedObject(oldSelection).FindProperty("panelManager");
            panelManager = managerProperty?.objectReferenceValue as BunkerPanelManager;
        }

        GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(characterStationPrefab, scene);
        replacement.name = "PlayerSelectPanel";
        replacement.transform.SetParent(parent, false);
        replacement.transform.SetSiblingIndex(siblingIndex);
        CopyRect(oldRect, replacement.transform as RectTransform);
        replacement.SetActive(active);

        CharacterSelectionUI newSelection = replacement.GetComponent<CharacterSelectionUI>();
        Assign(newSelection, "panelManager", panelManager);
        ReplaceSceneReferences(scene, oldRoot, replacement);
        Object.DestroyImmediate(oldRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (openedAdditively)
            EditorSceneManager.CloseScene(scene, true);
        else if (!string.IsNullOrEmpty(previousPath) && previousPath != MainMenuPath)
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
        return true;
    }

    private static void ReplaceSceneReferences(Scene scene, GameObject oldRoot, GameObject replacement)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                    continue;
                SerializedObject serialized = new(component);
                SerializedProperty iterator = serialized.GetIterator();
                bool changed = false;
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                        iterator.objectReferenceValue == oldRoot)
                    {
                        iterator.objectReferenceValue = replacement;
                        changed = true;
                    }
                }
                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;
        foreach (Transform child in root)
        {
            Transform result = FindRecursive(child, objectName);
            if (result != null)
                return result;
        }
        return null;
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private static Image[] CollectProgressImages(RectTransform progressBar, bool fills)
    {
        List<Image> result = new();
        for (int i = 0; i < progressBar.childCount; i++)
        {
            Transform segment = progressBar.GetChild(i);
            Image image = fills
                ? segment.Find("Fill")?.GetComponent<Image>()
                : segment.GetComponent<Image>();
            if (image != null)
                result.Add(image);
        }
        return result.ToArray();
    }

    private static Sprite FindCharacterSprite(GameObject characterPrefab)
    {
        if (characterPrefab == null)
            return null;

        SpriteRenderer fallback = null;
        foreach (SpriteRenderer renderer in characterPrefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null)
                continue;
            fallback ??= renderer;
            if (renderer.gameObject.activeSelf)
                return renderer.sprite;
        }

        return fallback != null ? fallback.sprite : null;
    }

    private static void EnsureFolder()
    {
        const string parent = "Assets/_Project/prefabs/UI";
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder(parent, "Stations");
    }

    private static GameObject CreateUi(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = 5;
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform CreateSection(string name, Transform parent)
    {
        GameObject go = CreateUi(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = StationPixelVisuals.Panel;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = StationPixelVisuals.SectionBorder;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        return go.GetComponent<RectTransform>();
    }

    private static void StyleWindow(GameObject root)
    {
        Image image = root.AddComponent<Image>();
        image.color = StationPixelVisuals.Window;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = StationPixelVisuals.SectionBorder;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value,
        float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUi(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void CreateDivider(string name, Transform parent)
    {
        GameObject divider = CreateUi(name, parent);
        Image image = divider.AddComponent<Image>();
        image.color = StationPixelVisuals.SectionBorder;
        image.raycastTarget = false;
        SetLayout(divider.GetComponent<RectTransform>(), -1f, 1f, 1f, 0f);
    }

    private static Button CreateButton(string name, Transform parent, string label, bool gold)
    {
        GameObject go = CreateUi(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = gold ? new Color(0.13f, 0.105f, 0.035f, 1f) : StationPixelVisuals.PanelRaised;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = gold ? StationPixelVisuals.Gold : StationPixelVisuals.CyanMuted;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureButtonColors(button);
        TextMeshProUGUI text = CreateText("Label", go.transform, label, 18f,
            gold ? StationPixelVisuals.Gold : StationPixelVisuals.Text,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f, 4f);
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static void ConfigureButtonColors(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.7f, 0.78f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = StationPixelVisuals.Disabled;
        colors.fadeDuration = 0.04f;
        button.colors = colors;
    }

    private static void Stretch(RectTransform rect, float horizontalInset, float verticalInset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontalInset, verticalInset);
        rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
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

    private static void SetLayout(RectTransform rect, float preferredWidth, float preferredHeight,
        float flexibleWidth, float flexibleHeight)
    {
        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = flexibleHeight;
    }

    private static void Assign(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignArray<T>(Object target, string propertyName, T[] values) where T : Object
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
