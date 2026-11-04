using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class SurfaceRoutePresentation
{
    private const float LocationSpacing = 310f;
    private static readonly Color Available = new(.34f, .95f, .83f);
    private static readonly Color Selected = new(1f, .72f, .28f);
    private static readonly Color SelectedText = new(1f, .95f, .69f);
    private static readonly Color Visited = new(.36f, .91f, .52f);
    private static readonly Color Muted = new(.48f, .54f, .54f);
    private static readonly Color Preview = new(.72f, .83f, .80f);
    private static readonly Color Text = new(.87f, .90f, .83f);
    private static readonly Color Ink = new(.056f, .081f, .093f, .98f);

    private readonly RectTransform root, graph, landmarks;
    private readonly SurfaceMapFog fog;
    private readonly SurfaceMapNavigation navigation;
    private readonly TMP_Text fontSource, header, goldLabel, facts, launchLabel, alternateLabel;
    private readonly RectTransform chrome;
    private readonly SurfaceMapPlate card;
    private readonly Button launch, back, alternateButton;
    private readonly SurfaceRouteRoads roads;
    private readonly Sprite bunkerSprite;
    private readonly SurfaceMapView.LocationIcon[] siteIcons;
    private readonly Dictionary<RouteNode, (Graphic border, Graphic glow, TMP_Text symbol, int road)> pins = new();
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
        SurfaceMapNavigation navigation, SurfaceMapFog fog, TMP_Text fontSource, Sprite bunkerSprite,
        SurfaceMapView.LocationIcon[] siteIcons)
    {
        this.root = root;
        this.graph = graph;
        this.navigation = navigation;
        this.fog = fog;
        this.fontSource = fontSource;
        this.bunkerSprite = bunkerSprite;
        this.siteIcons = siteIcons;
        navigation.ScrollAlongRoute = true;

        float right = Position(RunRoute.LevelCount + 1, 1).x + 1100f;
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
        var heading = Plate("Route stage HUD", chrome, new Vector2(0f, 392f), new Vector2(382f, 58f), Ink);
        header = Label("Route stage name", heading.rectTransform, Vector2.zero, new Vector2(344f, 42f), 28f, Text, true);
        header.textWrappingMode = TextWrappingModes.NoWrap;
        header.enableAutoSizing = true;
        header.fontSizeMin = 20f;
        header.fontSizeMax = 30f;
        var wallet = Plate("Route gold HUD", chrome, new Vector2(598f, 396f), new Vector2(196f, 42f), Ink);
        CreateRect("Coin", wallet.rectTransform, new Vector2(-73f, 0f), new Vector2(21f, 21f))
            .gameObject.AddComponent<SurfaceMapGlyph>().Show(SurfaceMapMeaning.Reward);
        goldLabel = Label("Route gold", wallet.rectTransform, new Vector2(17f, 0f), new Vector2(140f, 30f), 16f, SelectedText);
        goldLabel.textWrappingMode = TextWrappingModes.NoWrap;
        goldLabel.enableAutoSizing = true;
        goldLabel.fontSizeMin = 12f;
        goldLabel.fontSizeMax = 16f;
        back = Control("Route close", chrome, new Vector2(742f, 396f), new Vector2(46f, 42f), "×", out _);
        alternateButton = Control("Route variant", chrome, new Vector2(540f, 338f), new Vector2(312f, 42f), "", out alternateLabel);

        card = Plate("Route briefing", chrome, new Vector2(572f, -327f), new Vector2(380f, 144f), Ink);
        var cardRect = card.rectTransform;
        Plate("Briefing accent", cardRect, new Vector2(0f, 70f), new Vector2(368f, 4f), Selected);
        facts = Label("Node intel", cardRect, new Vector2(0f, 22f), new Vector2(332f, 58f), 17f, Text);
        facts.lineSpacing = 9f;
        facts.enableAutoSizing = true;
        facts.fontSizeMin = 16f;
        facts.fontSizeMax = 17f;
        launch = Control("Route confirm", cardRect, new Vector2(0f, -33f), new Vector2(332f, 43f), "", out launchLabel);
        launch.onClick.AddListener(() =>
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
        displayedLevel = available[0].Level;
        selected = inspected = null;
        card.gameObject.SetActive(false);
        Bind(back, close);
        Bind(alternateButton, alternate);
        foreach (Transform old in landmarks)
        {
            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);
        }
        pins.Clear();
        clearings.Clear();
        surveyedRoads.Clear();
        outlooks.Clear();
        roads.Clear();

        Vector2 bunker = Position(0, 1);
        var start = Artwork("Bunker", bunker, bunkerSprite, 174f, Color.white);
        bunkerLabel = Label("Bunker label", start, new Vector2(0f, -61f), new Vector2(190f, 32f), 23f, Text, true);
        clearings.Add(bunker);
        RouteNode last = null;
        var travelled = new HashSet<(RouteNode from, RouteNode to)>();
        var visited = new HashSet<RouteNode>(path);
        foreach (var node in path)
        {
            travelled.Add((last, node));
            last = node;
        }

        var open = new HashSet<RouteNode>(available);
        var upcoming = new HashSet<RouteNode>();
        foreach (var node in available) CollectUpcoming(node, upcoming);
        var incoming = new Dictionary<RouteNode, int>();

        foreach (var entry in route.Entries) DrawRoad(null, entry);
        for (int level = 1; level <= RunRoute.LevelCount; level++)
        {
            foreach (var node in route.NodesAt(level))
                foreach (var next in node.Next)
                    DrawRoad(node, next);
        }

        for (int level = 1; level <= RunRoute.LevelCount; level++)
            foreach (var node in route.NodesAt(level))
            {
                bool cleared = visited.Contains(node);
                bool enter = open.Contains(node);
                bool forecast = upcoming.Contains(node) && !enter;
                if (cleared || enter) clearings.Add(Position(node));
                else if (forecast) outlooks.Add(Position(node));
                AddPin(node, cleared ? Visited : enter ? Available : forecast ? Preview : Muted,
                    enter, cleared ? null : forecast || enter ? Symbol(TypeOf(node)) : "·",
                    incoming.TryGetValue(node, out int road) ? road : -1, forecast);
            }

        Vector2 boss = Position(RunRoute.LevelCount + 1, 1);
        foreach (var node in route.NodesAt(RunRoute.LevelCount))
            roads.Add(Position(node), boss, upcoming.Contains(node) ? SurfaceRouteRoads.State.Ahead : SurfaceRouteRoads.State.Missed);
        outlooks.Add(boss);
        var bossSite = Artwork("Final boss", boss, FindArt(SurfaceLocationFamily.Technogenic), 168f, Preview);
        var bossPlate = Plate("Boss marker", bossSite, Vector2.zero, new Vector2(74f, 68f), Selected);
        Plate("Inset", bossPlate.rectTransform, Vector2.zero, new Vector2(64f, 58f), Ink);
        CreateRect("Boss icon", bossPlate.rectTransform, Vector2.zero, new Vector2(42f, 42f))
            .gameObject.AddComponent<SurfaceMapGlyph>().Show(SurfaceMapMeaning.Boss);
        bossLabel = Label("Boss label", bossSite, new Vector2(0f, -63f), new Vector2(310f, 36f), 20f, Text, true);

        Vector2 current = last == null ? bunker : Position(last);
        Vector2 markerOffset = path.Count == 0 ? new Vector2(-90f, -37f) : new Vector2(-3f, -72f);
        characterMarker = Artwork("Route character", current + markerOffset, character?.GameplayIcon,
            66f, Color.white).GetComponent<Image>();

        Canvas.ForceUpdateCanvases();
        var size = root.rect.size;
        chrome.localScale = Vector3.one * Mathf.Min(size.x / 1600f, size.y / 900f);
        float zoom = Mathf.Clamp(size.x / 2280f, SurfaceMapNavigation.MinZoom, .90f);
        float focus = boss.x * .5f;
        navigation.SetBounds(Rect.MinMaxRect(-570f, -510f, boss.x + 650f, 510f), new Vector2(focus, 0f));
        graph.localScale = Vector3.one * zoom;
        graph.anchoredPosition = new Vector2(-focus, -10f) * zoom;
        navigation.Pan(Vector2.zero);
        fog.Reveal(clearings, surveyedRoads, outlooks);
        roads.SetVerticesDirty();
        RefreshText();

        void DrawRoad(RouteNode from, RouteNode to)
        {
            bool onPath = travelled.Contains((from, to));
            bool enter = from == last && open.Contains(to);
            bool ahead = from != null && upcoming.Contains(from) && upcoming.Contains(to);
            var state = onPath ? SurfaceRouteRoads.State.Travelled : enter ? SurfaceRouteRoads.State.Open :
                ahead ? SurfaceRouteRoads.State.Ahead : SurfaceRouteRoads.State.Missed;
            int road = roads.Add(from == null ? bunker : Position(from), Position(to), state,
                onPath || enter ? surveyedRoads : null);
            if (enter) incoming.Add(to, road);
        }
    }

    private static void CollectUpcoming(RouteNode node, HashSet<RouteNode> upcoming)
    {
        if (!upcoming.Add(node)) return;
        foreach (var next in node.Next) CollectUpcoming(next, upcoming);
    }

    internal void SetCharacter(CharacterData character)
    {
        if (characterMarker == null) return;
        characterMarker.sprite = character?.GameplayIcon;
        characterMarker.enabled = characterMarker.sprite != null;
    }

    internal void SetGold(int gold)
    {
        runGold = gold;
        goldLabel.text = F("route.gold", runGold);
    }

    private static void Bind(Button button, Action action)
    {
        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(action != null);
        if (action != null) button.onClick.AddListener(() => action());
    }

    private void AddPin(RouteNode node, Color accent, bool canSelect, string symbol, int road, bool forecast)
    {
        var pos = Position(node);
        var type = canSelect || forecast || symbol == null ? TypeOf(node) : null;
        var site = Artwork("Site", pos, FindArt(type switch
        {
            "route.node.anomaly" => SurfaceLocationFamily.Anomalous,
            "route.node.golden" => SurfaceLocationFamily.AdvancedAnomalous,
            "route.node.minefield" or "route.node.rare" => SurfaceLocationFamily.Technogenic,
            _ => SurfaceLocationFamily.Ruins
        }), 149f, canSelect ? Color.white : new Color(.61f, .67f, .65f));
        Graphic glow = null;
        if (canSelect)
        {
            glow = Plate("Route selection glow", site, Vector2.zero, new Vector2(99f, 91f), new Color(1f, .59f, .12f, .44f));
            Plate("Route selection edge", glow.rectTransform, Vector2.zero, new Vector2(83f, 77f), Selected);
            glow.gameObject.SetActive(false);
        }
        Vector2 size = canSelect ? new Vector2(68f, 62f) : new Vector2(57f, 53f);
        var badge = Plate($"Route location {node.Level}-{node.Row + 1}", site,
            Vector2.zero, size, accent);
        Plate("Inset", badge.rectTransform, Vector2.zero, size - new Vector2(10f, 10f), Ink);
        if (symbol == null)
        {
            var cleared = CreateRect("Cleared", badge.rectTransform, Vector2.zero, new Vector2(25f, 25f))
                .gameObject.AddComponent<SurfaceMapGlyph>();
            cleared.Show(SurfaceMapMeaning.Cleared);
            cleared.color = accent;
            return;
        }
        var icon = Label("Type mark", badge.rectTransform, Vector2.zero, new Vector2(55f, 49f), 31f, accent, true);
        icon.text = symbol;
        if (forecast)
        {
            badge.raycastTarget = true;
            badge.gameObject.AddComponent<SurfaceMapNodeFeedback>().HoverChanged = over =>
            {
                inspected = over ? node : null;
                RefreshText();
            };
        }
        if (!canSelect) return;
        pins.Add(node, (badge, glow, icon, road));
        var button = badge.gameObject.AddComponent<Button>();
        button.targetGraphic = badge;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        badge.raycastTarget = true;
        button.onClick.AddListener(() => Select(node));
    }

    private void Select(RouteNode node)
    {
        if (selected != null)
        {
            pins[selected].border.color = Available;
            pins[selected].symbol.color = Available;
            pins[selected].glow.gameObject.SetActive(false);
        }
        selected = node;
        inspected = null;
        var pin = pins[node];
        pin.border.color = SelectedText;
        pin.symbol.color = SelectedText;
        pin.glow.gameObject.SetActive(true);
        roads.Select(pin.road);
        RefreshText();
    }

    internal void RefreshText()
    {
        if (displayedLevel == 0) return;
        header.text = F("route.stage.name", displayedLevel, RunRoute.LevelCount);
        goldLabel.text = F("route.gold", runGold);
        bunkerLabel.text = L("map.home");
        if (bossLabel != null) bossLabel.text = L("route.node.boss");
        launchLabel.text = L("route.confirm");
        if (alternateKey != null) alternateLabel.text = L(alternateKey);

        var node = inspected ?? selected;
        card.gameObject.SetActive(node != null);
        if (node == null) return;
        card.raycastTarget = inspected == null;
        launch.gameObject.SetActive(inspected == null);
        string type = TypeOf(node);
        var rule = node.WorldRule;
        bool clear = rule == null || rule.RuleType is WorldRuleType.None or WorldRuleType.Golden;
        string weather = clear ? L("route.weather.clear") : rule.DisplayName;
        facts.text = $"{F("route.weather", weather)}\n{F("route.location_type", L(type))}";
    }

    private static string TypeOf(RouteNode node) => node.Type?.LabelKey ?? "route.node.combat";
    private static string Symbol(string type) => type switch
    {
        "route.node.unknown" => "?",
        "route.node.minefield" => "!",
        "route.node.anomaly" => "~",
        "route.node.golden" => "$",
        "route.node.rare" => "+",
        _ => "×"
    };
    private static string L(string key) => SurfaceMapBriefing.Text(key);
    private static string F(string key, params object[] args) => SurfaceMapBriefing.Format(key, args);

    private static Vector2 Position(RouteNode node) => Position(node.Level, node.Row, node.RowCount);
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
        var plate = Plate(name, parent, pos, size, new Color(.20f, .28f, .29f));
        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        plate.raycastTarget = true;
        label = Label("Label", plate.rectTransform, Vector2.zero, size - new Vector2(12f, 4f), 22f, Text, true);
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
