using System.Collections.Generic;
using UnityEngine;

public sealed class WorldRuleController : MonoBehaviour
{
    public static WorldRuleController Instance { get; private set; }

    [Header("View")]
    [SerializeField] private WorldRuleVisual worldRuleVisual;

    [Header("Explosion")]
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField, Min(0.1f)] private float explosionFxLifetime = 0.8f;

    [Header("Diagnostics")]
    [SerializeField] private bool logGoldenEnemyAssignments;
    [SerializeField] private bool logWindSelection;

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
    private readonly HashSet<int> explodedEnemyIds = new();
    private readonly HashSet<int> chainSuppressedEnemyIds = new();

    private WorldRuleData activeRule;
    private PlayerHealth playerHealth;
    private CharacterMovement2D playerMovement;
    private PlayerCombatModifiers playerModifiers;
    private float originalOutgoingDamageMultiplier = 1f;
    private float regenerationTimer;
    private bool enemyLifecycleSubscribed;
    private bool regenerationApplied;
    private bool hasteApplied;
    private bool playerMoveSpeedApplied;
    private bool enemyMoveSpeedApplied;
    private bool goldenSpawnSubscribed;
    private float goldenEnemyChance;
    private float goldenEnemyHealthMultiplier = 1f;
    private float goldenEnemyRewardMultiplier = 1f;
    private Vector2 activeWindDirection;
    private Vector2 activeWindVelocity;

    public WorldRuleData ActiveRule => activeRule;
    public Vector2 ActiveWindDirection => activeWindDirection;
    public Vector2 ActiveWindVelocity => activeWindVelocity;
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

    public void Apply(WorldRuleData rule)
    {
        Clear();

        if (rule == null || rule.RuleType == WorldRuleType.None)
            return;

        activeRule = rule;
        ResolvePlayerReferences();
        ApplyMovementModifiers();
        ApplyGoldenEnemyAssignment();
        ApplyTransitionalGameplayEffect();
        worldRuleVisual?.Apply(rule);
        ApplyWind();
    }

    public void Clear()
    {
        RestoreRuntimeEffects();
        StopGoldenEnemyAssignment();
        UnsubscribeEnemyLifecycle();

        activeRule = null;
        playerHealth = null;
        playerMovement = null;
        playerModifiers = null;

        explodedEnemyIds.Clear();
        chainSuppressedEnemyIds.Clear();
        worldRuleVisual?.Clear();
    }

    private void ResolvePlayerReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        playerHealth = player.GetComponent<PlayerHealth>();
        playerMovement = player.GetComponent<CharacterMovement2D>();
        playerModifiers = player.GetComponent<PlayerCombatModifiers>();
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

        if (!Mathf.Approximately(enemyMultiplier, 1f))
        {
            enemyMoveSpeedApplied = true;
            SubscribeEnemyLifecycle();
        }
    }

    private void ApplyTransitionalGameplayEffect()
    {
        switch (activeRule.RuleType)
        {
            case WorldRuleType.ExplosiveInfection:
                SubscribeEnemyLifecycle();
                break;

            case WorldRuleType.Haste:
                ApplyHaste();
                break;

            case WorldRuleType.Regeneration:
                ApplyRegeneration();
                break;

            default:
                break;
        }
    }

    private void ApplyWind()
    {
        float force = activeRule.WindForce;
        WindDirectionMode mode = activeRule.WindDirectionMode;

        if (force <= 0f || mode == WindDirectionMode.None)
            return;

        activeWindDirection = SelectWindDirection(activeRule, mode);
        activeWindVelocity = activeWindDirection * force;
        playerMovement?.SetWorldRuleExternalVelocity(activeWindVelocity);
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

        if (!modifier.TryBeginSpawnRoll() ||
            Random.value >= goldenEnemyChance)
        {
            return;
        }

        modifier.Apply(
            goldenEnemyHealthMultiplier,
            goldenEnemyRewardMultiplier
        );

#if UNITY_EDITOR
        if (logGoldenEnemyAssignments)
        {
            Debug.Log(
                $"[GoldenEnemy] Enemy='{enemy.name}' " +
                $"HealthMultiplier={goldenEnemyHealthMultiplier:F2} " +
                $"RewardMultiplier={goldenEnemyRewardMultiplier:F2}",
                enemy
            );
        }
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
    }

    private void ApplyHaste()
    {
        hasteApplied = true;
        ExperienceManager.Instance?.SetAnomalyXpGainMultiplier(
            activeRule.ExperienceGainMultiplier
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
            activeRule.OutgoingDamageMultiplier;
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

        if (activeRule.RuleType ==
            WorldRuleType.ExplosiveInfection)
        {
            enemy.OnDied += HandleEnemyDied;
        }

        if (activeRule.RuleType == WorldRuleType.Haste)
        {
            movement?.SetAnomalySpeedMultiplier(
                activeRule.EnemySpeedMultiplier
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
            activeRule == null ||
            activeRule.PlayerHealthPerSecond <= 0f ||
            playerHealth == null ||
            playerHealth.IsDead)
        {
            return;
        }

        regenerationTimer += Time.deltaTime;

        while (regenerationTimer >= 1f)
        {
            regenerationTimer -= 1f;
            playerHealth.Heal(activeRule.PlayerHealthPerSecond);
        }
    }

    private void HandleEnemyDied(EnemyHealth source)
    {
        if (source == null || activeRule == null)
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
            activeRule.ExplosionRadius
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
                    activeRule.PlayerExplosionDamage,
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

            if (!activeRule.AllowChainReaction)
                chainSuppressedEnemyIds.Add(enemyId);

            enemy.TakeDamage(activeRule.EnemyExplosionDamage, position);
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

    private void RestoreRuntimeEffects()
    {
        if (playerMovement != null)
            playerMovement.SetWorldRuleExternalVelocity(Vector2.zero);

        activeWindDirection = Vector2.zero;
        activeWindVelocity = Vector2.zero;

        if (playerMoveSpeedApplied && playerMovement != null)
            playerMovement.SetWorldRuleSpeedMultiplier(1f);

        if (enemyMoveSpeedApplied)
        {
            foreach (EnemyHealth enemy in registeredEnemies)
            {
                if (enemy == null)
                    continue;

                EnemyMovement movement = GetEnemyMovement(enemy);
                movement?.SetWorldRuleSpeedMultiplier(1f);
            }
        }

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

        regenerationApplied = false;
        hasteApplied = false;
        playerMoveSpeedApplied = false;
        enemyMoveSpeedApplied = false;
        regenerationTimer = 0f;
    }

    private void UnsubscribeEnemyLifecycle()
    {
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
