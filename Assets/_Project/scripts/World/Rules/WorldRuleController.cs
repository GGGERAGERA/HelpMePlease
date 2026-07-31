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

    private readonly HashSet<EnemyHealth> registeredEnemies = new();
    private readonly HashSet<int> explodedEnemyIds = new();
    private readonly HashSet<int> chainSuppressedEnemyIds = new();

    private WorldRuleData activeRule;
    private PlayerHealth playerHealth;
    private PlayerCombatModifiers playerModifiers;
    private float originalOutgoingDamageMultiplier = 1f;
    private float regenerationTimer;
    private bool enemyLifecycleSubscribed;
    private bool regenerationApplied;
    private bool hasteApplied;

    public WorldRuleData ActiveRule => activeRule;
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
        ApplyGlobalGameplayEffect();
        worldRuleVisual?.Apply(rule);
    }

    public void Clear()
    {
        RestoreRuntimeEffects();
        UnsubscribeEnemyLifecycle();

        activeRule = null;
        playerHealth = null;
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
        playerModifiers = player.GetComponent<PlayerCombatModifiers>();
    }

    private void ApplyGlobalGameplayEffect()
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

            case WorldRuleType.None:
            case WorldRuleType.Snow:
            case WorldRuleType.Rain:
            case WorldRuleType.Darkness:
            case WorldRuleType.Wind:
            case WorldRuleType.Golden:
                // Gameplay for migrated World Rules is enabled in a later stage.
                break;
        }
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

        if (activeRule.RuleType ==
            WorldRuleType.ExplosiveInfection)
        {
            enemy.OnDied += HandleEnemyDied;
        }

        if (activeRule.RuleType == WorldRuleType.Haste)
        {
            EnemyMovement movement = GetEnemyMovement(enemy);
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
