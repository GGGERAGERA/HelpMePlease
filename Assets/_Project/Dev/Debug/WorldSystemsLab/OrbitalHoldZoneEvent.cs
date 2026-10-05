#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Only the lab prefab uses this event. Production CaptureZoneEvent stays intact.
public sealed class OrbitalHoldZoneEvent : WorldEvent
{
    [Serializable]
    public sealed class Node
    {
        public SpriteRenderer Area;
        public SpriteRenderer Indicator;
        [NonSerialized] public float Progress;
        [NonSerialized] public bool Contact;
    }

    [SerializeField] private float holdRadius = 6f;
    [SerializeField] private float nodeRadius = 1.2f;
    [SerializeField] private float fillSeconds = 1.5f;
    [SerializeField] private Node[] nodes;
    private Transform player;
    private OrbitalStationRuntime station;
    public bool PlayerInside => player != null && CanStartFrom(player.position);
    public int CompletedNodes { get; private set; }
    public Node[] Nodes => nodes;

    public void Configure(Transform target, OrbitalStationRuntime orbital)
    {
        player = target;
        station = orbital;
        RefreshVisuals();
    }

    protected override bool CanStartFrom(Vector2 position) =>
        ((Vector2)transform.position - position).sqrMagnitude <= holdRadius * holdRadius;

    private void LateUpdate()
    {
        if (IsCompleted) return;
        bool canFill = IsStarted && PlayerInside && station != null && station.IsInitialized &&
            !station.Owner.IsDead && Time.timeScale > 0f;
        CompletedNodes = 0;
        foreach (Node node in nodes)
        {
            node.Contact = false;
            if (canFill && node.Progress < 1f)
                foreach (OrbitalModuleRuntime module in station.Modules)
                    if (module.TouchesCircle(node.Area.transform.position, nodeRadius))
                    {
                        node.Contact = true;
                        break;
                    }
            if (node.Contact) node.Progress = Mathf.Min(1f, node.Progress + Time.deltaTime / fillSeconds);
            if (node.Progress >= 1f) CompletedNodes++;
        }
        RefreshVisuals();
        if (canFill && CompletedNodes == nodes.Length) CompleteEvent();
    }

    private void RefreshVisuals()
    {
        foreach (Node node in nodes)
        {
            Color color = node.Progress >= 1f ? Color.green : node.Contact ? Color.yellow : Color.cyan;
            node.Area.color = new Color(color.r, color.g, color.b, node.Contact || node.Progress >= 1f ? .65f : .25f);
            node.Indicator.color = color;
            Vector3 scale = node.Indicator.transform.localScale;
            scale.x = 2f * nodeRadius * node.Progress;
            node.Indicator.transform.localScale = scale;
        }
    }
}
#endif
