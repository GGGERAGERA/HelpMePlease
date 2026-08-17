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
        Low,
        Medium,
        High
    }

    public enum EnemyScope
    {
        CurrentZone,
        All,
        Basic,
        Elite,
        Shooter,
        Bomber,
        Boss
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

    private enum EnemyCategory
    {
        Basic,
        Elite,
        Shooter,
        Bomber,
        Boss
    }

    private sealed class EnemyRendererState
    {
        public SpriteRenderer Renderer;
        public Material OriginalMaterial;
    }

    private sealed class EnemyVisualState
    {
        public EnemyHealth Enemy;
        public EnemyCategory Category;
        public EnemyWhiteFlash WhiteFlash;
        public readonly List<EnemyRendererState> Renderers = new();
    }

    private static readonly int SaturationId =
        Shader.PropertyToID("_ReadabilitySaturation");
    private static readonly int BrightnessId =
        Shader.PropertyToID("_ReadabilityBrightness");
    private static readonly int TintId =
        Shader.PropertyToID("_ReadabilityTint");
    private static readonly int TintStrengthId =
        Shader.PropertyToID("_ReadabilityTintStrength");
    private static readonly int OutlineColorId =
        Shader.PropertyToID("_ReadabilityOutlineColor");
    private static readonly int OutlineStrengthId =
        Shader.PropertyToID("_ReadabilityOutlineStrength");
    private static readonly int OutlineWidthId =
        Shader.PropertyToID("_ReadabilityOutlineWidth");

    private static readonly Color EnemyTint =
        new(0.05f, 1f, 1f, 1f);
    private static readonly Color EnemyOutlineColor =
        new(0.025f, 0.055f, 0.09f, 1f);

    private static ReadabilityPreset readabilityPreset =
        ReadabilityPreset.Original;
    private static float decorBrightness = 1f;
    private static float anomalyAccent = 1f;
    private static EnemyReadability enemyReadability = EnemyReadability.High;
    private static EnemyScope enemyScope = EnemyScope.All;
    private static float enemySaturation = 1.9f;
    private static float enemyBrightness = 1.45f;
    private static float enemyTintStrength = 0.5f;
    private static float enemyOutlineStrength;
    private static float enemyOutlineWidth = 1f;
    private static bool enemyOutlineEnabled;
    private static SpecialOverride specialOverride = SpecialOverride.Random;
    private static bool invulnerability;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionDefaults()
    {
        readabilityPreset = ReadabilityPreset.Original;
        decorBrightness = 1f;
        anomalyAccent = 1f;
        enemyReadability = EnemyReadability.High;
        enemyScope = EnemyScope.All;
        enemySaturation = 1.9f;
        enemyBrightness = 1.45f;
        enemyTintStrength = 0.5f;
        enemyOutlineStrength = 0f;
        enemyOutlineWidth = 1f;
        enemyOutlineEnabled = false;
        specialOverride = SpecialOverride.Random;
        invulnerability = false;
        EnemyDebugAiFreeze.SetFrozen(false);
    }

    private readonly List<EnvironmentState> environmentStates = new();
    private readonly Dictionary<LineRenderer, LineState> anomalyLines = new();
    private readonly Dictionary<EnemyHealth, EnemyVisualState> enemyVisuals =
        new();
    private MaterialPropertyBlock readabilityProperties;
    private readonly HashSet<Transform> decorObjects = new();
    private readonly List<ProductionAnomalySite> visualTunerSites = new();
    private readonly Dictionary<
        ProductionAnomalySite,
        AnomalyVisualTuningValues> monochromeOriginalValues = new();

    private ProductionAnomalySite currentSite;
    private int visualTunerIndex = -1;
    private PlayerHealth protectedPlayer;
    private float protectedPlayerMultiplier = 1f;
    private bool playerMultiplierCaptured;
    private SpriteRenderer groundOverlay;
    private Texture2D overlayTexture;
    private Sprite overlaySprite;
    private Material enemyReadabilityMaterial;
    private float nextRefresh;
    private bool monochromeAnomalies;
    private bool sessionDefaultsCaptured;
    private ReadabilityPreset defaultReadabilityPreset;
    private float defaultDecorBrightness;
    private float defaultAnomalyAccent;
    private EnemyReadability defaultEnemyReadability;
    private EnemyScope defaultEnemyScope;
    private float defaultEnemySaturation;
    private float defaultEnemyBrightness;
    private float defaultEnemyTintStrength;
    private float defaultEnemyOutlineStrength;
    private float defaultEnemyOutlineWidth;
    private bool defaultEnemyOutlineEnabled;

    public ReadabilityPreset Preset => readabilityPreset;
    public float DecorBrightness => decorBrightness;
    public float AnomalyAccent => anomalyAccent;
    public EnemyReadability EnemyMode => enemyReadability;
    public EnemyScope CurrentEnemyScope => enemyScope;
    public float EnemySaturation => enemySaturation;
    public float EnemyBrightness => enemyBrightness;
    public float EnemyTintStrength => enemyTintStrength;
    public float EnemyOutlineStrength => enemyOutlineStrength;
    public float EnemyOutlineWidth => enemyOutlineWidth;
    public bool EnemyOutlineEnabled => enemyOutlineEnabled;
    public int RegisteredEnemyCount => enemyVisuals.Count;
    public int RegisteredEnemyRendererCount
    {
        get
        {
            int count = 0;
            foreach (EnemyVisualState state in enemyVisuals.Values)
                count += state.Renderers.Count;
            return count;
        }
    }
    public int AffectedEnemyCount
    {
        get
        {
            if (enemyReadability == EnemyReadability.Off ||
                enemyReadabilityMaterial == null)
                return 0;

            int count = 0;
            foreach (EnemyVisualState state in enemyVisuals.Values)
            {
                if (state.Enemy != null && MatchesEnemyScope(state))
                    count++;
            }
            return count;
        }
    }
    public int AffectedEnemyRendererCount
    {
        get
        {
            if (enemyReadability == EnemyReadability.Off ||
                enemyReadabilityMaterial == null)
                return 0;

            int count = 0;
            foreach (EnemyVisualState state in enemyVisuals.Values)
            {
                if (state.Enemy != null && MatchesEnemyScope(state))
                    count += state.Renderers.Count;
            }
            return count;
        }
    }
    public int ActiveReadabilityMaterialRendererCount
    {
        get
        {
            if (enemyReadabilityMaterial == null)
                return 0;

            int count = 0;
            foreach (EnemyVisualState state in enemyVisuals.Values)
            {
                for (int i = 0; i < state.Renderers.Count; i++)
                {
                    SpriteRenderer renderer = state.Renderers[i].Renderer;
                    if (renderer != null &&
                        renderer.sharedMaterial == enemyReadabilityMaterial)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
    public bool EnemyReadabilityMaterialReady => enemyReadabilityMaterial != null;
    public int DecorObjectCount { get; private set; }
    public int DecorRendererCount { get; private set; }
    public int AnomalyZoneCount { get; private set; }
    public int AnomalyRendererCount => anomalyLines.Count;
    public SpecialOverride Override => specialOverride;
    public bool InvulnerabilityEnabled => invulnerability;
    public bool EnemyAiFrozen => EnemyDebugAiFreeze.IsFrozen;
    public ProductionAnomalySite CurrentSite => currentSite;
    public int EnvironmentRendererCount => environmentStates.Count;

    public ProductionAnomalySite VisualTunerTarget
    {
        get
        {
            return visualTunerIndex >= 0 &&
                visualTunerIndex < visualTunerSites.Count
                    ? visualTunerSites[visualTunerIndex]
                    : null;
        }
    }

    public int VisualTunerTargetCount => visualTunerSites.Count;
    public int VisualTunerTargetIndex => visualTunerIndex;
    public string VisualTunerTargetName => VisualTunerTarget != null
        ? VisualTunerTarget.DebugZoneName
        : "NO ACTIVE ANOMALY";
    public string VisualTunerTypeName => VisualTunerTarget != null
        ? VisualTunerTarget.VisualTunerTypeName
        : "NONE";
    public AnomalyVisualTuningCapabilities VisualTunerCapabilities =>
        VisualTunerTarget != null
            ? VisualTunerTarget.VisualTunerCapabilities
            : AnomalyVisualTuningCapabilities.None;
    public AnomalyVisualTuningValues VisualTunerValues =>
        VisualTunerTarget != null
            ? VisualTunerTarget.VisualTunerValues
            : default;
    public float VisualTunerDistance
    {
        get
        {
            Transform player = ResolvePlayerTransform();
            return VisualTunerTarget != null && player != null
                ? Vector2.Distance(
                    player.position,
                    VisualTunerTarget.transform.position
                )
                : -1f;
        }
    }
    public bool MonochromeAnomaliesEnabled => monochromeAnomalies;
    public int VisualTunerArtHookRootCount => VisualTunerTarget != null
        ? VisualTunerTarget.ArtHookRootCount
        : 0;
    public int VisualTunerInstantiatedArtCount => VisualTunerTarget != null
        ? VisualTunerTarget.InstantiatedArtHookCount
        : 0;
    public bool VisualTunerArtHooksVisible => VisualTunerTarget == null ||
        VisualTunerTarget.ArtHooksVisible;

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

    public int ProductionSectorCount => RunRoute.ExplorationSectorCount;

    public float InternalPressure
    {
        get
        {
            RunStateManager runState = RunStateManager.Instance;
            return runState != null ? runState.ThreatValue : 0f;
        }
    }

    public ThreatTier CurrentThreatTier =>
        ThreatTierPresentation.FromPressure(InternalPressure);

    public void SetThreatTier(ThreatTier tier)
    {
        RunStateManager runState = RunStateManager.Instance;

        if (runState == null)
            return;

        float pressure = tier switch
        {
            ThreatTier.Tier2 => ThreatTierPresentation.Tier2Minimum,
            ThreatTier.Tier3 => ThreatTierPresentation.Tier3Minimum,
            ThreatTier.Tier4 => ThreatTierPresentation.Tier4Minimum,
            _ => 0f
        };

        runState.SetThreatForDebug(pressure);
    }

    public void SetInvulnerability(bool enabled)
    {
        invulnerability = enabled;
        ApplyInvulnerability();
    }

    public void SetEnemyAiFrozen(bool frozen)
    {
        EnemyDebugAiFreeze.SetFrozen(frozen);
    }

    public void SetPreset(ReadabilityPreset value)
    {
        readabilityPreset = value;
        ApplyEnvironment();
    }

    public void SetDecorBrightness(float value)
    {
        decorBrightness = Mathf.Clamp(value, 0.25f, 1.5f);
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
        ApplyEnemyPreset(value);
        ApplyAllEnemies();
    }

    public void SetEnemyScope(EnemyScope value)
    {
        enemyScope = value;
        ApplyAllEnemies();
    }

    public void SetEnemySaturation(float value)
    {
        enemySaturation = Mathf.Clamp(value, 0f, 3f);
        ApplyAllEnemies();
    }

    public void SetEnemyBrightness(float value)
    {
        enemyBrightness = Mathf.Clamp(value, 0.5f, 2.5f);
        ApplyAllEnemies();
    }

    public void SetEnemyTintStrength(float value)
    {
        enemyTintStrength = Mathf.Clamp01(value);
        ApplyAllEnemies();
    }

    public void SetEnemyOutlineStrength(float value)
    {
        enemyOutlineStrength = Mathf.Clamp(value, 0f, 2f);
        ApplyAllEnemies();
    }

    public void SetEnemyOutlineWidth(float value)
    {
        enemyOutlineWidth = Mathf.Clamp(value, 0.5f, 4f);
        ApplyAllEnemies();
    }

    public void RefreshVisualTargets()
    {
        RefreshVisualRegistries();
        RefreshVisualTunerTargets(true);
    }

    public void RefreshVisualTunerTargetCache()
    {
        RefreshVisualTunerTargets(false);
    }

    public void SelectPreviousVisualTunerTarget()
    {
        RefreshVisualTunerTargets(false);

        if (visualTunerSites.Count == 0)
            return;

        visualTunerIndex = (visualTunerIndex - 1 + visualTunerSites.Count) %
            visualTunerSites.Count;
    }

    public void SelectNextVisualTunerTarget()
    {
        RefreshVisualTunerTargets(false);

        if (visualTunerSites.Count == 0)
            return;

        visualTunerIndex = (visualTunerIndex + 1) % visualTunerSites.Count;
    }

    public void ApplyVisualTunerValues(AnomalyVisualTuningValues values)
    {
        if (monochromeAnomalies && VisualTunerTarget != null)
            values = BuildMonochromeValues(
                values,
                VisualTunerTarget.VisualTunerCapabilities);

        VisualTunerTarget?.ApplyVisualTunerValues(values);
    }

    public void ResetVisualTuner()
    {
        if (monochromeAnomalies)
            SetMonochromeAnomalies(false);

        VisualTunerTarget?.ResetVisualTuner();
    }

    public void SetMonochromeAnomalies(bool enabled)
    {
        if (enabled == monochromeAnomalies)
            return;

        if (!enabled)
        {
            RestoreMonochromeAnomalies();
            return;
        }

        monochromeAnomalies = true;
        monochromeOriginalValues.Clear();
        MaintainMonochromeAnomalies();
    }

    public void SetVisualTunerArtHooksVisible(bool visible)
    {
        VisualTunerTarget?.SetArtHooksVisible(visible);
    }

    public void ApplyVisualTunerPreset(string preset)
    {
        VisualTunerTarget?.ApplyVisualTunerPreset(preset);
    }

    public void CopyVisualTunerValues()
    {
        ProductionAnomalySite target = VisualTunerTarget;

        if (target == null)
        {
            Debug.Log("[AnomalyVisualTuner] No active anomaly target.");
            return;
        }

        string values = target.GetVisualTunerValuesText();
        GUIUtility.systemCopyBuffer = values;
        Debug.Log($"[AnomalyVisualTuner]\n{values}", target);
    }

    public void SetEnemyOutlineEnabled(bool enabled)
    {
        enemyOutlineEnabled = enabled;
        ApplyAllEnemies();
    }

    public void SetSpecialOverride(SpecialOverride value)
    {
        specialOverride = value;
    }

    public void ResetVisualSettings()
    {
        ResetWorldVisualSettings();
        ResetEnemyVisualSettings();
        ResetAnomalyVisualSettings();
    }

    public void ResetVisualTestSettings()
    {
        ResetEnemyVisualSettings();
    }

    public void ResetWorldVisualSettings()
    {
        CaptureSessionDefaults();
        readabilityPreset = defaultReadabilityPreset;
        decorBrightness = defaultDecorBrightness;
        ApplyEnvironment();
    }

    public void ResetEnemyVisualSettings()
    {
        CaptureSessionDefaults();
        enemyReadability = defaultEnemyReadability;
        enemyScope = defaultEnemyScope;
        enemySaturation = defaultEnemySaturation;
        enemyBrightness = defaultEnemyBrightness;
        enemyTintStrength = defaultEnemyTintStrength;
        enemyOutlineStrength = defaultEnemyOutlineStrength;
        enemyOutlineWidth = defaultEnemyOutlineWidth;
        enemyOutlineEnabled = defaultEnemyOutlineEnabled;
        ApplyAllEnemies();
    }

    public void ResetAnomalyVisualSettings()
    {
        CaptureSessionDefaults();
        anomalyAccent = defaultAnomalyAccent;
        ApplyAnomalyAccent();

        if (monochromeAnomalies)
            SetMonochromeAnomalies(false);

        IReadOnlyList<ProductionAnomalySite> sites =
            ProductionAnomalySite.ActiveSites;
        for (int i = 0; i < sites.Count; i++)
            sites[i]?.ResetVisualTuner();
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
        CaptureSessionDefaults();
        readabilityProperties ??= new MaterialPropertyBlock();
        EnemyHealth.Spawned += RegisterEnemy;
        EnemyHealth.Despawned += UnregisterEnemy;
        ProductionAnomalySite.VisualTargetsChanged +=
            HandleVisualTargetsChanged;
        EnsureEnemyReadabilityMaterial();
        RefreshCurrentSite(true);
        RefreshVisualRegistries();
        RefreshVisualTunerTargets(true);
        RegisterActiveEnemies();
        ApplyAllEnemies();
        ApplyInvulnerability();
    }

    private void CaptureSessionDefaults()
    {
        if (sessionDefaultsCaptured)
            return;

        sessionDefaultsCaptured = true;
        defaultReadabilityPreset = readabilityPreset;
        defaultDecorBrightness = decorBrightness;
        defaultAnomalyAccent = anomalyAccent;
        defaultEnemyReadability = enemyReadability;
        defaultEnemyScope = enemyScope;
        defaultEnemySaturation = enemySaturation;
        defaultEnemyBrightness = enemyBrightness;
        defaultEnemyTintStrength = enemyTintStrength;
        defaultEnemyOutlineStrength = enemyOutlineStrength;
        defaultEnemyOutlineWidth = enemyOutlineWidth;
        defaultEnemyOutlineEnabled = enemyOutlineEnabled;
    }

    private void HandleVisualTargetsChanged()
    {
        RefreshVisualTunerTargets(false);
    }

    private void Update()
    {
        ApplyInvulnerability();

        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.35f;
        RefreshCurrentSite(false);
        ValidateVisualTunerTarget();
        MaintainMonochromeAnomalies();

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

        currentSite = next;

        if (enemyScope == EnemyScope.CurrentZone)
            ApplyAllEnemies();
    }

    private void RefreshVisualRegistries()
    {
        RestoreEnvironment();
        RestoreAnomalyAccent();
        CaptureEnvironment();
        CaptureAnomalyVisuals();
        ApplyEnvironment();
        ApplyAnomalyAccent();
    }

    private void RefreshVisualTunerTargets(bool selectNearest)
    {
        ProductionAnomalySite previous = VisualTunerTarget;
        visualTunerSites.Clear();
        IReadOnlyList<ProductionAnomalySite> sites =
            ProductionAnomalySite.ActiveSites;

        for (int i = 0; i < sites.Count; i++)
        {
            ProductionAnomalySite site = sites[i];

            if (site != null && site.IsMapVisible)
                visualTunerSites.Add(site);
        }

        if (visualTunerSites.Count == 0)
        {
            visualTunerIndex = -1;
            return;
        }

        int previousIndex = visualTunerSites.IndexOf(previous);

        if (!selectNearest && previousIndex >= 0)
        {
            visualTunerIndex = previousIndex;
            return;
        }

        Transform player = ResolvePlayerTransform();
        visualTunerIndex = 0;

        if (player == null)
            return;

        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < visualTunerSites.Count; i++)
        {
            float distance = Vector2.SqrMagnitude(
                (Vector2)player.position -
                (Vector2)visualTunerSites[i].transform.position
            );

            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            visualTunerIndex = i;
        }
    }

    private void ValidateVisualTunerTarget()
    {
        ProductionAnomalySite target = VisualTunerTarget;

        if (target == null || !target.IsMapVisible)
            RefreshVisualTunerTargets(true);
    }

    private void MaintainMonochromeAnomalies()
    {
        if (!monochromeAnomalies)
            return;

        IReadOnlyList<ProductionAnomalySite> sites =
            ProductionAnomalySite.ActiveSites;
        for (int i = 0; i < sites.Count; i++)
        {
            ProductionAnomalySite site = sites[i];
            if (site == null || !site.IsMapVisible || !site.HasVisualTuner ||
                monochromeOriginalValues.ContainsKey(site))
            {
                continue;
            }

            AnomalyVisualTuningValues original = site.VisualTunerValues;
            monochromeOriginalValues.Add(site, original);
            site.ApplyVisualTunerValues(BuildMonochromeValues(
                original,
                site.VisualTunerCapabilities));
        }
    }

    private void RestoreMonochromeAnomalies()
    {
        foreach (KeyValuePair<
            ProductionAnomalySite,
            AnomalyVisualTuningValues> pair in monochromeOriginalValues)
        {
            pair.Key?.ApplyVisualTunerValues(pair.Value);
        }

        monochromeOriginalValues.Clear();
        monochromeAnomalies = false;
    }

    private static AnomalyVisualTuningValues BuildMonochromeValues(
        AnomalyVisualTuningValues values,
        AnomalyVisualTuningCapabilities capabilities)
    {
        Color neutral = new(0.72f, 0.76f, 0.78f, 1f);
        Color neutralFill = new(0.16f, 0.18f, 0.19f, 1f);

        if ((capabilities & AnomalyVisualTuningCapabilities.PrimaryColor) != 0)
        {
            neutral.a = values.PrimaryColor.a;
            values.PrimaryColor = neutral;
        }

        if ((capabilities & AnomalyVisualTuningCapabilities.SecondaryColor) != 0)
        {
            neutral.a = values.SecondaryColor.a;
            values.SecondaryColor = neutral;
        }

        if ((capabilities & AnomalyVisualTuningCapabilities.FillColor) != 0)
        {
            neutralFill.a = values.FillColor.a;
            values.FillColor = neutralFill;
        }

        return values;
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
        SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>(
            FindObjectsSortMode.None
        );

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null || !IsEnvironmentRenderer(sprite.transform))
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
                !IsEnvironmentRenderer(renderer.transform))
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

        decorObjects.Clear();
        DecorRendererCount = 0;
        for (int i = 0; i < environmentStates.Count; i++)
        {
            EnvironmentState state = environmentStates[i];
            if (!state.IsDecor)
                continue;

            DecorRendererCount++;
            Transform target = state.Sprite != null
                ? state.Sprite.transform
                : state.Tilemap != null ? state.Tilemap.transform : null;
            if (target != null)
                decorObjects.Add(target);
        }
        DecorObjectCount = decorObjects.Count;
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
        IReadOnlyList<ProductionAnomalySite> sites =
            ProductionAnomalySite.ActiveSites;
        AnomalyZoneCount = 0;
        for (int i = 0; i < sites.Count; i++)
        {
            ProductionAnomalySite site = sites[i];
            if (site == null)
                continue;

            AnomalyZoneCount++;
            CaptureLines(site.transform);
            if (site.AnomalyZone != null)
                CaptureLines(site.AnomalyZone.transform);
        }
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

        IReadOnlyList<ProductionAnomalySite> sites =
            ProductionAnomalySite.ActiveSites;
        for (int i = 0; i < sites.Count; i++)
            sites[i]?.SetDebugVisualEmphasis(anomalyAccent);
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

        IReadOnlyList<ProductionAnomalySite> sites =
            ProductionAnomalySite.ActiveSites;
        for (int i = 0; i < sites.Count; i++)
            sites[i]?.SetDebugVisualEmphasis(1f);
        anomalyLines.Clear();
        AnomalyZoneCount = 0;
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

    private static void ApplyEnemyPreset(EnemyReadability preset)
    {
        enemyOutlineWidth = 1f;

        switch (preset)
        {
            case EnemyReadability.Low:
                enemySaturation = 1.2f;
                enemyBrightness = 1.1f;
                enemyTintStrength = 0.15f;
                enemyOutlineStrength = 0.6f;
                enemyOutlineEnabled = true;
                break;
            case EnemyReadability.Medium:
                enemySaturation = 1.5f;
                enemyBrightness = 1.25f;
                enemyTintStrength = 0.3f;
                enemyOutlineStrength = 1f;
                enemyOutlineEnabled = true;
                break;
            case EnemyReadability.High:
                enemySaturation = 1.9f;
                enemyBrightness = 1.45f;
                enemyTintStrength = 0.5f;
                enemyOutlineStrength = 0f;
                enemyOutlineEnabled = false;
                break;
            default:
                enemySaturation = 1f;
                enemyBrightness = 1f;
                enemyTintStrength = 0f;
                enemyOutlineStrength = 0f;
                enemyOutlineEnabled = false;
                break;
        }
    }

    private void EnsureEnemyReadabilityMaterial()
    {
        if (enemyReadabilityMaterial != null)
            return;

        enemyReadabilityMaterial = Resources.Load<Material>(
            "EnemyReadability"
        );

        if (enemyReadabilityMaterial == null)
        {
            Debug.LogWarning(
                "[ProductionSectorDebug] EnemyReadability shared material " +
                "was not found in Resources.",
                this
            );
        }
    }

    private void RegisterActiveEnemies()
    {
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            RegisterEnemy(enemy);
    }

    private void RegisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || enemyVisuals.ContainsKey(enemy))
            return;

        EnemyVisualState state = new()
        {
            Enemy = enemy,
            Category = ResolveEnemyCategory(enemy),
            WhiteFlash = enemy.GetComponent<EnemyWhiteFlash>()
        };
        SpriteRenderer[] renderers =
            enemy.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            state.Renderers.Add(new EnemyRendererState
            {
                Renderer = renderer,
                OriginalMaterial = renderer.sharedMaterial
            });
        }

        enemyVisuals.Add(enemy, state);
        ApplyEnemy(state);
    }

    private void UnregisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || !enemyVisuals.TryGetValue(
                enemy,
                out EnemyVisualState state))
        {
            return;
        }

        RestoreEnemy(state);
        enemyVisuals.Remove(enemy);
    }

    private void ApplyAllEnemies()
    {
        foreach (EnemyVisualState state in enemyVisuals.Values)
            ApplyEnemy(state);
    }

    private void ApplyEnemy(EnemyVisualState state)
    {
        if (state == null || state.Enemy == null)
            return;

        readabilityProperties ??= new MaterialPropertyBlock();

        bool apply = enemyReadability != EnemyReadability.Off &&
            enemyReadabilityMaterial != null &&
            MatchesEnemyScope(state);

        for (int i = 0; i < state.Renderers.Count; i++)
        {
            EnemyRendererState rendererState = state.Renderers[i];
            SpriteRenderer renderer = rendererState.Renderer;
            if (renderer == null)
                continue;

            Material targetMaterial = apply
                ? enemyReadabilityMaterial
                : rendererState.OriginalMaterial;
            SetEnemyBaseMaterial(state, renderer, targetMaterial);

            readabilityProperties.Clear();
            renderer.GetPropertyBlock(readabilityProperties);
            readabilityProperties.SetFloat(
                SaturationId,
                apply ? enemySaturation : 1f
            );
            readabilityProperties.SetFloat(
                BrightnessId,
                apply ? enemyBrightness : 1f
            );
            readabilityProperties.SetColor(TintId, EnemyTint);
            readabilityProperties.SetFloat(
                TintStrengthId,
                apply ? enemyTintStrength : 0f
            );
            readabilityProperties.SetColor(
                OutlineColorId,
                EnemyOutlineColor
            );
            readabilityProperties.SetFloat(
                OutlineStrengthId,
                apply && enemyOutlineEnabled ? enemyOutlineStrength : 0f
            );
            readabilityProperties.SetFloat(
                OutlineWidthId,
                apply ? enemyOutlineWidth : 1f
            );
            renderer.SetPropertyBlock(readabilityProperties);
        }
    }

    private static void SetEnemyBaseMaterial(
        EnemyVisualState state,
        SpriteRenderer renderer,
        Material material)
    {
        if (state.WhiteFlash != null &&
            state.WhiteFlash.TargetRenderer == renderer)
        {
            state.WhiteFlash.SetRuntimeBaseMaterial(material);
        }
        else
        {
            renderer.sharedMaterial = material;
        }
    }

    private bool MatchesEnemyScope(EnemyVisualState state)
    {
        switch (enemyScope)
        {
            case EnemyScope.CurrentZone:
                return currentSite != null &&
                    currentSite.ContainsWorldPosition(
                        state.Enemy.transform.position
                    );
            case EnemyScope.Basic:
                return state.Category == EnemyCategory.Basic;
            case EnemyScope.Elite:
                return state.Category == EnemyCategory.Elite;
            case EnemyScope.Shooter:
                return state.Category == EnemyCategory.Shooter;
            case EnemyScope.Bomber:
                return state.Category == EnemyCategory.Bomber;
            case EnemyScope.Boss:
                return state.Category == EnemyCategory.Boss;
            default:
                return true;
        }
    }

    private static EnemyCategory ResolveEnemyCategory(EnemyHealth enemy)
    {
        if (enemy.IsBoss)
            return EnemyCategory.Boss;
        if (enemy.GetComponent<EnemyShooterMovement>() != null)
            return EnemyCategory.Shooter;
        if (enemy.GetComponent<EnemyBomberMovement>() != null)
            return EnemyCategory.Bomber;
        if (enemy.GetComponent<EyesEnemyBehaviour>() != null ||
            enemy.GetComponent<TurretEnemyBehaviour>() != null)
        {
            return EnemyCategory.Elite;
        }

        return EnemyCategory.Basic;
    }

    private void RestoreEnemy(EnemyVisualState state)
    {
        if (state == null)
            return;

        readabilityProperties ??= new MaterialPropertyBlock();

        for (int i = 0; i < state.Renderers.Count; i++)
        {
            EnemyRendererState rendererState = state.Renderers[i];
            SpriteRenderer renderer = rendererState.Renderer;
            if (renderer == null)
                continue;

            SetEnemyBaseMaterial(
                state,
                renderer,
                rendererState.OriginalMaterial
            );
            readabilityProperties.Clear();
            renderer.GetPropertyBlock(readabilityProperties);
            readabilityProperties.SetFloat(SaturationId, 1f);
            readabilityProperties.SetFloat(BrightnessId, 1f);
            readabilityProperties.SetFloat(TintStrengthId, 0f);
            readabilityProperties.SetFloat(OutlineStrengthId, 0f);
            readabilityProperties.SetFloat(OutlineWidthId, 1f);
            renderer.SetPropertyBlock(readabilityProperties);
        }
    }

    private void RestoreAllEnemyVisuals()
    {
        foreach (EnemyVisualState state in enemyVisuals.Values)
            RestoreEnemy(state);
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
        RestoreMonochromeAnomalies();
        RestorePlayerMultiplier();
        RestoreEnvironment();
        RestoreAnomalyAccent();
        RestoreAllEnemyVisuals();
    }

    private void OnDisable()
    {
        EnemyHealth.Spawned -= RegisterEnemy;
        EnemyHealth.Despawned -= UnregisterEnemy;
        ProductionAnomalySite.VisualTargetsChanged -=
            HandleVisualTargetsChanged;
        RestoreAll();
        enemyVisuals.Clear();
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
