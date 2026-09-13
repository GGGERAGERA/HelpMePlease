using System.Collections.Generic;
using UnityEngine;

/// <summary>Presentation only. Reads the pair's existing flash/cooldown timers.</summary>
public sealed class ProductionPortalVisual : MonoBehaviour
{
    [SerializeField] private Material centerMaterial;
    [SerializeField] private Material ringMaterial;
    [SerializeField] private Material arcMaterial;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Material sparkMaterial;

    private readonly List<Sprite> sprites = new();
    private SpriteRenderer center;
    private SpriteRenderer ring;
    private SpriteRenderer arcs;
    private SpriteRenderer halo;
    private SpriteRenderer flash;
    private ParticleSystem motes;
    private Material moteMaterial;
    private Color tint;
    private float phase;

    public void Initialize(Color color, float animationPhase)
    {
        tint = color;
        phase = animationPhase;
        halo = CreateLayer("Soft ground glow", glowMaterial, 2.05f, 26);
        center = CreateLayer("Dark rift center", centerMaterial, 1.35f, 27);
        ring = CreateLayer("Torn energy rim", ringMaterial, 1.85f, 28,
            new Rect(0f, 0f, 0.5f, 0.5f));
        // Reuse one broken-ring frame from the existing 2x2 portal_soft atlas.
        arcs = CreateLayer("Counter-rotating arcs", arcMaterial, 1.55f, 29,
            new Rect(0.5f, 0f, 0.5f, 0.5f));
        flash = CreateLayer("Teleport energy flash", glowMaterial, 2.1f, 32);
        CreateMotes();
        Render(0f, 0f, 0f);
    }

    private SpriteRenderer CreateLayer(string label, Material source, float diameter,
        int order, Rect uv = default)
    {
        var texture = source.mainTexture as Texture2D;
        if (uv.width == 0f) uv = new Rect(0f, 0f, 1f, 1f);
        Rect pixels = new(uv.x * texture.width, uv.y * texture.height,
            uv.width * texture.width, uv.height * texture.height);
        // Sprite wrappers only: all pixels and materials come from existing Epic Toon FX assets.
        Sprite sprite = Sprite.Create(texture, pixels, new Vector2(0.5f, 0.5f),
            pixels.width, 0, SpriteMeshType.FullRect);
        sprites.Add(sprite);
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        child.transform.localScale = Vector3.one * diameter;
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = source;
        renderer.sortingLayerName = "Effects";
        renderer.sortingOrder = order;
        return renderer;
    }

    private void CreateMotes()
    {
        var child = new GameObject("Rift sparks");
        child.transform.SetParent(transform, false);
        motes = child.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        motes.useAutoRandomSeed = false;
        motes.randomSeed = 42;
        var main = motes.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.095f);
        main.startColor = Color.white;
        main.maxParticles = 10;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = motes.emission;
        emission.rateOverTime = 6f;
        var shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.82f;
        shape.radiusThickness = 0.2f;
        var fade = motes.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        moteMaterial = new Material(sparkMaterial) { name = "Portal spark tint" };
        var renderer = motes.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = moteMaterial;
        renderer.sortingLayerName = "Effects";
        renderer.sortingOrder = 31;
        motes.Play();
    }

    public void Render(float time, float cooldownRemaining, float flashRemaining)
    {
        if (ring == null) return;
        float recovery = 1f - Mathf.Clamp01(cooldownRemaining);
        float brightness = Mathf.Lerp(0.16f, 1f, recovery * recovery);
        float pulse = 1f + 0.035f * Mathf.Sin(time * 2.4f + phase);
        float crackle = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 7.3f + phase)), 18f);
        float teleport = Mathf.Clamp01(flashRemaining / 0.18f);
        center.color = new Color(0.025f, 0.012f, 0.045f, 0.94f);
        ring.color = new Color(tint.r, tint.g, tint.b, brightness * (0.82f + crackle * 0.18f));
        arcs.color = new Color(tint.r, tint.g, tint.b, brightness * (0.38f + crackle * 0.45f));
        halo.color = new Color(tint.r, tint.g, tint.b, brightness * 0.17f);
        flash.color = new Color(0.85f + tint.r * 0.15f, 0.85f + tint.g * 0.15f,
            0.85f + tint.b * 0.15f, teleport * teleport);
        ring.transform.localRotation = Quaternion.Euler(0f, 0f, time * 11f + phase * Mathf.Rad2Deg);
        arcs.transform.localRotation = Quaternion.Euler(0f, 0f, -time * 17f + phase * Mathf.Rad2Deg);
        ring.transform.localScale = Vector3.one * (1.85f * pulse);
        arcs.transform.localScale = Vector3.one * (1.55f / pulse);
        flash.transform.localScale = Vector3.one * (2.1f + (1f - teleport) * 0.25f);
        moteMaterial.SetColor("_Color", new Color(tint.r, tint.g, tint.b, brightness));
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in sprites) if (sprite != null) Destroy(sprite);
        if (moteMaterial != null) Destroy(moteMaterial);
    }
}
