using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelChoiceCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI weatherText;

    private RouteNode node;
    public RouteNode Node => node;
    public bool IsAvailable { get; private set; }

    internal void Bind(RouteNode routeNode, Action<RouteNode> onSelected)
    {
        node = routeNode;
        IsAvailable = onSelected != null;
        button.interactable = IsAvailable;
        if (IsAvailable) button.onClick.AddListener(() => onSelected(node));
    }

    internal void RefreshText()
    {
        var localization = LocalizationService.Instance;
        titleText.text = localization.Get(node.LocationLabelKey);
        var rule = node.WorldRule;
        bool clear = rule == null || rule.RuleType == WorldRuleType.None;
        weatherText.text = clear ? localization.Get("route.weather.clear") : rule.DisplayName;
        var readable = new Color(.72f, .84f, .9f);
        weatherText.color = clear ? readable : Color.Lerp(rule.PresentationColor, readable, .3f);
    }

    internal void SetAccent(Color accent, bool selected, bool current)
    {
        frameImage.color = accent;
        titleText.color = accent;
        button.image.color = selected ? new Color(.15f, .32f, .40f) :
            current ? new Color(.20f, .15f, .10f) : new Color(.055f, .10f, .14f);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void Press() => button.onClick.Invoke();
#endif
}
