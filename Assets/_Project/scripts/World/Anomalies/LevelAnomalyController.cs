using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelAnomalyController : MonoBehaviour
{
    public static LevelAnomalyController Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private LevelAnomalyData[] availableAnomalies;

    [Header("View")]
    [SerializeField] private LevelAnomalyView view;

    [Header("Explosion")]
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField, Min(0.1f)] private float explosionFxLifetime = 0.8f;

    private readonly HashSet<EnemyHealth> registeredEnemies = new();
    private readonly HashSet<int> explodedEnemyIds = new();
    private readonly HashSet<int> chainSuppressedEnemyIds = new();

    private LevelAnomalyData activeAnomaly;
    private PlayerHealth playerHealth;
    private PlayerCombatModifiers playerModifiers;
    private float originalIncomingDamageMultiplier = 1f;
    private float originalOutgoingDamageMultiplier = 1f;
    private float previousTimeScale = 1f;
    private bool levelStarted;
    private bool enemyLifecycleSubscribed;
    private bool berserkApplied;

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
                ApplyBerserk();
                break;
        }
    }

    private void ApplyBerserk()
    {
        berserkApplied = true;

        if (playerModifiers != null)
        {
            originalOutgoingDamageMultiplier =
                playerModifiers.bonusDamageMultiplier;
            playerModifiers.bonusDamageMultiplier =
                originalOutgoingDamageMultiplier *
                activeAnomaly.OutgoingDamageMultiplier;
        }

        if (playerHealth != null)
        {
            originalIncomingDamageMultiplier =
                playerHealth.IncomingDamageMultiplier;
            playerHealth.SetIncomingDamageMultiplier(
                originalIncomingDamageMultiplier *
                activeAnomaly.IncomingDamageMultiplier
            );
        }
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

        enemy.OnDied += HandleEnemyDied;
    }

    private void UnregisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || !registeredEnemies.Remove(enemy))
            return;

        enemy.OnDied -= HandleEnemyDied;
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
        if (!berserkApplied)
            return;

        if (playerModifiers != null)
            playerModifiers.bonusDamageMultiplier =
                originalOutgoingDamageMultiplier;

        if (playerHealth != null)
            playerHealth.SetIncomingDamageMultiplier(
                originalIncomingDamageMultiplier
            );

        berserkApplied = false;
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
