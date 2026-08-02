using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ExplosiveZone : LocalAnomalyZone
{
    private const int ExplosionHitBufferSize = 128;
    private const float ExplosionFxLifetime = 2f;

    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseStrengthId =
        Shader.PropertyToID("_PulseStrength");
    private static readonly int PulseSharpnessId =
        Shader.PropertyToID("_PulseSharpness");
    private static readonly int RegionSizeId =
        Shader.PropertyToID("_RegionSize");
    private static readonly int InnerPatternIntensityId =
        Shader.PropertyToID("_InnerPatternIntensity");
    private static readonly int InnerPatternSpeedId =
        Shader.PropertyToID("_InnerPatternSpeed");
    private static readonly int InnerPatternScaleId =
        Shader.PropertyToID("_InnerPatternScale");
    private static readonly int WarningPulseFrequencyId =
        Shader.PropertyToID("_WarningPulseFrequency");
    private static readonly int InnerColorId =
        Shader.PropertyToID("_InnerColor");
    private static readonly int EdgeColorId =
        Shader.PropertyToID("_EdgeColor");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField, Range(0.1f, 0.75f)] private float edgeWidth = 0.35f;
    [SerializeField, Min(0f)] private float pulseSpeed = 0.22f;
    [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.22f;
    [SerializeField, Min(1f)] private float pulseSharpness = 8f;
    [SerializeField, Range(0f, 0.25f)]
    private float innerPatternIntensity = 0.12f;
    [SerializeField, Min(0f)] private float innerPatternSpeed = 0.7f;
    [SerializeField, Min(0.75f)] private float innerPatternScale = 2.5f;
    [SerializeField, Min(0f)] private float warningPulseFrequency = 0.35f;
    [SerializeField] private Color innerColor =
        new(0.12f, 0.035f, 0.012f, 0.2f);
    [SerializeField] private Color edgeColor =
        new(1f, 0.32f, 0.025f, 0.78f);
    [SerializeField, Range(0.6f, 1f)] private float fadeDuration = 0.8f;

    private readonly Dictionary<EnemyHealth, int> enemyColliderCounts = new();
    private readonly Collider2D[] explosionHitBuffer =
        new Collider2D[ExplosionHitBufferSize];
    private readonly HashSet<EnemyHealth> damagedEnemies = new();
    private readonly List<GameObject> activeExplosionFx = new();

    private MeshRenderer visualRenderer;
    private MaterialPropertyBlock visualProperties;
    private ContactFilter2D explosionFilter;
    private float explosionDelay;
    private float explosionRadius;
    private float explosionDamage;
    private GameObject explosionEffectPrefab;
    private float visualFade;
    private float targetVisualFade;
    private int playerColliderCount;
    private bool initialized;
    private bool effectsCleared;
    private bool despawning;

    private void Awake()
    {
        explosionFilter = ContactFilter2D.noFilter;
        explosionFilter.useTriggers = true;
        BuildVisual();
    }

    private void Update()
    {
        if (visualRenderer == null)
            return;

        visualFade = Mathf.MoveTowards(
            visualFade,
            targetVisualFade,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );
        ApplyVisualProperties();

        if (despawning && visualFade <= 0f)
            Destroy(gameObject);
    }

    protected override void InitializeFromData(
        LocalAnomalyData data,
        Vector2 areaSize)
    {
        explosionDelay = data.ExplosionDelay;
        explosionRadius = data.ExplosionRadius;
        explosionDamage = data.ExplosionDamage;
        explosionEffectPrefab = data.ExplosionEffectPrefab;
        ConfigureVisual(areaSize);
        effectsCleared = false;
        despawning = false;
        visualFade = 0f;
        targetVisualFade = 1f;
        ApplyVisualProperties();
        initialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || effectsCleared)
            return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            playerColliderCount++;

            if (playerColliderCount == 1)
                Controller?.NotifyLocalZoneEntered(this, Data);

            return;
        }

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy == null || enemy.IsBoss || enemy.IsDead)
            return;

        if (enemyColliderCounts.TryGetValue(enemy, out int colliderCount))
        {
            enemyColliderCounts[enemy] = colliderCount + 1;
            return;
        }

        enemyColliderCounts.Add(enemy, 1);
        Controller?.ResetExplosiveDeathClaim(enemy);
        enemy.OnDied += HandleEnemyDied;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            bool wasInside = playerColliderCount > 0;
            playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

            if (wasInside && playerColliderCount == 0)
                Controller?.NotifyLocalZoneExited(this);

            return;
        }

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy == null ||
            !enemyColliderCounts.TryGetValue(enemy, out int colliderCount))
        {
            return;
        }

        colliderCount--;

        if (colliderCount > 0)
        {
            enemyColliderCounts[enemy] = colliderCount;
            return;
        }

        UnregisterEnemy(enemy);
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsBoss || effectsCleared)
            return;

        if (!enemyColliderCounts.ContainsKey(enemy))
            return;

        Vector2 deathPosition = enemy.transform.position;
        UnregisterEnemy(enemy);

        if (Controller == null ||
            !Controller.TryClaimExplosiveDeath(enemy))
        {
            return;
        }

        StartCoroutine(ExplodeAfterDelay(
            deathPosition,
            enemy.GetInstanceID()
        ));
    }

    private IEnumerator ExplodeAfterDelay(
        Vector2 position,
        int sourceInstanceId)
    {
        if (explosionDelay > 0f)
            yield return new WaitForSeconds(explosionDelay);

        if (!effectsCleared)
            Explode(position, sourceInstanceId);
    }

    private void Explode(Vector2 position, int sourceInstanceId)
    {
        SpawnExplosionFx(position);
        AudioService.Instance?.PlayAt(AudioCueId.RocketExplosion, position);

        int hitCount = Physics2D.OverlapCircle(
            position,
            explosionRadius,
            explosionFilter,
            explosionHitBuffer
        );

        damagedEnemies.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = explosionHitBuffer[i];
            explosionHitBuffer[i] = null;

            if (hit == null)
                continue;

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy.GetInstanceID() == sourceInstanceId ||
                enemy.IsDead ||
                !damagedEnemies.Add(enemy))
            {
                continue;
            }

            enemy.TakeDamage(explosionDamage, position);
        }
    }

    private void SpawnExplosionFx(Vector2 position)
    {
        if (explosionEffectPrefab == null)
            return;

        GameObject fx = Instantiate(
            explosionEffectPrefab,
            position,
            Quaternion.identity
        );
        activeExplosionFx.Add(fx);
        StartCoroutine(RemoveExplosionFxAfterLifetime(
            fx,
            ExplosionFxLifetime
        ));
    }

    private IEnumerator RemoveExplosionFxAfterLifetime(
        GameObject fx,
        float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        activeExplosionFx.Remove(fx);

        if (fx != null)
            Destroy(fx);
    }

    private void UnregisterEnemy(EnemyHealth enemy)
    {
        if (ReferenceEquals(enemy, null) ||
            !enemyColliderCounts.Remove(enemy))
        {
            return;
        }

        if (enemy != null)
            enemy.OnDied -= HandleEnemyDied;
    }

    private void ClearEffects()
    {
        if (effectsCleared)
            return;

        effectsCleared = true;
        StopAllCoroutines();

        foreach (EnemyHealth enemy in enemyColliderCounts.Keys)
        {
            if (enemy != null)
                enemy.OnDied -= HandleEnemyDied;
        }

        enemyColliderCounts.Clear();
        damagedEnemies.Clear();

        for (int i = 0; i < activeExplosionFx.Count; i++)
        {
            if (activeExplosionFx[i] != null)
                Destroy(activeExplosionFx[i]);
        }

        activeExplosionFx.Clear();

        if (playerColliderCount > 0)
            Controller?.NotifyLocalZoneExited(this);

        playerColliderCount = 0;

        if (AreaCollider != null)
            AreaCollider.enabled = false;
    }

    public override void Despawn()
    {
        if (despawning)
            return;

        ClearEffects();
        despawning = true;
        targetVisualFade = 0f;

        if (visualRenderer == null)
            Destroy(gameObject);
    }

    private void BuildVisual()
    {
        if (visualMaterial == null)
            return;

        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        if (quad == null)
        {
            Debug.LogWarning(
                "[ExplosiveZone] Built-in Quad mesh is unavailable.",
                this
            );
            return;
        }

        GameObject visualObject = new("ExplosiveZoneVisual");
        visualObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = quad;

        visualRenderer = visualObject.AddComponent<MeshRenderer>();
        visualRenderer.sharedMaterial = visualMaterial;
        visualRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        visualRenderer.receiveShadows = false;
        visualRenderer.sortingLayerName = "Midground";
        visualRenderer.sortingOrder = 1;

        visualProperties = new MaterialPropertyBlock();
        ApplyVisualProperties();
    }

    private void ConfigureVisual(Vector2 areaSize)
    {
        if (visualRenderer == null)
            return;

        visualRenderer.transform.localScale =
            new Vector3(areaSize.x, areaSize.y, 1f);
    }

    private void ApplyVisualProperties()
    {
        if (visualRenderer == null || visualProperties == null)
            return;

        visualProperties.SetFloat(FadeId, visualFade);
        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
        visualProperties.SetFloat(PulseSpeedId, pulseSpeed);
        visualProperties.SetFloat(PulseStrengthId, pulseStrength);
        visualProperties.SetFloat(PulseSharpnessId, pulseSharpness);
        visualProperties.SetVector(RegionSizeId, AreaSize);
        visualProperties.SetFloat(
            InnerPatternIntensityId,
            innerPatternIntensity
        );
        visualProperties.SetFloat(InnerPatternSpeedId, innerPatternSpeed);
        visualProperties.SetFloat(InnerPatternScaleId, innerPatternScale);
        visualProperties.SetFloat(
            WarningPulseFrequencyId,
            warningPulseFrequency
        );
        visualProperties.SetColor(InnerColorId, innerColor);
        visualProperties.SetColor(EdgeColorId, edgeColor);
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        ClearEffects();
    }
}
