using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelChoicePanelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private RectTransform mapRoot;
    [SerializeField] private LevelChoiceCardView nodePrefab;
    [SerializeField] private Button confirmButton;

    private static readonly Color Available = new(.33f, .88f, 1f);
    private static readonly Color Current = new(1f, .76f, .37f);
    private static readonly Color Future = new(.42f, .61f, .72f);
    private readonly Dictionary<RouteNode, LevelChoiceCardView> nodes = new();
    private readonly List<Image> links = new();
    private IReadOnlyList<RouteNode> choices;
    private Func<RouteNode, bool> confirm;
    private RouteNode currentNode;
    private RouteNode selectedNode;
    private int level;
    private int rows;

    private void Awake() => confirmButton.onClick.AddListener(ConfirmSelection);
    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }

    public void Show(RunRouteGraph graph, RouteNode current, IReadOnlyList<RouteNode> available,
        Func<RouteNode, bool> onConfirmed)
    {
        level = graph.Level;
        rows = graph.Rows;
        choices = available;
        confirm = onConfirmed;
        currentNode = current;
        selectedNode = null;
        gameObject.SetActive(true);
        if (current != null) AddReachableNode(current, false);
        else foreach (var entry in choices) AddReachableNode(entry, true);

        foreach (var node in nodes.Keys)
            foreach (var next in node.Next)
                AddLink(node, next);
        RefreshLanguage(LocalizationService.Instance.CurrentLanguage);
        RefreshSelection();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        foreach (var node in nodes.Values) Destroy(node.gameObject);
        foreach (var link in links) Destroy(link.gameObject);
        nodes.Clear();
        links.Clear();
        choices = null;
        confirm = null;
        currentNode = selectedNode = null;
    }

    private void AddReachableNode(RouteNode node, bool available)
    {
        if (nodes.ContainsKey(node)) return;
        var view = Instantiate(nodePrefab, mapRoot);
        ((RectTransform)view.transform).anchoredPosition = PositionOf(node);
        view.Bind(node, available ? SelectNode : null);
        nodes.Add(node, view);
        foreach (var next in node.Next) AddReachableNode(next, node == currentNode);
    }

    private Vector2 PositionOf(RouteNode node) =>
        new((node.Column - 1) * 365f, ((rows - 1) * .5f - node.Row) * 154f);

    private void AddLink(RouteNode from, RouteNode to)
    {
        // The endpoints stop at the frame so even a diagonal stays clear of the labels.
        var a = PositionOf(from) + Vector2.right * 128f;
        var b = PositionOf(to) - Vector2.right * 128f;
        var delta = b - a;
        var link = new GameObject("Route link", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        link.gameObject.layer = mapRoot.gameObject.layer;
        var rect = link.rectTransform;
        rect.SetParent(mapRoot, false);
        rect.SetAsFirstSibling();
        rect.anchoredPosition = (a + b) * .5f;
        rect.sizeDelta = new Vector2(delta.magnitude, from == currentNode ? 4f : 2f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        link.raycastTarget = false;
        link.color = from == currentNode ? Available : new Color(.24f, .46f, .56f);
        links.Add(link);
    }

    private void RefreshLanguage(GameLanguage _)
    {
        titleText.text = string.Format(LocalizationService.Instance.Get("route.map.title"), level, RunRoute.LevelCount);
        foreach (var view in nodes.Values) view.RefreshText();
    }

    private void SelectNode(RouteNode node)
    {
        selectedNode = node;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        confirmButton.interactable = selectedNode != null;
        foreach (var pair in nodes)
        {
            bool available = pair.Value.IsAvailable;
            Color accent = pair.Key == currentNode ? Current : available ? Available : Future;
            pair.Value.SetAccent(accent, pair.Key == selectedNode, pair.Key == currentNode);
        }
    }

    private void ConfirmSelection()
    {
        if (selectedNode != null && confirm?.Invoke(selectedNode) == true)
            AudioService.Instance?.Play(AudioCueId.UIConfirm);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal bool DebugConfirmFirstNode()
    {
        if (choices == null || choices.Count == 0) return false;
        nodes[choices[0]].Press();
        confirmButton.onClick.Invoke();
        return !gameObject.activeSelf;
    }
#endif
}
