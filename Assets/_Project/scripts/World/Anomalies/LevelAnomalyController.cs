using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelAnomalyController : MonoBehaviour
{
    private const int LocalAnomalyPositionAttempts = 64;

    public static LevelAnomalyController Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private LevelAnomalyData[] availableAnomalies;

    [Header("View")]
    [SerializeField] private LevelAnomalyView view;

    [Header("Local Anomaly Zones")]
    [SerializeField] private BerserkZone berserkZonePrefab;
    [SerializeField] private StasisZone stasisZonePrefab;
    [SerializeField, Range(1, 2)] private int anomalyCount = 1;
    [SerializeField, Min(0.1f)] private float anomalyRadius = 4f;
    [SerializeField, Min(0.1f)] private float stasisRadius = 4f;
    [SerializeField, Min(1f)] private float enemySpeedMultiplier = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float playerSpeedMultiplier = 0.65f;
    [SerializeField, Min(0f)] private float edgePadding = 1f;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayerStart = 5f;
    [SerializeField, Min(0f)] private float minimumDistanceBetweenAnomalies = 2f;
    [SerializeField] private GameplayAreaService gameplayArea;

    [Header("Explosion")]
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField, Min(0.1f)] private float explosionFxLifetime = 0.8f;

    private readonly HashSet<EnemyHealth> registeredEnemies = new();
    private readonly HashSet<int> explodedEnemyIds = new();
    private readonly HashSet<int> chainSuppressedEnemyIds = new();
    private readonly List<BerserkZone> berserkZones = new();
    private readonly List<StasisZone> stasisZones = new();

    private enum LocalAnomalyKind
    {
        Berserk,
        Stasis
    }

    private readonly struct LocalAnomalyPlacement
    {
        public readonly Vector3 Position;
        public readonly float Radius;

        public LocalAnomalyPlacement(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    private LevelAnomalyData activeAnomaly;
    private PlayerHealth playerHealth;
    private PlayerCombatModifiers playerModifiers;
    private float originalOutgoingDamageMultiplier = 1f;
    private float previousTimeScale = 1f;
    private float regenerationTimer;
    private bool levelStarted;
    private bool enemyLifecycleSubscribed;
    private bool regenerationApplied;
    private bool hasteApplied;

    public LevelAnomalyData ActiveAnomaly => activeAnomaly;
    public bool IsIntroComplete { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        view?.Prepare();
    }

    public void BeginLevel(LevelNodeData level)
    {
        if (levelStarted)
            return;

        levelStarted = true;
        activeAnomaly = SelectAnomaly();

        if (activeAnomaly == null)
        {
            FinishIntroPause();
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerModifiers = player.GetComponent<PlayerCombatModifiers>();
        }

        ApplyGameplayEffect();
        StartCoroutine(IntroRoutine(level));
    }

    private LevelAnomalyData SelectAnomaly()
    {
        if (availableAnomalies == null || availableAnomalies.Length == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < availableAnomalies.Length; i++)
        {
            if (availableAnomalies[i] != null)
                totalWeight += availableAnomalies[i].SelectionWeight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;

        for (int i = 0; i < availableAnomalies.Length; i++)
        {
            LevelAnomalyData anomaly = availableAnomalies[i];

            if (anomaly == null)
                continue;

            roll -= anomaly.SelectionWeight;

            if (roll <= 0f)
                return anomaly;
        }

        return availableAnomalies[availableAnomalies.Length - 1];
    }

    private void ApplyGameplayEffect()
    {
        switch (activeAnomaly.AnomalyType)
        {
            case LevelAnomalyType.ExplosiveInfection:
                SubscribeEnemyLifecycle();
                break;

            case LevelAnomalyType.Berserk:
                SpawnLocalAnomalyZones();
                break;

            case LevelAnomalyType.Haste:
                ApplyHaste();
                break;

            case LevelAnomalyType.Regeneration:
                ApplyRegeneration();
                break;

            case LevelAnomalyType.Stasis:
                SpawnLocalAnomalyZones();
                break;
        }
    }

    private void ApplyHaste()
    {
        hasteApplied = true;
        ExperienceManager.Instance?.SetAnomalyXpGainMultiplier(
            activeAnomaly.ExperienceGainMultiplier
        );
        SubscribeEnemyLifecycle();
    }

    private void ApplyRegeneration()
    {
        regenerationApplied = true;

        if (playerModifiers == null)
            return;

        originalOutgoingDamageMultiplier =
            playerModifiers.bonusDamageMultiplier;
        playerModifiers.bonusDamageMultiplier =
            originalOutgoingDamageMultiplier *
            activeAnomaly.OutgoingDamageMultiplier;
    }

    private void SubscribeEnemyLifecycle()
    {
        if (enemyLifecycleSubscribed)
            return;

        enemyLifecycleSubscribed = true;
        EnemyHealth.Spawned += RegisterEnemy;
        EnemyHealth.Despawned += UnregisterEnemy;

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            RegisterEnemy(enemy);
    }

    private void RegisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || !registeredEnemies.Add(enemy))
            return;

        if (activeAnomaly.AnomalyType ==
            LevelAnomalyType.ExplosiveInfection)
        {
            enemy.OnDied += HandleEnemyDied;
        }

        if (activeAnomaly.AnomalyType == LevelAnomalyType.Haste)
        {
            EnemyMovement movement = GetEnemyMovement(enemy);
            movement?.SetAnomalySpeedMultiplier(
                activeAnomaly.EnemySpeedMultiplier
            );
        }
    }

    private void UnregisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || !registeredEnemies.Remove(enemy))
            return;

        enemy.OnDied -= HandleEnemyDied;
    }

    private void Update()
    {
        if (!IsIntroComplete ||
            !regenerationApplied ||
            activeAnomaly == null ||
            activeAnomaly.PlayerHealthPerSecond <= 0f ||
            playerHealth == null ||
            playerHealth.IsDead)
        {
            return;
        }

        regenerationTimer += Time.deltaTime;

        while (regenerationTimer >= 1f)
        {
            regenerationTimer -= 1f;
            playerHealth.Heal(activeAnomaly.PlayerHealthPerSecond);
        }
    }

    private void HandleEnemyDied(EnemyHealth source)
    {
        if (source == null || activeAnomaly == null)
            return;

        int sourceId = source.GetInstanceID();

        if (chainSuppressedEnemyIds.Remove(sourceId))
            return;

        if (!explodedEnemyIds.Add(sourceId))
            return;

        Vector2 position = source.transform.position;
        SpawnExplosionFx(position);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            activeAnomaly.ExplosionRadius
        );

        HashSet<PlayerHealth> damagedPlayers = new();
        HashSet<EnemyHealth> damagedEnemies = new();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            PlayerHealth hitPlayer = hit.GetComponentInParent<PlayerHealth>();

            if (hitPlayer != null && damagedPlayers.Add(hitPlayer))
            {
                Vector2 direction =
                    (Vector2)hitPlayer.transform.position - position;
                hitPlayer.TakeDamage(
                    activeAnomaly.PlayerExplosionDamage,
                    direction
                );
            }

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy == source ||
                enemy.IsDead ||
                !damagedEnemies.Add(enemy))
            {
                continue;
            }

            int enemyId = enemy.GetInstanceID();

            if (!activeAnomaly.AllowChainReaction)
                chainSuppressedEnemyIds.Add(enemyId);

            enemy.TakeDamage(activeAnomaly.EnemyExplosionDamage, position);
            chainSuppressedEnemyIds.Remove(enemyId);
        }
    }

    private void SpawnExplosionFx(Vector2 position)
    {
        AudioService.Instance?.PlayAt(
            AudioCueId.RocketExplosion,
            position
        );

        if (explosionFxPrefab == null)
            return;

        GameObject fx = Instantiate(
            explosionFxPrefab,
            position,
            Quaternion.identity
        );
        Destroy(fx, explosionFxLifetime);
    }

    private IEnumerator IntroRoutine(LevelNodeData level)
    {
        if (view != null)
            yield return view.PlayIntro(level, activeAnomaly);

        FinishIntroPause();
    }

    private void FinishIntroPause()
    {
        IsIntroComplete = true;

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = previousTimeScale;
    }

    private void RestoreRuntimeEffects()
    {
        if (regenerationApplied && playerModifiers != null)
        {
            playerModifiers.bonusDamageMultiplier =
                originalOutgoingDamageMultiplier;
        }

        if (hasteApplied)
        {
            ExperienceManager.Instance?.SetAnomalyXpGainMultiplier(1f);

            foreach (EnemyHealth enemy in registeredEnemies)
            {
                if (enemy == null)
                    continue;

                EnemyMovement movement = GetEnemyMovement(enemy);
                movement?.SetAnomalySpeedMultiplier(1f);
            }
        }

        CleanupLocalAnomalyZones();
        regenerationApplied = false;
        hasteApplied = false;
        regenerationTimer = 0f;
    }

    private static EnemyMovement GetEnemyMovement(EnemyHealth enemy)
    {
        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

        if (movement == null)
            movement = enemy.GetComponentInParent<EnemyMovement>();

        if (movement == null)
            movement = enemy.GetComponentInChildren<EnemyMovement>();

        return movement;
    }

    private void SpawnLocalAnomalyZones()
    {
        CleanupLocalAnomalyZones();
        ResolveGameplayArea();

        List<LocalAnomalyKind> availableKinds =
            GetAvailableLocalAnomalyKinds();

        if (availableKinds.Count == 0 || gameplayArea == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] Local anomalies cannot spawn: " +
                "prefabs or GameplayAreaService are missing.",
                this
            );
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return;

        Vector3 playerStart = playerObject.transform.position;
        List<LocalAnomalyPlacement> placements = new();
        int count = Mathf.Clamp(anomalyCount, 1, 2);
        LocalAnomalyKind firstKind =
            GetFirstLocalAnomalyKind(availableKinds);

        for (int i = 0; i < count; i++)
        {
            LocalAnomalyKind kind = i == 0
                ? firstKind
                : GetNextLocalAnomalyKind(
                    firstKind,
                    availableKinds
                );
            float radius = GetLocalAnomalyRadius(kind);

            if (!TryGetLocalAnomalyPosition(
                    playerStart,
                    radius,
                    placements,
                    out Vector3 position))
            {
                Debug.LogWarning(
                    "[LevelAnomalyController] No valid position exists " +
                    $"for local anomaly {i + 1}.",
                    this
                );
                break;
            }

            SpawnLocalAnomaly(kind, position, radius);
            placements.Add(
                new LocalAnomalyPlacement(position, radius)
            );
        }
    }

    private List<LocalAnomalyKind> GetAvailableLocalAnomalyKinds()
    {
        List<LocalAnomalyKind> result = new();

        if (berserkZonePrefab != null)
            result.Add(LocalAnomalyKind.Berserk);
        if (stasisZonePrefab != null)
            result.Add(LocalAnomalyKind.Stasis);

        return result;
    }

    private LocalAnomalyKind GetFirstLocalAnomalyKind(
        List<LocalAnomalyKind> availableKinds)
    {
        LocalAnomalyKind preferred =
            activeAnomaly.AnomalyType == LevelAnomalyType.Stasis
                ? LocalAnomalyKind.Stasis
                : LocalAnomalyKind.Berserk;

        if (availableKinds.Contains(preferred))
            return preferred;

        return availableKinds[Random.Range(0, availableKinds.Count)];
    }

    private static LocalAnomalyKind GetNextLocalAnomalyKind(
        LocalAnomalyKind firstKind,
        List<LocalAnomalyKind> availableKinds)
    {
        for (int i = 0; i < availableKinds.Count; i++)
        {
            if (availableKinds[i] != firstKind)
                return availableKinds[i];
        }

        return firstKind;
    }

    private float GetLocalAnomalyRadius(LocalAnomalyKind kind)
    {
        return kind == LocalAnomalyKind.Stasis
            ? Mathf.Max(0.1f, stasisRadius)
            : Mathf.Max(0.1f, anomalyRadius);
    }

    private void SpawnLocalAnomaly(
        LocalAnomalyKind kind,
        Vector3 position,
        float radius)
    {
        if (kind == LocalAnomalyKind.Stasis)
        {
            StasisZone zone = Instantiate(
                stasisZonePrefab,
                position,
                Quaternion.identity
            );
            zone.Initialize(radius, playerSpeedMultiplier);
            stasisZones.Add(zone);
            return;
        }

        BerserkZone berserkZone = Instantiate(
            berserkZonePrefab,
            position,
            Quaternion.identity
        );
        berserkZone.Initialize(radius, enemySpeedMultiplier);
        berserkZones.Add(berserkZone);
    }

    private bool TryGetLocalAnomalyPosition(
        Vector3 playerStart,
        float radius,
        List<LocalAnomalyPlacement> existingPlacements,
        out Vector3 position)
    {
        position = default;

        if (gameplayArea == null || gameplayArea.SpawnArea == null)
            return false;

        float requiredPlayerDistance =
            radius + Mathf.Max(0f, minimumDistanceFromPlayerStart);
        float placementPadding =
            radius + Mathf.Max(0f, edgePadding);
        float maximumDistance =
            gameplayArea.SpawnArea.bounds.size.magnitude;

        for (int attempt = 0;
             attempt < LocalAnomalyPositionAttempts;
             attempt++)
        {
            if (!gameplayArea.TryGetSpawnPosition(
                    playerStart,
                    requiredPlayerDistance,
                    maximumDistance,
                    1,
                    placementPadding,
                    out Vector3 candidate))
            {
                continue;
            }

            if (Vector2.Distance(candidate, playerStart) <
                requiredPlayerDistance)
            {
                continue;
            }

            if (!IsCircleInsidePlayableArea(
                    candidate,
                    placementPadding))
            {
                continue;
            }

            bool separated = true;

            for (int i = 0; i < existingPlacements.Count; i++)
            {
                LocalAnomalyPlacement existing =
                    existingPlacements[i];
                float requiredZoneDistance =
                    radius +
                    existing.Radius +
                    Mathf.Max(
                        0f,
                        minimumDistanceBetweenAnomalies
                    );

                if (Vector2.Distance(
                        candidate,
                        existing.Position) <
                    requiredZoneDistance)
                {
                    separated = false;
                    break;
                }
            }

            if (!separated)
                continue;

            position = candidate;
            return true;
        }

        return false;
    }

    private bool IsCircleInsidePlayableArea(
        Vector2 center,
        float radius)
    {
        const int Samples = 16;

        if (!gameplayArea.IsInsidePlayableArea(center))
            return false;

        for (int i = 0; i < Samples; i++)
        {
            float angle = i * Mathf.PI * 2f / Samples;
            Vector2 sample = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius;

            if (!gameplayArea.IsInsidePlayableArea(sample))
                return false;
        }

        return true;
    }

    private void CleanupLocalAnomalyZones()
    {
        for (int i = 0; i < berserkZones.Count; i++)
        {
            BerserkZone zone = berserkZones[i];

            if (zone == null)
                continue;

            zone.ClearEffects();
            Destroy(zone.gameObject);
        }

        berserkZones.Clear();

        for (int i = 0; i < stasisZones.Count; i++)
        {
            StasisZone zone = stasisZones[i];

            if (zone == null)
                continue;

            zone.ClearEffect();
            Destroy(zone.gameObject);
        }

        stasisZones.Clear();
    }

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }

    private void OnDisable()
    {
        RestoreRuntimeEffects();

        if (enemyLifecycleSubscribed)
        {
            EnemyHealth.Spawned -= RegisterEnemy;
            EnemyHealth.Despawned -= UnregisterEnemy;
            enemyLifecycleSubscribed = false;
        }

        foreach (EnemyHealth enemy in registeredEnemies)
        {
            if (enemy != null)
                enemy.OnDied -= HandleEnemyDied;
        }

        registeredEnemies.Clear();
        explodedEnemyIds.Clear();
        chainSuppressedEnemyIds.Clear();

        if (!IsIntroComplete && Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = previousTimeScale;

        if (Instance == this)
            Instance = null;
    }
}
