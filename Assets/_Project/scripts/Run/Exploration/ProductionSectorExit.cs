using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class ProductionSectorExit : MonoBehaviour
{
    private static readonly List<ProductionSectorExit> activeExits = new();

    private RunFlowController runFlow;
    private Material material;
    private LineRenderer ring;
    private LineRenderer glow;
    private Transform player;
    private Mesh platformMesh;
    private float visualRadius;
    private float proximity;
    private float nextPlayerSearch;
    private float pulse;
    private bool initialized;
    private ParticleSystem energyParticles;
    private readonly List<LineRenderer> chevrons = new();

    public static IReadOnlyList<ProductionSectorExit> ActiveExits =>
        activeExits;
    public bool IsMapVisible => initialized && isActiveAndEnabled;
    public bool IsAvailable => IsMapVisible && runFlow != null &&
        runFlow.IsExitUnlocked && !runFlow.IsLevelCompleted && runFlow.Phase == RunPhase.NormalSector;

    private void OnEnable()
    {
        if (!activeExits.Contains(this))
            activeExits.Add(this);
    }

    private void OnDisable()
    {
        activeExits.Remove(this);
    }

    public void Initialize(
        Vector2 position,
        float radius,
        RunFlowController flow,
        Material energy)
    {
        transform.position = position;
        runFlow = flow;
        initialized = true;

        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.5f, radius);

        CreateVisual(trigger.radius, energy);
    }

    // Presentation only: root transform and trigger geometry never animate.
    private void CreateVisual(float radius, Material energy)
    {
        visualRadius = radius;
        material = AnomalyPowerVisuals.CreateMaterial("Sector Exit Runtime Material");
        var vertices = new Vector3[9];
        var triangles = new int[24];
        var colors = new Color[9];
        colors[0] = new Color(0.025f, 0.045f, 0.065f, 1f);
        for (int i = 0; i < 8; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI / 4f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            colors[i + 1] = new Color(0.055f, 0.09f, 0.12f, 1f);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % 8 + 1;
            triangles[i * 3 + 2] = i + 1;
        }
        platformMesh = new Mesh { name = "Exit platform", vertices = vertices, triangles = triangles, colors = colors };
        platformMesh.RecalculateBounds();
        var platform = new GameObject("Exit dark platform", typeof(MeshFilter), typeof(MeshRenderer));
        platform.transform.SetParent(transform, false);
        platform.GetComponent<MeshFilter>().sharedMesh = platformMesh;
        var surface = platform.GetComponent<MeshRenderer>();
        surface.sharedMaterial = material;
        surface.sortingLayerName = "Midground";
        surface.sortingOrder = 26;
        glow = CreateContour("Exit contour glow", vertices, 0.32f, new Color(0.1f, 0.8f, 1f, 0.18f), 27);
        ring = CreateContour("Exit energy contour", vertices, 0.065f, new Color(0.55f, 0.95f, 1f), 28);
        for (int i = 0; i < 2; i++)
        {
            var arrow = AnomalyPowerVisuals.CreateLine(transform, "Exit forward chevron", new Color(0.8f, 0.97f, 1f, 0.9f), 0.11f, 3, material);
            arrow.useWorldSpace = false;
            float y = (-0.36f + i * 0.43f) * radius;
            arrow.SetPosition(0, new Vector3(-0.3f * radius, y, 0f));
            arrow.SetPosition(1, new Vector3(0f, y + 0.22f * radius, 0f));
            arrow.SetPosition(2, new Vector3(0.3f * radius, y, 0f));
            chevrons.Add(arrow);
        }

        // Reuse Epic Toon FX's portal glow texture without its vortex/gameplay prefab.
        if (energy == null) return;
        var fxObject = new GameObject("Exit ascending energy");
        fxObject.transform.SetParent(transform, false);
        fxObject.transform.localPosition = new Vector3(0f, radius * 0.3f, 0f);
        var particles = fxObject.AddComponent<ParticleSystem>();
        energyParticles = particles;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
        main.startColor = new Color(0.4f, 0.9f, 1f, 0.65f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission;
        emission.rateOverTime = 9f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 0.65f, 0.12f, 0f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = radius * 0.85f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = energy;
        renderer.sortingLayerName = "Midground";
        renderer.sortingOrder = 29;
        if (IsAvailable) particles.Play();
    }

    private LineRenderer CreateContour(string label, Vector3[] vertices, float width, Color color, int order)
    {
        var line = AnomalyPowerVisuals.CreateLine(transform, label, color, width, 9, material);
        line.useWorldSpace = false;
        line.sortingOrder = order;
        for (int i = 0; i < 9; i++) line.SetPosition(i, vertices[i % 8 + 1]);
        return line;
    }

    private void Update()
    {
        if (ring == null) return;
        if (player == null && Time.time >= nextPlayerSearch)
        {
            nextPlayerSearch = Time.time + 1f;
            var actor = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
            if (actor != null) player = actor;
        }
        float target = player != null
            ? 1f - Mathf.InverseLerp(visualRadius, visualRadius + 4f, Vector2.Distance(player.position, transform.position))
            : 0f;
        proximity = Mathf.MoveTowards(proximity, target, Time.deltaTime * 2f);
        pulse += Time.deltaTime * 2f;
        float brightness = 0.78f + Mathf.Sin(pulse) * 0.06f + proximity * 0.16f;
        ring.startColor = ring.endColor = new Color(0.55f, 0.95f, 1f, brightness);
        glow.startColor = glow.endColor = new Color(0.1f, 0.8f, 1f, 0.16f + proximity * 0.13f + Mathf.Sin(pulse) * 0.025f);
        // A closed, dim amber contour reads as locked without a localized label.
        if (!IsAvailable)
        {
            ring.startColor = ring.endColor = new Color(0.6f, 0.34f, 0.12f, 0.55f);
            glow.startColor = glow.endColor = Color.clear;
        }
        foreach (var chevron in chevrons) chevron.enabled = IsAvailable;
        if (energyParticles != null)
        {
            if (IsAvailable && !energyParticles.isPlaying) energyParticles.Play();
            else if (!IsAvailable && energyParticles.isPlaying)
                energyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerHealth>() == null)
            return;

        if (IsAvailable && runFlow.HandleExitReached())
            enabled = false;
    }

    private void OnTriggerStay2D(Collider2D other) => OnTriggerEnter2D(other);

    private void OnDestroy()
    {
        activeExits.Remove(this);

        if (material != null)
            Destroy(material);
        if (platformMesh != null)
            Destroy(platformMesh);
    }
}
