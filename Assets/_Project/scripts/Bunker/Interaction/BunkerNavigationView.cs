using System;
using UnityEngine;

/// <summary>Authored floor network. Geometry never follows the player.</summary>
public sealed class BunkerNavigationView : MonoBehaviour
{
    [Serializable]
    public sealed class Route
    {
        public string destination;
        public MeshRenderer floor;
        public BunkerRoomAccess room;
        public BunkerStation station;
        public BunkerGateVisual gate;
        public BunkerMinigame minigame;
        [Range(0f, 1f)] public float brightness = .7f;

        // The exit is a permanent landmark, including while its gate is closed.
        // Other branches still follow access; unbound branches fail closed.
        public bool IsAvailable => gate != null || (room != null || station != null || minigame != null) &&
            (room == null || (room.isActiveAndEnabled && room.Unlocked)) &&
            (station == null || (station.isActiveAndEnabled && station.CanInteract)) &&
            (minigame == null || minigame.isActiveAndEnabled);
    }

    [SerializeField] private MeshRenderer mainLine;
    [SerializeField] private Route[] routes = Array.Empty<Route>();
    private MaterialPropertyBlock properties;
    private static readonly int ActiveId = Shader.PropertyToID("_Active");

    private void OnEnable()
    {
        foreach (var route in routes)
        {
            if (route.gate != null) continue;
            if (route.room != null) route.room.AvailabilityChanged += Refresh;
            if (route.station != null) route.station.AvailabilityChanged += Refresh;
            if (route.minigame != null) route.minigame.AvailabilityChanged += Refresh;
        }
        Refresh();
    }

    // Handles arbitrary Awake/OnEnable order when the scene is initialized/restored.
    private void Start() => Refresh();

    public void Refresh()
    {
        SetVisible(mainLine, isActiveAndEnabled, .75f);
        foreach (var route in routes)
            SetVisible(route.floor, isActiveAndEnabled && route.IsAvailable, route.brightness);
    }

    private void SetVisible(MeshRenderer floor, bool visible, float brightness)
    {
        if (floor == null) return;
        floor.enabled = visible;
        if (!visible) return;
        properties ??= new MaterialPropertyBlock();
        floor.GetPropertyBlock(properties);
        properties.SetFloat(ActiveId, brightness);
        floor.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        SetVisible(mainLine, false, 0);
        foreach (var route in routes)
        {
            SetVisible(route.floor, false, 0);
            if (route.gate != null) continue;
            if (route.room != null) route.room.AvailabilityChanged -= Refresh;
            if (route.station != null) route.station.AvailabilityChanged -= Refresh;
            if (route.minigame != null) route.minigame.AvailabilityChanged -= Refresh;
        }
    }
}
