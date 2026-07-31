using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldRuleController : MonoBehaviour
{
    public static WorldRuleController Instance { get; private set; }

    [Header("World Rules")]
    [SerializeField] private bool enableWorldRules;
    [SerializeField] private WorldAccelerationRule worldAccelerationRule;
    [SerializeField] private WorldRuleData[] availableRules;

    [Header("View")]
    [SerializeField] private LevelAnomalyView view;
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
    private float previousTimeScale = 1f;
    private float regenerationTimer;
    private bool levelStarted;
    private bool enemyLifecycleSubscribed;
    private bool regenerationApplied;
    private bool hasteApplied;
    private bool introPauseApplied;

    public WorldRuleData ActiveRule => activeRule;
    public bool IsIntroComplete { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        previousTimeScale = Time.timeScale;
    }

    public void BeginLevel(LevelNodeData level)
    {
        if (levelStarted)
            return;

        levelStarted = true;
        worldRuleVisual?.SetSnowActive(
            level != null &&
            level.weatherType == LevelWeatherType.Snow
        );
        ResolveWorldAccelerationRule();
        ApplyConfiguredWorldAcceleration(level);

        if (!enableWorldRules)
        {
            worldRuleVisual?.ClearRuleImmediate();
            IsIntroComplete = true;
            return;
        }

        activeRule = SelectGlobalRule();

        if (activeRule == null)
        {
            worldRuleVisual?.ClearRuleImmediate();
            IsIntroComplete = true;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerModifiers = player.GetComponent<PlayerCombatModifiers>();
        }

        if (!UsesGlobalIntro(activeRule.RuleType))
        {
            Debug.LogWarning(
                "[WorldRuleController] Selected data " +
                $"'{activeRule.name}' is not a global world rule. " +
                "Continuing without that rule.",
                this
            );
            activeRule = null;
            worldRuleVisual?.ClearRuleImmediate();
            IsIntroComplete = true;
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        introPauseApplied = true;
        worldRuleVisual?.ShowRule(activeRule.RuleType);
        ApplyGlobalGameplayEffect();
        StartCoroutine(IntroRoutine(level));
    }

    private void ApplyConfiguredWorldAcceleration(LevelNodeData level)
    {
        if (enableWorldRules &&
            level != null &&
            level.hasWorldAccelerationRule)
        {
            worldAccelerationRule?.StartRule();
        }
        else
        {
            worldAccelerationRule?.StopRule();
        }
    }

    private void ResolveWorldAccelerationRule()
    {
        if (worldAccelerationRule == null)
        {
            worldAccelerationRule =
                FindFirstObjectByType<WorldAccelerationRule>();
        }
    }

    private WorldRuleData SelectGlobalRule()
    {
        if (availableRules == null || availableRules.Length == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < availableRules.Length; i++)
        {
            WorldRuleData rule = availableRules[i];

            if (rule != null && UsesGlobalIntro(rule.RuleType))
                totalWeight += rule.SelectionWeight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;
        WorldRuleData lastEligible = null;

        for (int i = 0; i < availableRules.Length; i++)
        {
            WorldRuleData rule = availableRules[i];

            if (rule == null || !UsesGlobalIntro(rule.RuleType))
                continue;

            lastEligible = rule;
            roll -= rule.SelectionWeight;

            if (roll <= 0f)
                return rule;
        }

        return lastEligible;
    }

    private static bool UsesGlobalIntro(WorldRuleType type)
    {
        switch (type)
        {
            case WorldRuleType.ExplosiveInfection:
            case WorldRuleType.Haste:
            case WorldRuleType.Regeneration:
                return true;

            default:
                return false;
        }
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

    private IEnumerator IntroRoutine(LevelNodeData level)
    {
        if (view != null)
            yield return view.PlayIntro(
                level,
                activeRule.Presentation
            );

        FinishIntroPause();
    }

    private void FinishIntroPause()
    {
        IsIntroComplete = true;

        if (introPauseApplied &&
            Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = previousTimeScale;
        }

        introPauseApplied = false;
    }

    private void RestoreRuntimeEffects()
    {
        worldAccelerationRule?.StopRule();

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

        RestoreRuntimeEffects();
        worldRuleVisual?.ClearImmediate();

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

        if (introPauseApplied &&
            Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = previousTimeScale;
        }

        introPauseApplied = false;

        if (Instance == this)
            Instance = null;
    }
}
