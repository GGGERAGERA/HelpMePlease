using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Subject42UITheme;

/// <summary>Route briefing laid out in pixels on the route's 1600×900 reference canvas.</summary>
internal sealed class SurfaceRouteCard
{
    private const float Width = 845f;
    private const float Height = 257f;
    private const float Padding = 15f;
    private const float ValueColumn = 429f;
    private readonly TMP_FontAsset font;
    private readonly Material textMaterial;
    private readonly Image panel;
    private readonly TMP_Text title, action, modifier, duration, reward, difficulty;
    private readonly TMP_Text modifierLabel, durationLabel, rewardLabel, difficultyLabel;
    internal Button Launch { get; }

    internal SurfaceRouteCard(RectTransform parent, TMP_FontAsset font, Material textMaterial)
    {
        this.font = font;
        this.textMaterial = textMaterial;
        var rect = Rect("Route briefing", parent, Vector2.zero, new Vector2(Width, Height));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-38f, 42f);
        panel = Subject42UIStyle.Panel(rect);

        title = Text("Node title", rect, Padding, 19f, Width - 2f * Padding - 28f, 22f, 16f, PrimaryText, true);
        Rect("Combat icon", rect, new Vector2(Width - Padding - 18f, 21f), new Vector2(18f, 18f))
            .gameObject.AddComponent<SurfaceMapGlyph>().Show(SurfaceMapMeaning.Combat);

        (modifierLabel, modifier) = Row("Modifier", rect, 59f);
        (durationLabel, duration) = Row("Duration", rect, 90f);
        (rewardLabel, reward) = Row("Reward", rect, 121f, Amber);
        (difficultyLabel, difficulty) = Row("Difficulty", rect, 151f);

        var button = Rect("Route confirm", rect, new Vector2(Padding, 185f), new Vector2(Width - 2f * Padding, 57f));
        Launch = Subject42UIStyle.ActionButton(button);
        action = Subject42UIStyle.Text(Rect("Label", button, Vector2.zero, button.sizeDelta), font, textMaterial,
            15.5f, PrimaryText, true, TextAlignmentOptions.Center);
        Show(null, false);
    }

    internal void Show(RouteNode node, bool canLaunch)
    {
        panel.gameObject.SetActive(node != null);
        if (node == null) return;

        panel.raycastTarget = canLaunch;
        Launch.gameObject.SetActive(canLaunch);
        action.text = L("route.confirm");
        // SectorNumber is the route column. The local sector slot is the node's row.
        title.text = F("route.node.name", node.Row + 1);

        modifierLabel.text = L("route.weather");
        durationLabel.text = L("route.duration");
        rewardLabel.text = L("route.reward");
        difficultyLabel.text = L("route.difficulty");

        var rule = node.WorldRule;
        var ruleType = rule?.RuleType ?? WorldRuleType.None;
        modifier.text = ruleType == WorldRuleType.None ? L("route.weather.clear") : SentenceCase(rule.DisplayName);
        modifier.color = WorldRule(ruleType);
        duration.text = F("route.duration.value", node.DurationSeconds);
        string rewardKey = node.RewardProfile switch
        {
            RewardProfile.WeaponLevel => "route.reward.weapon",
            RewardProfile.ModuleLevel => "route.reward.module",
            _ => null
        };
        rewardLabel.gameObject.SetActive(rewardKey != null);
        reward.gameObject.SetActive(rewardKey != null);
        if (rewardKey != null) reward.text = L(rewardKey);
        difficulty.text = L(node.Difficulty switch
        {
            RouteDifficulty.Easy => "route.difficulty.easy",
            RouteDifficulty.Hard => "route.difficulty.hard",
            _ => "route.difficulty.medium"
        });
        difficulty.color = node.Difficulty switch
        {
            RouteDifficulty.Hard => Coral,
            RouteDifficulty.Medium => Amber,
            _ => PrimaryText
        };
    }

    private (TMP_Text label, TMP_Text value) Row(string name, RectTransform parent, float y, Color? tint = null)
    {
        var label = Text(name + " label", parent, Padding, y, ValueColumn - 2f * Padding, 22f, 15f, SecondaryText);
        var value = Text(name + " value", parent, ValueColumn, y, Width - ValueColumn - Padding, 22f, 14f,
            tint ?? PrimaryText, true);
        return (label, value);
    }

    private TMP_Text Text(string name, RectTransform parent, float x, float y, float width, float height, float size,
        Color tint, bool bold = false) =>
        Subject42UIStyle.Text(Rect(name, parent, new Vector2(x, y), new Vector2(width, height)), font, textMaterial, size, tint, bold);

    private static RectTransform Rect(string name, RectTransform parent, Vector2 offset, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(offset.x, -offset.y);
        return rect;
    }

    private static string SentenceCase(string value) => value.Length < 2
        ? value
        : value.Substring(0, 1) + value.Substring(1).ToLowerInvariant();

    private static string L(string key) => SurfaceMapBriefing.Text(key);
    private static string F(string key, params object[] args) => SurfaceMapBriefing.Format(key, args);
}
