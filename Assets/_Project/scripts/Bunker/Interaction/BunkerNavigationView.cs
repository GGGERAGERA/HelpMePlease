using System;
using TMPro;
using UnityEngine;

/// <summary>Animates authored floor meshes. Progress belongs to station progression.</summary>
public sealed class BunkerNavigationView : MonoBehaviour
{
    [Serializable]
    public sealed class Route
    {
        public BunkerOnboardingStep step;
        public MeshRenderer floor;
        public BunkerStation station;
        public TMP_Text label;
        public string title;
    }

    [SerializeField] private Transform player;
    [SerializeField] private BunkerPanelManager panels;
    [SerializeField] private BunkerIntroController intro;
    [SerializeField] private Route[] routes = Array.Empty<Route>();
    private MaterialPropertyBlock properties;
    private static readonly int ActiveId = Shader.PropertyToID("_Active");
    private BunkerOnboardingStep lastStep = (BunkerOnboardingStep)(-1);
    private bool lastGuidance;

    private void Update()
    {
        BunkerOnboardingStep step = BunkerStationProgressionService.OnboardingStep;
        bool guidance = intro == null || !intro.IsPlaying;
        Apply(step, guidance);
    }

    public void Apply(BunkerOnboardingStep step, bool guidance)
    {
        properties ??= new MaterialPropertyBlock();
        bool changed = step != lastStep || guidance != lastGuidance;
        foreach (Route route in routes)
        {
            bool active = guidance && step == route.step;
            if (changed)
            {
                properties.SetFloat(ActiveId, active ? 1f : 0f);
                route.floor.SetPropertyBlock(properties);
            }
            bool near = active && !panels.IsAnyPanelOpen &&
                Vector2.Distance(player.position, route.label.transform.position) < 3.5f;
            string text = near ? "ЛКМ · " + route.station.InteractionText : route.title;
            if (route.label.text != text) route.label.text = text;
            route.label.color = active ? new Color(.48f, .88f, .82f, .95f) : new Color(.38f, .52f, .51f, .38f);
        }
        lastStep = step;
        lastGuidance = guidance;
    }

    private void OnDisable()
    {
        if (properties == null) return; // Editor-created view may never have been presented.
        properties.SetFloat(ActiveId, 0f);
        foreach (Route route in routes) route.floor.SetPropertyBlock(properties);
        lastStep = (BunkerOnboardingStep)(-1);
    }
}
