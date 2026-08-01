using System.Collections.Generic;
using UnityEngine;

public sealed class WorldRuleController : MonoBehaviour
{
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
        ApplySpawnPressure();
        worldRuleVisual?.Apply(rule);
        ApplyWind();
    }

    public void Clear()
    {
        RestoreSpawnPressure();
        RestoreRuntimeEffects();
        StopGoldenEnemyAssignment();
        UnsubscribeEnemyLifecycle();

        activeRule = null;
        playerMovement = null;
        worldRuleVisual?.Clear();
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

        if (!Mathf.Approximately(enemyMultiplier, 1f))
        {
            enemyMoveSpeedApplied = true;
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

    }

    private void UnregisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || !registeredEnemies.Remove(enemy))
            return;

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

        playerMoveSpeedApplied = false;
        enemyMoveSpeedApplied = false;
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
