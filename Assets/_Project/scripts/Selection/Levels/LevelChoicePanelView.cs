using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelChoicePanelView : MonoBehaviour
{
    [SerializeField] private SurfaceMapView surfaceMapPrefab;
    private SurfaceMapView map;

    public void Show(RunRouteGraph graph, IReadOnlyList<RouteNode> path, IReadOnlyList<RouteNode> available,
        Func<RouteNode, bool> onConfirmed)
    {
        gameObject.SetActive(true);
        if (map == null) map = Instantiate(surfaceMapPrefab, transform);
        map.ShowRoute(graph, path, available, onConfirmed, run: RunStateManager.Instance);
    }

    public void Hide() => gameObject.SetActive(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal bool DebugConfirmFirstNode() => map != null && map.DebugConfirmFirstRouteNode();
#endif
}
