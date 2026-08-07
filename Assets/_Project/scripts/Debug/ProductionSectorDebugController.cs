using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class ProductionSectorDebugController : MonoBehaviour
{
    public enum ReadabilityPreset
    {
        Original,
        Muted,
        HighGameplayContrast,
        DarkWorld
    }

    public enum EnemyReadability
    {
        Off,
        Light,
        Strong
    }

    public enum EnemyScope
    {
        CurrentZone,
        All
    }

    public enum SpecialOverride
    {
        Random,
        Gravity,
        Electric,
        Beam
    }

    private sealed class EnvironmentState
    {
        public SpriteRenderer Sprite;
        public Tilemap Tilemap;
        public Color Color;
        public bool IsDecor;
    }

    private struct LineState
    {
        public float Width;
        public Color StartColor;
        public Color EndColor;
    }

    private static ReadabilityPreset readabilityPreset =
        ReadabilityPreset.Original;
    private static float decorBrightness = 1f;
    private static float anomalyAccent = 1f;
    private static EnemyReadability enemyReadability = EnemyReadability.Off;
    private static EnemyScope enemyScope = EnemyScope.CurrentZone;
    private static SpecialOverride specialOverride = SpecialOverride.Random;
    private static bool invulnerability;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionDefaults()
    {
        readabilityPreset = ReadabilityPreset.Original;
        decorBrightness = 1f;
        anomalyAccent = 1f;
        enemyReadability = EnemyReadability.Off;
        enemyScope = EnemyScope.CurrentZone;
        specialOverride = SpecialOverride.Random;
        invulnerability = false;
    }

    private readonly List<EnvironmentState> environmentStates = new();
    private readonly Dictionary<LineRenderer, LineState> anomalyLines = new();
    private readonly Dictionary<SpriteRenderer, Color> enemySprites = new();

    private ProductionAnomalySite currentSite;
    private PlayerHealth protectedPlayer;
    private float protectedPlayerMultiplier = 1f;
    private bool playerMultiplierCaptured;
    private SpriteRenderer groundOverlay;
    private Texture2D overlayTexture;
    private Sprite overlaySprite;
    private float nextRefresh;

    public ReadabilityPreset Preset => readabilityPreset;
    public float DecorBrightness => decorBrightness;
    public float AnomalyAccent => anomalyAccent;
    public EnemyReadability EnemyMode => enemyReadability;
    public EnemyScope CurrentEnemyScope => enemyScope;
    public SpecialOverride Override => specialOverride;
    public bool InvulnerabilityEnabled => invulnerability;
    public ProductionAnomalySite CurrentSite => currentSite;
    public int EnvironmentRendererCount => environmentStates.Count;

    public string CurrentZoneName => currentSite != null
        ? currentSite.DebugZoneName
        : "NONE";

    public string CurrentSpecialName
    {
        get
        {
            IReadOnlyList<ProductionAnomalySite> sites =
                ProductionAnomalySite.ActiveSites;
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i] != null && sites[i].IsSpecial)
                    return sites[i].DebugZoneName.Replace("SPECIAL ", "");
            }

            return "NONE";
        }
    }

    public int CurrentSectorNumber
    {
        get
        {
            RunSector sector = RunStateManager.Instance != null
                ? RunStateManager.Instance.CurrentSector
                : null;
            return sector != null ? sector.SectorNumber : 0;
        }
    }

    public void SetInvulnerability(bool enabled)
    {
        invulnerability = enabled;
        ApplyInvulnerability();
    }

    public void SetPreset(ReadabilityPreset value)
    {
        readabilityPreset = value;
        ApplyEnvironment();
    }

    public void SetDecorBrightness(float value)
    {
        decorBrightness = Mathf.Clamp(value, 0.25f, 1f);
        ApplyEnvironment();
    }

    public void SetAnomalyAccent(float value)
    {
        anomalyAccent = Mathf.Clamp(value, 1f, 1.75f);
        ApplyAnomalyAccent();
    }

    public void SetEnemyReadability(EnemyReadability value)
    {
        enemyReadability = value;
        RefreshEnemies();
    }

    public void SetEnemyScope(EnemyScope value)
    {
        enemyScope = value;
        RefreshEnemies();
    }

    public void SetSpecialOverride(SpecialOverride value)
    {
        specialOverride = value;
    }

    public void ResetVisualSettings()
    {
        readabilityPreset = ReadabilityPreset.Original;
        decorBrightness = 1f;
        anomalyAccent = 1f;
        enemyReadability = EnemyReadability.Off;
        ApplyEnvironment();
        ApplyAnomalyAccent();
        RefreshEnemies();
    }

    public void RebuildCurrentSector()
    {
        RestoreAll();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static bool TryGetSpecialOverride(out AnomalyPowerType power)
    {
        switch (specialOverride)
        {
            case SpecialOverride.Gravity:
                power = AnomalyPowerType.GravityOrb;
                return true;
            case SpecialOverride.Electric:
                power = AnomalyPowerType.ArcNode;
                return true;
            case SpecialOverride.Beam:
                power = AnomalyPowerType.RedBeam;
                return true;
            default:
                power = default;
                return false;
        }
    }

    private void OnEnable()
    {
        RefreshCurrentSite(true);
        ApplyInvulnerability();
    }

    private void Update()
    {
        ApplyInvulnerability();

        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.35f;
        RefreshCurrentSite(false);
        RefreshEnemies();
    }

    private void RefreshCurrentSite(bool force)
    {
        Transform player = ResolvePlayerTransform();
        ProductionAnomalySite next = null;

        if (player != null)
        {
            IReadOnlyList<ProductionAnomalySite> sites =
                ProductionAnomalySite.ActiveSites;
            Vector2 position = player.position;

            for (int i = 0; i < sites.Count; i++)
            {
                ProductionAnomalySite site = sites[i];
                if (site != null && site.ContainsWorldPosition(position))
                {
                    next = site;
                    break;
                }
            }
        }

        if (!force && next == currentSite)
            return;

        RestoreEnvironment();
        RestoreAnomalyAccent();
        currentSite = next;
        CaptureEnvironment();
        CaptureAnomalyVisuals();
        ApplyEnvironment();
        ApplyAnomalyAccent();
    }

    private Transform ResolvePlayerTransform()
    {
        if (protectedPlayer != null)
            return protectedPlayer.transform;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void ApplyInvulnerability()
    {
        if (protectedPlayer == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            protectedPlayer = player != null
                ? player.GetComponent<PlayerHealth>()
                : null;
            playerMultiplierCaptured = false;
        }

        if (protectedPlayer == null)
            return;

        if (invulnerability)
        {
            if (!playerMultiplierCaptured)
            {
                protectedPlayerMultiplier =
                    protectedPlayer.IncomingDamageMultiplier;
                playerMultiplierCaptured = true;
            }

            protectedPlayer.SetIncomingDamageMultiplier(0f);
        }
        else
        {
            RestorePlayerMultiplier();
        }
    }

    private void RestorePlayerMultiplier()
    {
        if (protectedPlayer != null && playerMultiplierCaptured)
        {
            protectedPlayer.SetIncomingDamageMultiplier(
                protectedPlayerMultiplier
            );
        }

        playerMultiplierCaptured = false;
    }

    private void CaptureEnvironment()
    {
        if (currentSite == null)
            return;

        SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>(
            FindObjectsSortMode.None
        );

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null || !IsEnvironmentRenderer(sprite.transform) ||
                !currentSite.ContainsWorldPosition(sprite.bounds.center))
            {
                continue;
            }

            environmentStates.Add(new EnvironmentState
            {
                Sprite = sprite,
                Color = sprite.color,
                IsDecor = IsDecorRenderer(sprite.transform)
            });
        }

        TilemapRenderer[] tileRenderers = FindObjectsByType<TilemapRenderer>(
            FindObjectsSortMode.None
        );

        for (int i = 0; i < tileRenderers.Length; i++)
        {
            TilemapRenderer renderer = tileRenderers[i];
            Tilemap tilemap = renderer != null
                ? renderer.GetComponent<Tilemap>()
                : null;

            if (tilemap == null ||
                !IsEnvironmentRenderer(renderer.transform) ||
                !RendererFitsCurrentSite(renderer))
            {
                continue;
            }

            environmentStates.Add(new EnvironmentState
            {
                Tilemap = tilemap,
                Color = tilemap.color,
                IsDecor = IsDecorRenderer(renderer.transform)
            });
        }
    }

    private bool RendererFitsCurrentSite(Renderer renderer)
    {
        if (currentSite == null || renderer == null ||
            !currentSite.ContainsWorldPosition(renderer.bounds.center))
        {
            return false;
        }

        Vector2 size = renderer.bounds.size;
        Vector2 siteSize = currentSite.SiteSize;
        return size.x <= siteSize.x * 1.1f &&
            size.y <= siteSize.y * 1.1f;
    }

    private void ApplyEnvironment()
    {
        for (int i = 0; i < environmentStates.Count; i++)
        {
            EnvironmentState state = environmentStates[i];
            Color color = TransformEnvironmentColor(state.Color);
            float decor = state.IsDecor ? decorBrightness : 1f;
            color.r *= decor;
            color.g *= decor;
            color.b *= decor;
            color.a = state.Color.a;

            if (state.Sprite != null)
                state.Sprite.color = color;
            else if (state.Tilemap != null)
                state.Tilemap.color = color;
        }

        ApplyGroundOverlay();
    }

    private static Color TransformEnvironmentColor(Color source)
    {
        float saturation;
        float brightness;
        float contrast;
        Color tint;

        switch (readabilityPreset)
        {
            case ReadabilityPreset.Muted:
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
                return source;
        }

        float luminance = source.r * 0.299f + source.g * 0.587f +
            source.b * 0.114f;
        Color gray = new(luminance, luminance, luminance, source.a);
        Color result = Color.Lerp(gray, source, saturation);
        result.r = ((result.r - 0.5f) * contrast + 0.5f) *
            brightness * tint.r;
        result.g = ((result.g - 0.5f) * contrast + 0.5f) *
            brightness * tint.g;
        result.b = ((result.b - 0.5f) * contrast + 0.5f) *
            brightness * tint.b;
        result.a = source.a;
        return result;
    }

    private void ApplyGroundOverlay()
    {
        if (currentSite == null || readabilityPreset == ReadabilityPreset.Original)
        {
            if (groundOverlay != null)
                groundOverlay.enabled = false;
            return;
        }

        EnsureGroundOverlay();
        groundOverlay.transform.position = new Vector3(
            currentSite.transform.position.x,
            currentSite.transform.position.y,
            0f
        );
        groundOverlay.transform.localScale = currentSite.SiteSize;
        groundOverlay.color = readabilityPreset switch
        {
            ReadabilityPreset.Muted => new Color(0.05f, 0.1f, 0.15f, 0.18f),
            ReadabilityPreset.HighGameplayContrast =>
                new Color(0.025f, 0.07f, 0.12f, 0.34f),
            _ => new Color(0.01f, 0.025f, 0.055f, 0.54f)
        };
        groundOverlay.enabled = true;
    }

    private void EnsureGroundOverlay()
    {
        if (groundOverlay != null)
            return;

        overlayTexture = new Texture2D(1, 1)
        {
            name = "Production Sector Debug Ground Pixel",
            hideFlags = HideFlags.HideAndDontSave
        };
        overlayTexture.SetPixel(0, 0, Color.white);
        overlayTexture.Apply();
        overlaySprite = Sprite.Create(
            overlayTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        overlaySprite.name = "Production Sector Debug Ground Sprite";

        GameObject overlay = new("Production Sector Debug Ground Overlay");
        overlay.transform.SetParent(transform, false);
        groundOverlay = overlay.AddComponent<SpriteRenderer>();
        groundOverlay.sprite = overlaySprite;
        groundOverlay.sortingLayerName = "Background";
        groundOverlay.sortingOrder = -8;
    }

    private void RestoreEnvironment()
    {
        for (int i = 0; i < environmentStates.Count; i++)
        {
            EnvironmentState state = environmentStates[i];
            if (state.Sprite != null)
                state.Sprite.color = state.Color;
            else if (state.Tilemap != null)
                state.Tilemap.color = state.Color;
        }

        environmentStates.Clear();
        if (groundOverlay != null)
            groundOverlay.enabled = false;
    }

    private void CaptureAnomalyVisuals()
    {
        if (currentSite == null)
            return;

        CaptureLines(currentSite.transform);
        if (currentSite.AnomalyZone != null)
            CaptureLines(currentSite.AnomalyZone.transform);
    }

    private void CaptureLines(Transform root)
    {
        LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null || anomalyLines.ContainsKey(line))
                continue;

            anomalyLines.Add(line, new LineState
            {
                Width = line.widthMultiplier,
                StartColor = line.startColor,
                EndColor = line.endColor
            });
        }
    }

    private void ApplyAnomalyAccent()
    {
        float t = Mathf.InverseLerp(1f, 1.75f, anomalyAccent);
        float width = Mathf.Lerp(1f, 1.35f, t);

        foreach (KeyValuePair<LineRenderer, LineState> pair in anomalyLines)
        {
            if (pair.Key == null)
                continue;

            pair.Key.widthMultiplier = pair.Value.Width * width;
            pair.Key.startColor = BoostColor(pair.Value.StartColor, anomalyAccent);
            pair.Key.endColor = BoostColor(pair.Value.EndColor, anomalyAccent);
        }

        currentSite?.SetDebugVisualEmphasis(anomalyAccent);
    }

    private void RestoreAnomalyAccent()
    {
        foreach (KeyValuePair<LineRenderer, LineState> pair in anomalyLines)
        {
            if (pair.Key == null)
                continue;

            pair.Key.widthMultiplier = pair.Value.Width;
            pair.Key.startColor = pair.Value.StartColor;
            pair.Key.endColor = pair.Value.EndColor;
        }

        currentSite?.SetDebugVisualEmphasis(1f);
        anomalyLines.Clear();
    }

    private static Color BoostColor(Color source, float multiplier)
    {
        Color result = source;
        result.r = Mathf.Clamp01(result.r * multiplier);
        result.g = Mathf.Clamp01(result.g * multiplier);
        result.b = Mathf.Clamp01(result.b * multiplier);
        result.a = Mathf.Clamp01(source.a * Mathf.Lerp(
            1f,
            1.15f,
            Mathf.InverseLerp(1f, 1.75f, multiplier)
        ));
        return result;
    }

    private void RefreshEnemies()
    {
        RestoreEnemyColors();
        if (enemyReadability == EnemyReadability.Off)
            return;

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead ||
                (enemyScope == EnemyScope.CurrentZone &&
                 (currentSite == null ||
                  !currentSite.ContainsWorldPosition(enemy.transform.position))))
            {
                continue;
            }

            SpriteRenderer[] sprites =
                enemy.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null)
                    continue;

                Color original = sprite.color;
                enemySprites.Add(sprite, original);
                float blend = enemyReadability == EnemyReadability.Light
                    ? 0.08f
                    : 0.17f;
                float brightness = enemyReadability == EnemyReadability.Light
                    ? 1.08f
                    : 1.2f;
                Color lifted = new(
                    Mathf.Clamp01(original.r * brightness),
                    Mathf.Clamp01(original.g * brightness),
                    Mathf.Clamp01(original.b * brightness),
                    original.a
                );
                Color readable = Color.Lerp(
                    lifted,
                    new Color(0.78f, 0.94f, 1f, original.a),
                    blend
                );
                readable.a = original.a;
                sprite.color = readable;
            }
        }
    }

    private void RestoreEnemyColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in enemySprites)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }

        enemySprites.Clear();
    }

    private static bool IsEnvironmentRenderer(Transform target)
    {
        if (target == null || target.GetComponentInParent<Canvas>() != null ||
            target.GetComponentInParent<PlayerHealth>() != null ||
            target.GetComponentInParent<EnemyHealth>() != null ||
            target.GetComponentInParent<LocalAnomalyZone>() != null ||
            target.GetComponentInParent<ProductionAnomalySite>() != null ||
            target.GetComponentInParent<ProductionSectorExit>() != null)
        {
            return false;
        }

        for (Transform current = target; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("backgrnd") || name.Contains("background") ||
                name.Contains("envir") || name.Contains("plant") ||
                name.Contains("grass") || name.Contains("tree") ||
                name.Contains("vegetation") || name.Contains("decor") ||
                name.Contains("prop") || name.Contains("clutter") ||
                name == "grnd" || name.Contains("ground"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDecorRenderer(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("plant") || name.Contains("grass") ||
                name.Contains("tree") || name.Contains("vegetation") ||
                name.Contains("decor") || name.Contains("prop") ||
                name.Contains("clutter") || name.Contains("bush") ||
                name.Contains("shadow"))
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreAll()
    {
        RestorePlayerMultiplier();
        RestoreEnvironment();
        RestoreAnomalyAccent();
        RestoreEnemyColors();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
        if (overlaySprite != null)
            Destroy(overlaySprite);
        if (overlayTexture != null)
            Destroy(overlayTexture);
    }
}
#endif
