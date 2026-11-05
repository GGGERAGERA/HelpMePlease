using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Subject42UITheme;

internal sealed class SurfaceRoutePresentation
{
    private const float LocationSpacing = 310f;
    private const int PreviewColumns = 3;
    private enum Intel { Hidden, Preview, Surveyed }
    private readonly RectTransform root, graph, landmarks;
    private readonly SurfaceMapFog fog;
    private readonly SurfaceMapNavigation navigation;
    private readonly TMP_Text fontSource, header, goldLabel, alternateLabel;
    private readonly RunPressureMeter pressureMeter;
    private readonly RectTransform chrome;
    private readonly SurfaceRouteCard card;
    private readonly Button back, alternateButton;
    private readonly SurfaceRouteRoads roads;
    private readonly Sprite bunkerSprite;
    private readonly SurfaceMapView.LocationIcon[] siteIcons;
    private readonly Dictionary<RouteNode, (Graphic selection, int road)> pins = new();
    private readonly Dictionary<RouteNode, List<int>> branchRoads = new();
    private readonly List<Vector2> clearings = new();
    private readonly List<(Vector2 from, Vector2 to)> surveyedRoads = new();
    private readonly List<Vector2> outlooks = new();
    private TMP_Text bunkerLabel, bossLabel;
    private Image characterMarker;

    private IReadOnlyList<RouteNode> available;
    private Func<RouteNode, bool> confirm;
    private string alternateKey;
    private int runGold;
    private int displayedLevel;
    private RouteNode selected, inspected;

    internal SurfaceRoutePresentation(RectTransform root, RectTransform graph, RectTransform terrain,
        SurfaceMapNavigation navigation, SurfaceMapFog fog, TMP_Text fontSource, TMP_FontAsset cardFont,
        Material cardTextMaterial, Sprite bunkerSprite, SurfaceMapView.LocationIcon[] siteIcons)
    {
        this.root = root;
        this.graph = graph;
        this.navigation = navigation;
        this.fog = fog;
        this.fontSource = fontSource;
        this.bunkerSprite = bunkerSprite;
        this.siteIcons = siteIcons;
        navigation.ScrollAlongRoute = true;

        float right = Position(RunRoute.TotalSectors + 1, 1).x + 1100f;
        terrain.anchoredPosition = new Vector2((right - 1000f) / 2f, 0f);
        terrain.sizeDelta = new Vector2(right + 1000f, 2100f);
        var ground = terrain.GetComponent<Image>();
        ground.type = Image.Type.Tiled;
        ground.color = new Color(.91f, .95f, .94f);

        landmarks = CreateRect("Route landmarks", graph, Vector2.zero, Vector2.one);
        var roadRoot = CreateRect("Roads", graph, Vector2.zero, new Vector2(right * 2f, 1040f));
        roadRoot.SetSiblingIndex(landmarks.GetSiblingIndex());
        roads = roadRoot.gameObject.AddComponent<SurfaceRouteRoads>();
        roads.raycastTarget = false;

        chrome = CreateRect("Route controls", root, Vector2.zero, new Vector2(1600f, 900f));
        var heading = Frame("Route stage HUD", chrome, new Vector2(0f, 392f), new Vector2(382f, 58f), Border, Surface);
        header = Label("Route stage name", heading.rectTransform, Vector2.zero, new Vector2(344f, 42f), 28f, PrimaryText, true);
        header.textWrappingMode = TextWrappingModes.NoWrap;
        header.enableAutoSizing = true;
        header.fontSizeMin = 20f;
        header.fontSizeMax = 30f;
        var wallet = Frame("Route gold HUD", chrome, new Vector2(598f, 396f), new Vector2(196f, 42f), Border, Surface);
        CreateRect("Coin", wallet.rectTransform, new Vector2(-73f, 0f), new Vector2(21f, 21f))
            .gameObject.AddComponent<SurfaceMapGlyph>().Show(SurfaceMapMeaning.Reward);
        goldLabel = Label("Route gold", wallet.rectTransform, new Vector2(17f, 0f), new Vector2(140f, 30f), 16f, SecondaryText);
        goldLabel.textWrappingMode = TextWrappingModes.NoWrap;
        goldLabel.enableAutoSizing = true;
        goldLabel.fontSizeMin = 12f;
        goldLabel.fontSizeMax = 16f;
        var pressure = Frame("Route pressure HUD", chrome, new Vector2(395f, 396f), new Vector2(198f, 42f), Border, Surface);
        pressureMeter = RunPressureMeter.Create(pressure.rectTransform, fontSource.font,
            new Vector2(176f, 30f), "Route pressure");
        back = Control("Route close", chrome, new Vector2(742f, 396f), new Vector2(46f, 42f), "×", out _);
        alternateButton = Control("Route variant", chrome, new Vector2(540f, 338f), new Vector2(312f, 42f), "", out alternateLabel);

        card = new SurfaceRouteCard(chrome, cardFont != null ? cardFont : fontSource.font, cardTextMaterial);
        card.Launch.onClick.AddListener(() =>
        {
            if (selected != null && confirm?.Invoke(selected) == true)
                AudioService.Instance?.Play(AudioCueId.UIConfirm);
        });
    }

    internal void Show(RunRouteGraph route, IReadOnlyList<RouteNode> path, IReadOnlyList<RouteNode> available,
        Func<RouteNode, bool> confirm, Action close, Action alternate, string alternateKey,
        CharacterData character)
    {
        this.available = available;
        this.confirm = confirm;
        this.alternateKey = alternateKey;
        displayedLevel = available[0].SectorNumber;
        selected = inspected = null;
        card.Show(null, false);
        Bind(back, close);
        Bind(alternateButton, alternate);
        foreach (Transform old in landmarks)
        {
            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);
        }
        pins.Clear();
        branchRoads.Clear();
        clearings.Clear();
        surveyedRoads.Clear();
        outlooks.Clear();
        roads.Clear();

        Vector2 bunker = Position(0, 1);
        var start = Artwork("Bunker", bunker, bunkerSprite, 174f, Color.white);
        bunkerLabel = Label("Bunker label", start, new Vector2(0f, -61f), new Vector2(190f, 32f), 23f, PrimaryText, true);
        clearings.Add(bunker);
        RouteNode last = null;
        var travelled = new HashSet<(RouteNode from, RouteNode to)>();
        var visited = new HashSet<RouteNode>(path);
        var reachable = new HashSet<RouteNode>(available);
        var incoming = new Dictionary<RouteNode, int>();
        int column = available[0].SectorNumber;
        foreach (var node in path)
        {
            travelled.Add((last, node));
            last = node;
        }

        foreach (var entry in route.Entries) DrawRoad(null, entry);
        for (int sector = 1; sector <= RunRoute.TotalSectors; sector++)
            foreach (var node in route.NodesAt(sector))
                foreach (var next in node.Next) DrawRoad(node, next);

        for (int sector = 1; sector <= RunRoute.TotalSectors; sector++)
        {
            Intel intel = RevealAt(sector, column);
            foreach (var node in route.NodesAt(sector))
            {
                bool cleared = visited.Contains(node);
                bool enter = reachable.Contains(node);
                if (cleared || enter) clearings.Add(Position(node));
                else if (intel == Intel.Preview || sector == column) outlooks.Add(Position(node));
                AddPin(node, intel, cleared, enter, incoming.TryGetValue(node, out int road) ? road : -1);
            }
        }

        int bossColumn = RunRoute.TotalSectors + 1;
        Vector2 boss = Position(bossColumn, 1);
        bool bossRevealed = RevealAt(bossColumn, column) != Intel.Hidden;
        foreach (var node in route.NodesAt(RunRoute.FinalSector))
        {
            int road = roads.Add(Position(node), boss,
                bossRevealed ? SurfaceRouteRoads.State.Ahead : SurfaceRouteRoads.State.Uncharted);
            RecordBranch(node, road);
        }
        if (bossRevealed) outlooks.Add(boss);
        var bossSite = Artwork("Final boss", boss, FindArt(SurfaceLocationFamily.Technogenic), 168f,
            bossRevealed ? SecondaryText : Border);
        var bossPlate = Frame("Boss marker", bossSite, Vector2.zero, new Vector2(74f, 68f),
            bossRevealed ? Coral : Border, Background, 5f);
        var bossIcon = CreateRect("Boss icon", bossPlate.rectTransform, Vector2.zero, new Vector2(42f, 42f))
            .gameObject.AddComponent<SurfaceMapGlyph>();
        bossIcon.Show(SurfaceMapMeaning.Boss);
        if (!bossRevealed) bossIcon.color = SecondaryText;
        bossLabel = Label("Boss label", bossSite, new Vector2(0f, -63f), new Vector2(310f, 36f), 20f,
            bossRevealed ? PrimaryText : SecondaryText, true);

        Vector2 current = last == null ? bunker : Position(last);
        Vector2 markerOffset = path.Count == 0 ? new Vector2(-90f, -37f) : new Vector2(-3f, -72f);
        characterMarker = Artwork("Route character", current + markerOffset, character?.GameplayIcon,
            66f, Color.white).GetComponent<Image>();

        Canvas.ForceUpdateCanvases();
        var size = root.rect.size;
        chrome.localScale = Vector3.one * Mathf.Min(size.x / 1600f, size.y / 900f);
        float zoom = Mathf.Clamp(size.x / 2080f, SurfaceMapNavigation.MinZoom, .90f);
        float focus = Position(available[0]).x + LocationSpacing * .5f;
        navigation.SetBounds(Rect.MinMaxRect(-570f, -510f, boss.x + 470f, 510f),
            new Vector2(focus, 0f));
        graph.localScale = Vector3.one * zoom;
        graph.anchoredPosition = new Vector2(-focus, -10f) * zoom;
        navigation.Pan(Vector2.zero);
        fog.Reveal(clearings, surveyedRoads, outlooks);
        roads.SetVerticesDirty();
        RefreshText();

        void DrawRoad(RouteNode from, RouteNode to)
        {
            bool onPath = travelled.Contains((from, to));
            bool enter = from == last && reachable.Contains(to);
            var state = onPath ? SurfaceRouteRoads.State.Travelled : enter ? SurfaceRouteRoads.State.Open :
                RevealAt(to.SectorNumber, column) == Intel.Hidden ? SurfaceRouteRoads.State.Uncharted :
                to.SectorNumber > column ? SurfaceRouteRoads.State.Ahead : SurfaceRouteRoads.State.Missed;
            int road = roads.Add(from == null ? bunker : Position(from), Position(to), state,
                onPath || enter ? surveyedRoads : null);
            if (enter) incoming.Add(to, road);
            RecordBranch(from, road);
        }

        void RecordBranch(RouteNode from, int road)
        {
            if (from == null || !reachable.Contains(from)) return;
            if (!branchRoads.TryGetValue(from, out var branches)) branchRoads.Add(from, branches = new List<int>());
            branches.Add(road);
        }
    }

    private static Intel RevealAt(int sector, int column) => sector <= column ? Intel.Surveyed :
        sector <= column + PreviewColumns ? Intel.Preview : Intel.Hidden;

    internal void SetCharacter(CharacterData character)
    {
        if (characterMarker == null) return;
        characterMarker.sprite = character?.GameplayIcon;
        characterMarker.enabled = characterMarker.sprite != null;
    }

    internal void SetRunStatus(int gold, float pressure)
    {
        runGold = gold;
        pressureMeter.SetValue(pressure);
        RefreshRunStatus();
    }

    private void RefreshRunStatus()
    {
        goldLabel.text = F("route.gold", Tint(runGold, PrimaryText));
    }

    private static void Bind(Button button, Action action)
    {
        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(action != null);
        if (action != null) button.onClick.AddListener(() => action());
    }

    private void AddPin(RouteNode node, Intel intel, bool cleared, bool canSelect, int road)
    {
        var pos = Position(node);
        Color accent = cleared ? Green : canSelect ? Cyan : Border;
        var site = Artwork("Site", pos, FindArt(SurfaceLocationFamily.Ruins), 149f,
            canSelect ? Color.white : intel == Intel.Hidden ? Border : SecondaryText);
        SurfaceMapPlate selection = null;
        if (canSelect)
        {
            selection = Frame("Route selection outline", site, Vector2.zero, new Vector2(88f, 82f), Amber, Background, 5f);
            selection.gameObject.SetActive(false);
        }
        Vector2 size = canSelect ? new Vector2(68f, 62f) : new Vector2(57f, 53f);
        var badge = Frame($"Route location {node.SectorNumber}-{node.Row + 1}", site,
            Vector2.zero, size, accent, Background, 4f);
        var symbol = CreateRect("Route status", badge.rectTransform, Vector2.zero, Vector2.one * 38f)
            .gameObject.AddComponent<SurfaceMapGlyph>();
        symbol.Show(cleared ? SurfaceMapMeaning.Cleared :
            intel == Intel.Hidden ? SurfaceMapMeaning.Expedition : SurfaceMapMeaning.Combat);
        if (!cleared && !canSelect) symbol.color = SecondaryText;
        if (intel == Intel.Hidden) return;
        badge.raycastTarget = true;
        if (!canSelect)
        {
            badge.gameObject.AddComponent<SurfaceMapNodeFeedback>().HoverChanged = over =>
            {
                if (over) inspected = node;
                else if (inspected == node) inspected = null;
                RefreshText();
            };
            return;
        }
        pins.Add(node, (selection, road));
        var button = badge.gameObject.AddComponent<Button>();
        button.targetGraphic = badge;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => Select(node));
    }

    private void Select(RouteNode node)
    {
        if (selected != null)
            pins[selected].selection.gameObject.SetActive(false);
        selected = node;
        inspected = null;
        var pin = pins[node];
        pin.selection.gameObject.SetActive(true);
        branchRoads.TryGetValue(node, out var branches);
        roads.Select(pin.road, branches);
        RefreshText();
    }

    internal void RefreshText()
    {
        if (displayedLevel == 0) return;
        header.text = F("route.stage.name", displayedLevel, RunRoute.LevelCount);
        RefreshRunStatus();
        bunkerLabel.text = L("map.home");
        if (bossLabel != null) bossLabel.text = L("route.node.boss");
        if (alternateKey != null) alternateLabel.text = L(alternateKey);

        card.Show(inspected ?? selected, inspected == null);
    }

    private static string Tint(object value, Color color) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>";

    private static string L(string key) => SurfaceMapBriefing.Text(key);
    private static string F(string key, params object[] args) => SurfaceMapBriefing.Format(key, args);

    private static Vector2 Position(RouteNode node) => Position(node.SectorNumber, node.Row, node.RowCount);
    private static Vector2 Position(int level, int row, int rows = 3)
    {
        float x = level * LocationSpacing;
        float terrainOffset = level == 0 ? 0f : Mathf.Round(Mathf.Sin(level * 1.25f) * 15f) * 4f;
        float spacing = Mathf.Min(151f, 478f / Mathf.Max(1, rows - 1));
        return new Vector2(x, ((rows - 1) * .5f - row) * spacing + terrainOffset);
    }

    private Sprite FindArt(SurfaceLocationFamily family) => Array.Find(siteIcons, entry => entry.Family == family)?.Sprite;

    private RectTransform Artwork(string name, Vector2 point, Sprite sprite, float size, Color tint)
    {
        var rect = CreateRect(name, landmarks, point, new Vector2(size, size));
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = tint;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return rect;
    }

    private static readonly Vector2 Middle = new(.5f, .5f);
    private static RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Middle;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static SurfaceMapPlate Plate(string name, RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        var plate = CreateRect(name, parent, pos, size).gameObject.AddComponent<SurfaceMapPlate>();
        plate.color = color;
        plate.raycastTarget = false;
        return plate;
    }

    private static SurfaceMapPlate Frame(string name, RectTransform parent, Vector2 pos, Vector2 size,
        Color border, Color fill, float width = 2f)
    {
        var frame = Plate(name, parent, pos, size, border);
        var inset = Plate("Inset", frame.rectTransform, Vector2.zero, size - Vector2.one * (width * 2f), fill);
        inset.Cut = Mathf.Max(0f, frame.Cut - width);
        return frame;
    }

    private TMP_Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size, float pointSize, Color color,
        bool center = false)
    {
        var label = CreateRect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = fontSource.font;
        label.fontSharedMaterial = fontSource.fontSharedMaterial;
        label.color = color;
        label.fontSize = pointSize;
        label.raycastTarget = false;
        label.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        return label;
    }

    private Button Control(string name, RectTransform parent, Vector2 pos, Vector2 size, string title, out TMP_Text label)
    {
        var plate = Frame(name, parent, pos, size, Border, Surface);
        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        plate.raycastTarget = true;
        label = Label("Label", plate.rectTransform, Vector2.zero, size - new Vector2(12f, 4f), 22f, PrimaryText, true);
        label.text = title;
        return button;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal bool ConfirmFirst()
    {
        if (available == null || available.Count == 0) return false;
        Select(available[0]);
        return confirm?.Invoke(selected) == true;
    }
#endif
}
