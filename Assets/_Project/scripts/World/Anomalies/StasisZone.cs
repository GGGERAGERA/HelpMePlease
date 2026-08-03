using System.Collections.Generic;
using UnityEngine;

public sealed class StasisZone : LocalAnomalyZone
{
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int RegionSizeId =
        Shader.PropertyToID("_RegionSize");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");

    private static readonly Dictionary<CharacterMovement2D, int>
        activeZoneCounts = new();

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField, Range(0.1f, 0.75f)] private float edgeWidth = 0.35f;
    [SerializeField, Min(0f)] private float pulseSpeed = 0.18f;
    [SerializeField, Range(0.6f, 1f)] private float fadeDuration = 0.8f;

    [Header("Enemy Tint")]
    [SerializeField] private Color enemyTint =
        new(0.45f, 0.78f, 1f, 1f);

    private readonly Dictionary<EnemyHealth, int>
        enemyColliderCounts = new();
    private readonly Dictionary<Component, int>
        projectileColliderCounts = new();

    private MeshRenderer visualRenderer;
    private MaterialPropertyBlock visualProperties;
    private CharacterMovement2D affectedMovement;
    private float speedMultiplier = 0.65f;
    private float enemySpeedMultiplier = 0.65f;
    private float visualFade;
    private float targetVisualFade;
    private int playerColliderCount;
    private bool initialized;
    private bool effectCleared;
    private bool despawning;

    private void Awake()
    {
        BuildVisual();
    }

    private void OnEnable()
    {
        EnemyHealth.Despawned += HandleEnemyDespawned;
        AnomalyProjectileLifecycle.Disabled += HandleProjectileDisabled;
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
        speedMultiplier = data.PlayerSpeedMultiplier;
        enemySpeedMultiplier = data.EnemySpeedMultiplier;
        ConfigureVisual(areaSize);
        effectCleared = false;
        despawning = false;
        visualFade = 0f;
        targetVisualFade = 1f;
        ApplyVisualProperties();
        initialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || effectCleared)
            return;

        CharacterMovement2D movement =
            other.GetComponentInParent<CharacterMovement2D>();

        if (movement != null)
        {
            if (affectedMovement != null &&
                affectedMovement != movement)
            {
                return;
            }

            affectedMovement = movement;
            playerColliderCount++;

            if (playerColliderCount > 1)
                return;

            ApplyEffect(movement);
            Controller?.NotifyLocalZoneEntered(this, Data);
            return;
        }

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy != null && !enemy.IsDead)
        {
            if (enemyColliderCounts.TryGetValue(enemy, out int enemyCount))
            {
                enemyColliderCounts[enemy] = enemyCount + 1;
                return;
            }

            enemyColliderCounts.Add(enemy, 1);
            EnemyAnomalyEffects.GetOrCreate(enemy)?.EnterZone(
                this,
                enemySpeedMultiplier,
                enemyTint
            );
            return;
        }

        IAnomalySpeedProjectile projectile = FindProjectile(other);

        if (projectile == null || projectile.ProjectileComponent == null)
            return;

        Component projectileComponent = projectile.ProjectileComponent;

        if (projectileColliderCounts.TryGetValue(
                projectileComponent,
                out int projectileCount))
        {
            projectileColliderCounts[projectileComponent] =
                projectileCount + 1;
            return;
        }

        projectileColliderCounts.Add(projectileComponent, 1);
        projectile.SetAnomalySpeedMultiplier(this, speedMultiplier);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        CharacterMovement2D movement =
            other.GetComponentInParent<CharacterMovement2D>();

        if (movement != null)
        {
            if (movement != affectedMovement)
                return;

            playerColliderCount = Mathf.Max(
                0,
                playerColliderCount - 1
            );

            if (playerColliderCount > 0)
                return;

            RemoveEffect(movement);
            affectedMovement = null;
            Controller?.NotifyLocalZoneExited(this);
            return;
        }

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy != null &&
            enemyColliderCounts.TryGetValue(enemy, out int enemyCount))
        {
            enemyCount--;

            if (enemyCount > 0)
            {
                enemyColliderCounts[enemy] = enemyCount;
                return;
            }

            enemyColliderCounts.Remove(enemy);
            enemy.GetComponent<EnemyAnomalyEffects>()?.ExitZone(this);
            return;
        }

        IAnomalySpeedProjectile projectile = FindProjectile(other);
        Component projectileComponent = projectile?.ProjectileComponent;

        if (projectileComponent == null ||
            !projectileColliderCounts.TryGetValue(
                projectileComponent,
                out int projectileCount))
        {
            return;
        }

        projectileCount--;

        if (projectileCount > 0)
        {
            projectileColliderCounts[projectileComponent] = projectileCount;
            return;
        }

        projectileColliderCounts.Remove(projectileComponent);
        projectile.RemoveAnomalySpeedMultiplier(this);
    }

    private void ApplyEffect(CharacterMovement2D movement)
    {
        if (movement == null)
            return;

        if (activeZoneCounts.TryGetValue(movement, out int zoneCount))
        {
            activeZoneCounts[movement] = zoneCount + 1;
            return;
        }

        activeZoneCounts[movement] = 1;
        movement.SetAnomalySpeedMultiplier(speedMultiplier);
    }

    private static void RemoveEffect(CharacterMovement2D movement)
    {
        if (ReferenceEquals(movement, null) ||
            !activeZoneCounts.TryGetValue(
                movement,
                out int zoneCount))
        {
            return;
        }

        zoneCount--;

        if (zoneCount > 0)
        {
            activeZoneCounts[movement] = zoneCount;
            return;
        }

        activeZoneCounts.Remove(movement);

        if (movement != null)
            movement.SetAnomalySpeedMultiplier(1f);
    }

    public void ClearEffect()
    {
        if (effectCleared)
            return;

        effectCleared = true;

        if (!ReferenceEquals(affectedMovement, null) &&
            playerColliderCount > 0)
        {
            RemoveEffect(affectedMovement);
        }

        if (playerColliderCount > 0)
            Controller?.NotifyLocalZoneExited(this);

        affectedMovement = null;
        playerColliderCount = 0;

        foreach (EnemyHealth enemy in enemyColliderCounts.Keys)
        {
            if (enemy != null)
                enemy.GetComponent<EnemyAnomalyEffects>()?.ExitZone(this);
        }

        enemyColliderCounts.Clear();

        foreach (Component component in projectileColliderCounts.Keys)
        {
            if (component is IAnomalySpeedProjectile projectile)
                projectile.RemoveAnomalySpeedMultiplier(this);
        }

        projectileColliderCounts.Clear();

        if (AreaCollider != null)
            AreaCollider.enabled = false;
    }

    public override void Despawn()
    {
        if (despawning)
            return;

        ClearEffect();
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
                "[StasisZone] Built-in Quad mesh is unavailable.",
                this
            );
            return;
        }

        GameObject visualObject = new("StasisZoneVisual");
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
        visualProperties.SetVector(RegionSizeId, AreaSize);
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;
        AnomalyProjectileLifecycle.Disabled -= HandleProjectileDisabled;
        ClearEffect();
    }

    private void HandleEnemyDespawned(EnemyHealth enemy)
    {
        if (enemy == null || !enemyColliderCounts.Remove(enemy))
            return;

        enemy.GetComponent<EnemyAnomalyEffects>()?.ExitZone(this);
    }

    private void HandleProjectileDisabled(Component projectile)
    {
        if (ReferenceEquals(projectile, null))
            return;

        projectileColliderCounts.Remove(projectile);
    }

    private static IAnomalySpeedProjectile FindProjectile(Collider2D other)
    {
        Bullet bullet = other.GetComponentInParent<Bullet>();

        if (bullet != null)
            return bullet;

        RocketProjectile rocket =
            other.GetComponentInParent<RocketProjectile>();

        if (rocket != null)
            return rocket;

        return other.GetComponentInParent<EnemyProjectile>();
    }
}
