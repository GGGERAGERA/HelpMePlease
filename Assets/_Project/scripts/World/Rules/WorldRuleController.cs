using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldRuleController : MonoBehaviour
{
#if UNITY_EDITOR
    private const string GoldenCoinPrefabAssetPath =
        "Assets/_Project/prefabs/Pickups/p_coin1.prefab";
#endif

    public static WorldRuleController Instance { get; private set; }

    [Header("View")]
    [SerializeField] private WorldRuleVisual worldRuleVisual;

    [Header("Diagnostics")]
    [SerializeField] private bool logGoldenEnemyAssignments;
    [SerializeField] private bool logWindSelection;

    [Header("Golden / Enemy Visual")]
    [SerializeField] private Color goldenEnemyTint =
        new Color(1f, 0.62f, 0.08f, 1f);
    [SerializeField, Min(0.01f)] private float assignmentPulseDuration = 0.28f;
    [SerializeField, Min(0f)] private float deathFlashIntensity = 1.35f;

    [Header("Golden / Existing FX")]
    [SerializeField] private ParticleSystem goldenDeathFxPrefab;

    [Header("Golden / Physical Coin Reward")]
    [SerializeField] private GoldenCoinPickup goldenCoinPrefab;

    private static readonly Vector2[] CardinalWindDirections =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    private static readonly Vector2[] EightWindDirections =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right,
        new Vector2(1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private readonly HashSet<EnemyHealth> registeredEnemies = new();

    private WorldRuleData activeRule;
    private CharacterMovement2D playerMovement;
    private EnemySpawner enemySpawner;
    private bool enemyLifecycleSubscribed;
    private bool playerMoveSpeedApplied;
    private bool enemyMoveSpeedApplied;
    private bool goldenSpawnSubscribed;
    private float goldenEnemyChance;
    private float goldenEnemyHealthMultiplier = 1f;
    private float goldenEnemyRewardMultiplier = 1f;
    private Vector2 activeWindDirection;
    private Vector2 activeWindVelocity;
    private Vector2 pendingWindDirection;
    private float windDirectionChangeTimeRemaining;
    private bool windWarningVisible;
    private bool enemyWindApplied;
    private Coroutine snowLifecycle;
    private bool darknessActive;
    private bool darknessShotSubscribed;
    private float nextLaserRevealTime;

    public WorldRuleData ActiveRule => activeRule;
    public Vector2 ActiveWindDirection => activeWindDirection;
    public Vector2 ActiveWindVelocity => activeWindVelocity;
    public Vector2 ProjectileWindVelocity { get; private set; }
    public bool IsIntroComplete => true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (goldenCoinPrefab != null)
            return;

        GameObject coinPrefabObject =
            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                GoldenCoinPrefabAssetPath
            );
        goldenCoinPrefab = coinPrefabObject != null
            ? coinPrefabObject.GetComponent<GoldenCoinPickup>()
            : null;

        if (goldenCoinPrefab != null)
            UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void Update()
    {
        if (activeRule == null ||
            activeRule.RuleType != WorldRuleType.Wind ||
            activeWindVelocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        windDirectionChangeTimeRemaining -= Time.deltaTime;

        if (!windWarningVisible &&
            windDirectionChangeTimeRemaining <=
            activeRule.WindDirectionWarningDuration)
        {
            pendingWindDirection = SelectNextCardinalWindDirection(
                activeWindDirection
            );
            windWarningVisible = true;
            worldRuleVisual?.WarnWind(pendingWindDirection);
        }

        if (windDirectionChangeTimeRemaining > 0f)
            return;

        Vector2 nextDirection = pendingWindDirection.sqrMagnitude > 0.0001f
            ? pendingWindDirection
            : SelectNextCardinalWindDirection(activeWindDirection);
        ApplyWindDirection(nextDirection);
        ScheduleNextWindDirectionChange();
    }

    public void Apply(WorldRuleData rule)
    {
        Clear();

        if (rule == null || rule.RuleType == WorldRuleType.None)
            return;

        activeRule = rule;
        ResolvePlayerReferences();
        ApplyMovementModifiers();
        ApplyGoldenEnemyAssignment();
        ApplySpawnPressure();
        worldRuleVisual?.Apply(rule);
        ApplyDarkness();
        ApplyWind();
        ApplySnow();
    }

    public void Clear()
    {
        StopSnowLifecycle();
        RestoreSpawnPressure();
        RestoreRuntimeEffects();
        StopGoldenEnemyAssignment();
        StopDarkness();
        UnsubscribeEnemyLifecycle();

        activeRule = null;
        playerMovement = null;
        worldRuleVisual?.Clear();
    }

    private void ApplySnow()
    {
        if (activeRule == null ||
            activeRule.RuleType != WorldRuleType.Snow)
        {
            return;
        }

        StopSnowLifecycle();
        worldRuleVisual?.SetSnowBlizzardState(
            0f,
            0f,
            activeRule.SnowTransitionDuration
        );
        snowLifecycle = StartCoroutine(SnowCycle(activeRule));
    }

    private void ApplyDarkness()
    {
        if (activeRule == null ||
            activeRule.RuleType != WorldRuleType.Darkness)
        {
            return;
        }

        darknessActive = true;
        nextLaserRevealTime = 0f;
        SubscribeEnemyLifecycle();

        if (!darknessShotSubscribed)
        {
            BaseWeapon.ShotFired += HandlePlayerShot;
            darknessShotSubscribed = true;
        }

        foreach (EnemyHealth enemy in registeredEnemies)
            ApplyDarknessToEnemy(enemy, true);
    }

    private void StopDarkness()
    {
        if (darknessShotSubscribed)
        {
            BaseWeapon.ShotFired -= HandlePlayerShot;
            darknessShotSubscribed = false;
        }

        if (darknessActive)
        {
            foreach (EnemyHealth enemy in registeredEnemies)
                ApplyDarknessToEnemy(enemy, false);
        }

        darknessActive = false;
        nextLaserRevealTime = 0f;
        worldRuleVisual?.StopDarknessReveal();
    }

    private void HandlePlayerShot(Vector2 origin, WeaponShotKind shotKind)
    {
        if (!darknessActive || activeRule == null)
            return;

        if (shotKind == WeaponShotKind.Laser)
        {
            if (Time.unscaledTime < nextLaserRevealTime)
                return;

            nextLaserRevealTime = Time.unscaledTime +
                activeRule.DarknessLaserRevealCooldown;
        }

        float multiplier = shotKind == WeaponShotKind.Rocket
            ? activeRule.DarknessRocketRevealMultiplier
            : 1f;
        worldRuleVisual?.RevealDarkness(origin, multiplier);
    }

    private IEnumerator SnowCycle(WorldRuleData snowRule)
    {
        while (activeRule == snowRule)
        {
            float calmDuration = Random.Range(
                snowRule.SnowCalmDurationMin,
                snowRule.SnowCalmDurationMax
            );
            yield return new WaitForSeconds(calmDuration);

            if (activeRule != snowRule)
                break;

            float direction = Random.value < 0.5f ? -1f : 1f;
            worldRuleVisual?.SetSnowBlizzardState(
                1f,
                direction,
                snowRule.SnowWarningDuration
            );
            yield return new WaitForSeconds(
                snowRule.SnowWarningDuration
            );

            if (activeRule != snowRule)
                break;

            float blizzardDuration = Random.Range(
                snowRule.SnowBlizzardDurationMin,
                snowRule.SnowBlizzardDurationMax
            );
            yield return new WaitForSeconds(blizzardDuration);

            if (activeRule != snowRule)
                break;

            worldRuleVisual?.SetSnowBlizzardState(
                0f,
                direction,
                snowRule.SnowTransitionDuration
            );
            yield return new WaitForSeconds(
                snowRule.SnowTransitionDuration
            );
        }

        snowLifecycle = null;
    }

    private void StopSnowLifecycle()
    {
        if (snowLifecycle == null)
            return;

        StopCoroutine(snowLifecycle);
        snowLifecycle = null;
    }

    private void ResolvePlayerReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        playerMovement = player.GetComponent<CharacterMovement2D>();
    }

    private void ApplyMovementModifiers()
    {
        float playerMultiplier = activeRule.PlayerMoveSpeedMultiplier;

        if (!Mathf.Approximately(playerMultiplier, 1f) &&
            playerMovement != null)
        {
            playerMovement.SetWorldRuleSpeedMultiplier(playerMultiplier);
            playerMoveSpeedApplied = true;
        }

        float enemyMultiplier = activeRule.EnemyMoveSpeedMultiplier;

        if (!Mathf.Approximately(enemyMultiplier, 1f) ||
            (activeRule.RuleType == WorldRuleType.Wind &&
             activeRule.WindForce > 0f))
        {
            enemyMoveSpeedApplied =
                !Mathf.Approximately(enemyMultiplier, 1f);
            SubscribeEnemyLifecycle();
        }
    }

    private void ApplySpawnPressure()
    {
        ResolveEnemySpawner();
        enemySpawner?.SetWorldRuleSpawnPressureMultiplier(
            activeRule.SpawnPressureMultiplier
        );
    }

    private void RestoreSpawnPressure()
    {
        ResolveEnemySpawner();
        enemySpawner?.SetWorldRuleSpawnPressureMultiplier(1f);
    }

    private void ResolveEnemySpawner()
    {
        if (enemySpawner == null || !enemySpawner.gameObject.scene.IsValid())
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
    }

    private void ApplyWind()
    {
        float force = activeRule.WindForce;
        WindDirectionMode mode = activeRule.WindDirectionMode;

        if (force <= 0f || mode == WindDirectionMode.None)
            return;

        enemyWindApplied = true;
        ApplyWindDirection(SelectWindDirection(activeRule, mode));
        ScheduleNextWindDirectionChange();

    }

    private void ApplyWindDirection(Vector2 direction)
    {
        if (activeRule == null || direction.sqrMagnitude <= 0.0001f)
            return;

        activeWindDirection = direction.normalized;
        activeWindVelocity = activeWindDirection * activeRule.WindForce;
        ProjectileWindVelocity = activeWindVelocity *
            activeRule.WindProjectileForceMultiplier;
        playerMovement?.SetWorldRuleExternalVelocity(activeWindVelocity);

        Vector2 enemyWindVelocity = activeWindVelocity *
            activeRule.WindEnemyForceMultiplier;

        foreach (EnemyHealth enemy in registeredEnemies)
        {
            if (enemy == null)
                continue;

            GetEnemyMovement(enemy)?.SetWorldRuleExternalVelocity(
                enemyWindVelocity
            );
        }

        pendingWindDirection = Vector2.zero;
        windWarningVisible = false;
        worldRuleVisual?.ShowWind(activeWindDirection);

#if UNITY_EDITOR
        if (logWindSelection)
        {
            Debug.Log(
                $"[WindRule] Direction={activeWindDirection} " +
                $"Velocity={activeWindVelocity}",
                this
            );
        }
#endif
    }

    private void ScheduleNextWindDirectionChange()
    {
        if (activeRule == null)
            return;

        windDirectionChangeTimeRemaining = Random.Range(
            activeRule.WindMinDirectionDuration,
            activeRule.WindMaxDirectionDuration
        );
        pendingWindDirection = Vector2.zero;
        windWarningVisible = false;
    }

    private static Vector2 SelectNextCardinalWindDirection(
        Vector2 currentDirection)
    {
        int currentIndex = -1;

        for (int i = 0; i < CardinalWindDirections.Length; i++)
        {
            if (Vector2.Dot(
                    CardinalWindDirections[i],
                    currentDirection) > 0.999f)
            {
                currentIndex = i;
                break;
            }
        }

        int offset = Random.Range(1, CardinalWindDirections.Length);
        int nextIndex = currentIndex >= 0
            ? (currentIndex + offset) % CardinalWindDirections.Length
            : Random.Range(0, CardinalWindDirections.Length);
        return CardinalWindDirections[nextIndex];
    }

    private static Vector2 SelectWindDirection(
        WorldRuleData rule,
        WindDirectionMode mode)
    {
        switch (mode)
        {
            case WindDirectionMode.Fixed:
                return rule.FixedWindDirection;

            case WindDirectionMode.RandomCardinal:
                return CardinalWindDirections[
                    Random.Range(0, CardinalWindDirections.Length)
                ];

            case WindDirectionMode.RandomEightDirections:
                return EightWindDirections[
                    Random.Range(0, EightWindDirections.Length)
                ];

            default:
                return Vector2.zero;
        }
    }

    private void ApplyGoldenEnemyAssignment()
    {
        goldenEnemyChance = activeRule.GoldenEnemyChance;
        goldenEnemyHealthMultiplier =
            activeRule.GoldenEnemyHealthMultiplier;
        goldenEnemyRewardMultiplier =
            activeRule.GoldenEnemyRewardMultiplier;

        if (goldenEnemyChance <= 0f || goldenSpawnSubscribed)
            return;

        goldenSpawnSubscribed = true;
        EnemyHealth.SpawnConfigured += TryAssignGoldenEnemy;
    }

    private void TryAssignGoldenEnemy(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsBoss || goldenEnemyChance <= 0f)
            return;

        GoldenEnemyModifier modifier =
            enemy.GetComponent<GoldenEnemyModifier>();

        if (modifier == null)
            modifier = enemy.gameObject.AddComponent<GoldenEnemyModifier>();

        modifier.ConfigureVisuals(
            goldenEnemyTint,
            assignmentPulseDuration,
            deathFlashIntensity,
            goldenDeathFxPrefab
        );

        if (!modifier.TryBeginSpawnRoll() ||
            Random.value >= goldenEnemyChance)
        {
            return;
        }

        modifier.Apply(
            goldenEnemyHealthMultiplier,
            goldenEnemyRewardMultiplier
        );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[GoldenRule] Roll success: enemy='{enemy.name}', " +
            $"healthMultiplier={goldenEnemyHealthMultiplier:F2}, " +
            $"rewardMultiplier={goldenEnemyRewardMultiplier:F2}.",
            enemy
        );
#endif
    }

    private void StopGoldenEnemyAssignment()
    {
        if (goldenSpawnSubscribed)
        {
            EnemyHealth.SpawnConfigured -= TryAssignGoldenEnemy;
            goldenSpawnSubscribed = false;
        }

        goldenEnemyChance = 0f;
        goldenEnemyHealthMultiplier = 1f;
        goldenEnemyRewardMultiplier = 1f;
        GoldenCoinPickup.ClearAll();
    }

    public void HandleGoldenEnemyDeath(EnemyHealth enemy)
    {
        if (enemy == null || activeRule == null ||
            activeRule.RuleType != WorldRuleType.Golden)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[GoldenRule] Drop rejected: enemy=" +
                $"'{(enemy != null ? enemy.name : "null")}', " +
                $"activeRule='{(activeRule != null ? activeRule.name : "null")}'.",
                this
            );
#endif
            return;
        }

        int coinCount = Random.Range(
            activeRule.GoldenCoinCountMin,
            activeRule.GoldenCoinCountMax + 1
        );
        int availableSlots = Mathf.Max(
            0,
            activeRule.GoldenCoinActiveLimit -
            GoldenCoinPickup.ActiveCount
        );
        Transform player = playerMovement != null
            ? playerMovement.transform
            : null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[GoldenRule] Drop request: enemy='{enemy.name}', " +
            $"count={coinCount}, activeCoins={GoldenCoinPickup.ActiveCount}, " +
            $"availableSlots={availableSlots}.",
            enemy
        );
#endif

        if (goldenCoinPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "[GoldenRule] Drop aborted: goldenCoinPrefab is null. " +
                "No direct-gold fallback was used.",
                this
            );
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (player == null)
        {
            Debug.LogWarning(
                "[GoldenRule] Player Transform is null; coins will still " +
                "spawn and try one player lookup during Initialize.",
                this
            );
        }
#endif

        int spawnCount = Mathf.Min(coinCount, availableSlots);

        for (int i = 0; i < spawnCount; i++)
        {
            GoldenCoinPickup coin = Instantiate(
                goldenCoinPrefab,
                enemy.transform.position,
                Quaternion.identity
            );
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[GoldenRule] Coin instantiated: object='{coin.name}', " +
                $"position={coin.transform.position}.",
                coin
            );
#endif
            coin.Initialize(
                player,
                activeRule.GoldenCoinValue,
                activeRule.GoldenCoinLifetime,
                activeRule.GoldenCoinPickupRadius,
                activeRule.GoldenCoinAttractSpeed,
                activeRule.GoldenCoinScatterSpeed,
                activeRule.GoldenCoinFadeDuration
            );
        }

        int overflowValue = (coinCount - spawnCount) *
            activeRule.GoldenCoinValue;

        if (overflowValue > 0)
            CurrencyManager.Instance?.AddGold(overflowValue);
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

        if (activeRule == null)
            return;

        EnemyMovement movement = GetEnemyMovement(enemy);
        movement?.SetWorldRuleSpeedMultiplier(
            activeRule.EnemyMoveSpeedMultiplier
        );
        movement?.SetWorldRuleExternalVelocity(
            activeWindVelocity * activeRule.WindEnemyForceMultiplier
        );

        if (darknessActive)
            ApplyDarknessToEnemy(enemy, true);
    }

    private void UnregisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || !registeredEnemies.Remove(enemy))
            return;

        ApplyDarknessToEnemy(enemy, false);
    }

    private void ApplyDarknessToEnemy(EnemyHealth enemy, bool active)
    {
        if (enemy == null)
            return;

        DarknessEnemyMarker marker =
            enemy.GetComponent<DarknessEnemyMarker>();

        if (active && marker == null)
            marker = enemy.gameObject.AddComponent<DarknessEnemyMarker>();

        if (marker == null)
            return;

        marker.SetActive(
            active,
            worldRuleVisual != null
                ? worldRuleVisual.DarknessMarkerSprite
                : null,
            worldRuleVisual != null
                ? worldRuleVisual.DarknessMarkerMaterial
                : null,
            activeRule != null
                ? activeRule.DarknessEnemyMarkerIntensity
                : 0f,
            enemy
        );
    }

    private void RestoreRuntimeEffects()
    {
        if (playerMovement != null)
            playerMovement.SetWorldRuleExternalVelocity(Vector2.zero);

        activeWindDirection = Vector2.zero;
        activeWindVelocity = Vector2.zero;
        pendingWindDirection = Vector2.zero;
        ProjectileWindVelocity = Vector2.zero;
        windDirectionChangeTimeRemaining = 0f;
        windWarningVisible = false;

        if (playerMoveSpeedApplied && playerMovement != null)
            playerMovement.SetWorldRuleSpeedMultiplier(1f);

        if (enemyMoveSpeedApplied || enemyWindApplied)
        {
            foreach (EnemyHealth enemy in registeredEnemies)
            {
                if (enemy == null)
                    continue;

                EnemyMovement movement = GetEnemyMovement(enemy);
                movement?.SetWorldRuleSpeedMultiplier(1f);
                movement?.SetWorldRuleExternalVelocity(Vector2.zero);
            }
        }

        playerMoveSpeedApplied = false;
        enemyMoveSpeedApplied = false;
        enemyWindApplied = false;
    }

    private void UnsubscribeEnemyLifecycle()
    {
        if (enemyLifecycleSubscribed)
        {
            EnemyHealth.Spawned -= RegisterEnemy;
            EnemyHealth.Despawned -= UnregisterEnemy;
            enemyLifecycleSubscribed = false;
        }

        registeredEnemies.Clear();
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

    private void OnDisable()
    {
        if (Instance != this)
            return;

        Clear();

        if (Instance == this)
            Instance = null;
    }
}
