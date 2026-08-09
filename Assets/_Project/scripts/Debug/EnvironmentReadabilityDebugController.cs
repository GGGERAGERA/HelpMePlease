using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class EnvironmentReadabilityDebugController : MonoBehaviour
{
    public enum ReadabilityPreset
    {
        Original,
        MutedWorld,
        HighGameplayContrast,
        DarkWorld
    }

    private sealed class EnvironmentRendererState
    {
        public Renderer Renderer;
        public Material[] Materials;
        public int SortingLayerId;
        public int SortingOrder;
        public SpriteRenderer Sprite;
        public Tilemap Tilemap;
        public Color Color;
        public bool IsProp;
    }

    private struct LineState
    {
        public float Width;
        public Color StartColor;
        public Color EndColor;
    }

    private readonly List<EnvironmentRendererState> environmentStates = new();
    private readonly Dictionary<LineRenderer, LineState> anomalyLines = new();
    private readonly Dictionary<SpriteRenderer, Color> anomalySprites = new();
    private readonly Dictionary<SpriteRenderer, Color> enemySprites = new();

    private GameObject environmentInstance;
    private Material readabilityMaterial;
    private GameObject environmentPrefab;
    private Shader readabilityShader;
    private bool testEnabled;
    private ReadabilityPreset preset = ReadabilityPreset.Original;
    private float propsIntensity = 1f;
    private float anomalyEmphasis = 1f;
    private bool enemyHighlight;
    private float nextRefresh;

    public ReadabilityPreset Preset => preset;
    public float PropsIntensity => propsIntensity;
    public float AnomalyEmphasis => anomalyEmphasis;
    public bool EnemyHighlight => enemyHighlight;
    public int EnvironmentRendererCount => environmentStates.Count;
    public bool TestEnabled => testEnabled;
    public bool CanEnable => environmentPrefab != null;

    public void Configure(GameObject environmentPrefab, Shader readabilityShader)
    {
        this.environmentPrefab = environmentPrefab;
        this.readabilityShader = readabilityShader;
        testEnabled = false;
    }

    public bool SetTestEnabled(bool enabled)
    {
        if (enabled == testEnabled)
            return testEnabled;

        if (!enabled)
        {
            DisableTestAndRestore();
            return false;
        }

        try
        {
            EnsureTestResources();
            if (environmentInstance == null)
                return false;

            testEnabled = true;
            environmentInstance.SetActive(true);
            CaptureEnvironmentRenderers();
            PutEnvironmentBehindGameplay();
            RefreshDynamicTargets();
            ApplyAll();
            return true;
        }
        catch (System.Exception exception)
        {
            DisableTestAndRestore();
            Debug.LogError($"Environment Readability test could not start: {exception.Message}");
            return false;
        }
    }

    public void SetPreset(ReadabilityPreset value)
    {
        if (!testEnabled)
            return;
        preset = value;
        ApplyEnvironment();
    }

    public void SetPropsIntensity(float value)
    {
        if (!testEnabled)
            return;
        propsIntensity = Mathf.Clamp01(value);
        ApplyEnvironment();
    }

    public void SetAnomalyEmphasis(float value)
    {
        if (!testEnabled)
            return;
        anomalyEmphasis = Mathf.Clamp(value, 1f, 1.5f);
        CaptureAnomalyRenderers();
        ApplyAnomalyEmphasis();
    }

    public void SetEnemyHighlight(bool enabled)
    {
        if (!testEnabled)
            return;
        RestoreEnemyColors();
        enemyHighlight = enabled;
        if (enabled)
        {
            CaptureEnemyRenderers();
            ApplyEnemyHighlight();
        }
    }

    public void ResetVisual()
    {
        if (!testEnabled)
            return;
        preset = ReadabilityPreset.Original;
        propsIntensity = 1f;
        anomalyEmphasis = 1f;
        enemyHighlight = false;
        ApplyAll();
    }

    public static string GetPresetName(ReadabilityPreset value) => value switch
    {
        ReadabilityPreset.MutedWorld => "MUTED WORLD",
        ReadabilityPreset.HighGameplayContrast => "HIGH GAMEPLAY CONTRAST",
        ReadabilityPreset.DarkWorld => "DARK WORLD",
        _ => "ORIGINAL"
    };

    private void Update()
    {
        if (!testEnabled)
            return;
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.5f;
        RefreshDynamicTargets();
    }

    private void RefreshDynamicTargets()
    {
        CaptureAnomalyRenderers();
        ApplyAnomalyEmphasis();
        RestoreDestroyedEnemyEntries();
        if (enemyHighlight)
        {
            CaptureEnemyRenderers();
            ApplyEnemyHighlight();
        }
    }

    private void CaptureEnvironmentRenderers()
    {
        environmentStates.Clear();
        if (environmentInstance == null)
            return;

        Renderer[] renderers = environmentInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer is not SpriteRenderer && renderer is not TilemapRenderer)
                continue;
            SpriteRenderer sprite = renderer as SpriteRenderer;
            Tilemap tilemap = renderer.GetComponent<Tilemap>();
            environmentStates.Add(new EnvironmentRendererState
            {
                Renderer = renderer,
                Materials = renderer.sharedMaterials,
                SortingLayerId = renderer.sortingLayerID,
                SortingOrder = renderer.sortingOrder,
                Sprite = sprite,
                Tilemap = tilemap,
                Color = sprite != null ? sprite.color :
                    tilemap != null ? tilemap.color : Color.white,
                IsProp = IsPropsRenderer(renderer.transform)
            });
        }
    }

    private void ApplyAll()
    {
        ApplyEnvironment();
        ApplyAnomalyEmphasis();
        RestoreEnemyColors();
        if (enemyHighlight)
            ApplyEnemyHighlight();
    }

    private void EnsureTestResources()
    {
        if (environmentInstance == null && environmentPrefab != null)
        {
            environmentInstance = Instantiate(environmentPrefab);
            environmentInstance.name = "Sandbox Environment Readability Visual";
            DisableGameplayComponents(environmentInstance);
        }

        if (readabilityMaterial == null && readabilityShader != null)
        {
            readabilityMaterial = new Material(readabilityShader)
            {
                name = "Sandbox Environment Readability (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private void PutEnvironmentBehindGameplay()
    {
        for (int i = 0; i < environmentStates.Count; i++)
        {
            Renderer renderer = environmentStates[i].Renderer;
            if (renderer == null)
                continue;
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -200 + Mathf.Clamp(
                environmentStates[i].SortingOrder,
                -40,
                40
            );
        }
    }

    private void ApplyEnvironment()
    {
        ConfigureReadabilityMaterial();
        float presetPropsMultiplier = preset switch
        {
            ReadabilityPreset.MutedWorld => 0.7f,
            ReadabilityPreset.HighGameplayContrast => 0.55f,
            ReadabilityPreset.DarkWorld => 0.45f,
            _ => 1f
        };

        for (int i = 0; i < environmentStates.Count; i++)
        {
            EnvironmentRendererState state = environmentStates[i];
            if (state.Renderer == null)
                continue;
            if (preset == ReadabilityPreset.Original || readabilityMaterial == null)
                state.Renderer.sharedMaterials = state.Materials;
            else
                AssignReadabilityMaterial(state.Renderer, state.Materials.Length);

            float prominence = state.IsProp
                ? propsIntensity * presetPropsMultiplier
                : 1f;
            Color color = state.Color;
            color.r *= prominence;
            color.g *= prominence;
            color.b *= prominence;
            color.a *= prominence;
            if (state.Sprite != null)
                state.Sprite.color = color;
            else if (state.Tilemap != null)
                state.Tilemap.color = color;
        }
    }

    private void ConfigureReadabilityMaterial()
    {
        if (readabilityMaterial == null)
            return;
        float saturation;
        float brightness;
        float contrast;
        Color tint;
        switch (preset)
        {
            case ReadabilityPreset.MutedWorld:
                saturation = 0.45f;
                brightness = 0.72f;
                contrast = 0.9f;
                tint = new Color(0.84f, 0.91f, 1f, 1f);
                break;
            case ReadabilityPreset.HighGameplayContrast:
                saturation = 0.25f;
                brightness = 0.56f;
                contrast = 0.82f;
                tint = new Color(0.76f, 0.86f, 1f, 1f);
                break;
            case ReadabilityPreset.DarkWorld:
                saturation = 0.08f;
                brightness = 0.32f;
                contrast = 0.72f;
                tint = new Color(0.62f, 0.73f, 0.9f, 1f);
                break;
            default:
                saturation = 1f;
                brightness = 1f;
                contrast = 1f;
                tint = Color.white;
                break;
        }
        readabilityMaterial.SetFloat("_Saturation", saturation);
        readabilityMaterial.SetFloat("_Brightness", brightness);
        readabilityMaterial.SetFloat("_Contrast", contrast);
        readabilityMaterial.SetColor("_ReadabilityTint", tint);
    }

    private void AssignReadabilityMaterial(Renderer renderer, int materialCount)
    {
        int count = Mathf.Max(1, materialCount);
        Material[] materials = new Material[count];
        for (int i = 0; i < count; i++)
            materials[i] = readabilityMaterial;
        renderer.sharedMaterials = materials;
    }

    private void CaptureAnomalyRenderers()
    {
        LineRenderer[] lines = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null || anomalyLines.ContainsKey(line) ||
                !IsAnomalyVisual(line.transform))
                continue;
            anomalyLines.Add(line, new LineState
            {
                Width = line.widthMultiplier,
                StartColor = line.startColor,
                EndColor = line.endColor
            });
        }

        SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null || anomalySprites.ContainsKey(sprite) ||
                !IsAnomalyVisual(sprite.transform))
                continue;
            anomalySprites.Add(sprite, sprite.color);
        }
    }

    private void ApplyAnomalyEmphasis()
    {
        float widthMultiplier = Mathf.Lerp(1f, 1.35f,
            Mathf.InverseLerp(1f, 1.5f, anomalyEmphasis));
        List<LineRenderer> deadLines = null;
        foreach (KeyValuePair<LineRenderer, LineState> pair in anomalyLines)
        {
            if (pair.Key == null)
            {
                deadLines ??= new List<LineRenderer>();
                deadLines.Add(pair.Key);
                continue;
            }
            pair.Key.widthMultiplier = pair.Value.Width * widthMultiplier;
            pair.Key.startColor = BoostColor(pair.Value.StartColor, anomalyEmphasis);
            pair.Key.endColor = BoostColor(pair.Value.EndColor, anomalyEmphasis);
        }
        if (deadLines != null)
            for (int i = 0; i < deadLines.Count; i++) anomalyLines.Remove(deadLines[i]);

        List<SpriteRenderer> deadSprites = null;
        foreach (KeyValuePair<SpriteRenderer, Color> pair in anomalySprites)
        {
            if (pair.Key == null)
            {
                deadSprites ??= new List<SpriteRenderer>();
                deadSprites.Add(pair.Key);
                continue;
            }
            pair.Key.color = BoostColor(pair.Value, anomalyEmphasis);
        }
        if (deadSprites != null)
            for (int i = 0; i < deadSprites.Count; i++) anomalySprites.Remove(deadSprites[i]);
    }

    private void CaptureEnemyRenderers()
    {
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.GetComponent<GoldenEnemyModifier>() != null)
                continue;
            SpriteRenderer[] sprites = enemy.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && !enemySprites.ContainsKey(sprites[i]))
                    enemySprites.Add(sprites[i], sprites[i].color);
            }
        }
    }

    private void ApplyEnemyHighlight()
    {
        List<SpriteRenderer> goldenSprites = null;
        foreach (KeyValuePair<SpriteRenderer, Color> pair in enemySprites)
        {
            if (pair.Key == null)
                continue;
            if (pair.Key.GetComponentInParent<GoldenEnemyModifier>() != null)
            {
                goldenSprites ??= new List<SpriteRenderer>();
                goldenSprites.Add(pair.Key);
                continue;
            }
            Color separated = Color.Lerp(
                pair.Value,
                new Color(0.76f, 0.94f, 1f, pair.Value.a),
                0.14f
            );
            separated.a = pair.Value.a;
            pair.Key.color = separated;
        }
        if (goldenSprites != null)
            for (int i = 0; i < goldenSprites.Count; i++)
                enemySprites.Remove(goldenSprites[i]);
    }

    private void RestoreEnemyColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in enemySprites)
            if (pair.Key != null) pair.Key.color = pair.Value;
        enemySprites.Clear();
    }

    private void RestoreDestroyedEnemyEntries()
    {
        if (enemySprites.Count == 0)
            return;
        List<SpriteRenderer> dead = null;
        foreach (SpriteRenderer sprite in enemySprites.Keys)
        {
            if (sprite != null)
                continue;
            dead ??= new List<SpriteRenderer>();
            dead.Add(sprite);
        }
        if (dead != null)
            for (int i = 0; i < dead.Count; i++) enemySprites.Remove(dead[i]);
    }

    private static bool IsAnomalyVisual(Transform target)
    {
        return target.GetComponentInParent<LocalAnomalyZone>() != null ||
            target.GetComponentInParent<GravityAnomalySiteController>() != null;
    }

    private static bool IsPropsRenderer(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("tree") || name.Contains("plant") ||
                name.Contains("prop") || name.Contains("decor") ||
                name.Contains("shadow"))
                return true;
        }
        return false;
    }

    private static Color BoostColor(Color color, float multiplier)
    {
        color.r *= multiplier;
        color.g *= multiplier;
        color.b *= multiplier;
        color.a = Mathf.Clamp01(color.a * Mathf.Lerp(1f, 1.12f,
            Mathf.InverseLerp(1f, 1.5f, multiplier)));
        return color;
    }

    private static void DisableGameplayComponents(GameObject root)
    {
        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
        Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].simulated = false;
    }

    private void RestoreEnvironment()
    {
        for (int i = 0; i < environmentStates.Count; i++)
        {
            EnvironmentRendererState state = environmentStates[i];
            if (state.Renderer == null)
                continue;
            state.Renderer.sharedMaterials = state.Materials;
            state.Renderer.sortingLayerID = state.SortingLayerId;
            state.Renderer.sortingOrder = state.SortingOrder;
            if (state.Sprite != null)
                state.Sprite.color = state.Color;
            else if (state.Tilemap != null)
                state.Tilemap.color = state.Color;
        }
    }

    private void DisableTestAndRestore()
    {
        RestoreEnvironment();
        RestoreEnemyColors();
        anomalyEmphasis = 1f;
        ApplyAnomalyEmphasis();
        anomalyLines.Clear();
        anomalySprites.Clear();
        preset = ReadabilityPreset.Original;
        propsIntensity = 1f;
        enemyHighlight = false;
        testEnabled = false;
        if (environmentInstance != null)
            environmentInstance.SetActive(false);
    }

    private void OnDestroy()
    {
        DisableTestAndRestore();
        if (readabilityMaterial != null)
            Destroy(readabilityMaterial);
        if (environmentInstance != null)
            Destroy(environmentInstance);
    }
}
#endif
