using System.Collections.Generic;
using UnityEngine;

public enum AssaultEventType { BomberRush, ShooterSquad, Encirclement, Crossfire, Stampede }

public partial class EnemySpawner
{
    [Header("Assault Events (production prototype)")]
    [SerializeField] private bool assaultEventsEnabled = true;
    [SerializeField] private Vector2 firstAssaultDelay = new(70f, 80f);
    [Tooltip("Gameplay seconds between event starts; also guarantees a cooldown after the assault window.")]
    [SerializeField] private Vector2 assaultInterval = new(110f, 130f);
    [SerializeField, Min(1f)] private float assaultDuration = 24f;
    [SerializeField, Min(0f)] private float assaultBreathDuration = 8f;
    [SerializeField, Min(0.1f)] private float assaultRecoveryDuration = 6f;
    [SerializeField, Range(0.1f, 1f)] private float assaultNormalRefillMultiplier = 0.5f;
    [SerializeField, Min(0.5f)] private float assaultScreenPadding = 2f;
    [SerializeField, Min(1f)] private float assaultMinDistance = 12f;
    [SerializeField, Min(0.5f)] private float assaultSpacing = 1.25f;
    [SerializeField] private Vector2Int bomberRushCount = new(8, 16);
    [SerializeField] private Vector2Int shooterSquadCount = new(6, 10);
    [SerializeField] private Vector2Int encirclementCount = new(12, 20);
    [SerializeField] private Vector2Int crossfireCountPerSide = new(3, 5);
    [SerializeField] private Vector2Int stampedeCount = new(20, 30);
    [Tooltip("Optional existing prefab overrides; otherwise resolved from the production spawn profile.")]
    [SerializeField] private GameObject assaultBasicPrefab;
    [SerializeField] private GameObject assaultFastPrefab;
    [SerializeField] private GameObject assaultBomberPrefab;
    [SerializeField] private GameObject assaultShooterPrefab;

    private readonly List<SpawnedEnemy> assaultEnemies = new();
    private readonly List<EnemyHealth> currentAssault = new();
    // Separate RNG: formations must not consume the Threat/normal spawn random stream.
    private readonly System.Random assaultRandom = new();
    private float assaultElapsed, assaultNextAt, assaultRemaining;
    private AssaultEventType? pendingAssault;
    private bool pendingAssaultIsAutomatic;
    private enum FirstAutomaticAssaultProgress { NotStarted, Active, Recovering, Complete }
    private FirstAutomaticAssaultProgress firstAutomaticAssault;
    public bool HasStartedFirstAutomaticAssault => firstAutomaticAssault != FirstAutomaticAssaultProgress.NotStarted;
    public bool HasRecoveredFromFirstAutomaticAssault => firstAutomaticAssault == FirstAutomaticAssaultProgress.Complete;
    private float breathRemaining, recoveryRemaining;
    public bool IsAssaultBreathing => pendingAssault.HasValue;
    public float NormalRefillMultiplier => IsAssaultBreathing ? 0f :
        IsAssaultActive ? assaultNormalRefillMultiplier :
        Mathf.Lerp(1f, assaultNormalRefillMultiplier,
            Mathf.Clamp01(recoveryRemaining / assaultRecoveryDuration));
    public bool IsAssaultActive => assaultRemaining > 0f;
    public AssaultEventType? ActiveAssault { get; private set; }
    public bool CanStartAssault => isActiveAndEnabled && spawningEnabled &&
        Time.timeScale > 0f && !SceneTransitionOverlay.IsTransitioning &&
        (UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle) &&
        (RunStateManager.Instance == null || !RunStateManager.Instance.IsRunEnded) &&
        RunFlowController.Instance != null &&
        RunFlowController.Instance.Phase == RunPhase.NormalSector && !RunFlowController.Instance.IsLevelCompleted &&
        player != null && player.TryGetComponent<PlayerHealth>(out var health) && !health.IsDead;

    private float NextAssaultInterval() => Mathf.Max(assaultDuration + 5f,
        Mathf.Lerp(Mathf.Max(1f, assaultInterval.x), Mathf.Max(assaultInterval.x, assaultInterval.y),
            (float)assaultRandom.NextDouble()));

    private void ResetAssaultEvents()
    {
        EndAssault();
        firstAutomaticAssault = FirstAutomaticAssaultProgress.NotStarted;
        pendingAssault = null;
        breathRemaining = recoveryRemaining = 0f;
        assaultElapsed = 0f;
        assaultNextAt = Mathf.Max(assaultBreathDuration,
            Mathf.Lerp(firstAssaultDelay.x, firstAssaultDelay.y, (float)assaultRandom.NextDouble()));
    }

    private void EndAssault(bool completed = false)
    {
        // Stop/reset cancels a wave; only the normal completion path satisfies the exit gate.
        if (completed && firstAutomaticAssault == FirstAutomaticAssaultProgress.Active)
            firstAutomaticAssault = FirstAutomaticAssaultProgress.Recovering;
        else if (!completed && firstAutomaticAssault != FirstAutomaticAssaultProgress.Complete)
            firstAutomaticAssault = FirstAutomaticAssaultProgress.NotStarted;
        if (ActiveAssault.HasValue)
        {
            recoveryRemaining = assaultRecoveryDuration;
            spawnTimer = 0f;
        }
        foreach (var enemy in currentAssault)
            if (enemy != null && enemy.TryGetComponent<EnemyChaseMovement>(out var chase))
                chase.SetAssaultDestination(null);
        currentAssault.Clear();
        assaultRemaining = 0f;
        ActiveAssault = null;
    }

    private void UpdateAssaultEvents()
    {
        if (!CanStartAssault) return;
        assaultElapsed += Time.deltaTime;
        recoveryRemaining = Mathf.Max(0f, recoveryRemaining - Time.deltaTime);
        if (firstAutomaticAssault == FirstAutomaticAssaultProgress.Recovering && recoveryRemaining <= 0f)
            firstAutomaticAssault = FirstAutomaticAssaultProgress.Complete;
        assaultEnemies.RemoveAll(e => e.instance == null);
        if (IsAssaultActive)
        {
            assaultRemaining -= Time.deltaTime;
            currentAssault.RemoveAll(e => e == null || e.IsDead);
            if (assaultRemaining <= 0f || currentAssault.Count == 0) EndAssault(completed: true);
        }
        if (pendingAssault.HasValue)
        {
            breathRemaining -= Time.deltaTime;
            if (breathRemaining > 0f) return;
            var type = pendingAssault.Value;
            pendingAssault = null;
            if (!TryStartAssault(type, pendingAssaultIsAutomatic))
            {
                // Resume refill if geometry is blocked; retry with a fresh breath later.
                recoveryRemaining = assaultRecoveryDuration;
                assaultNextAt = assaultElapsed + 5f + assaultBreathDuration;
            }
            return;
        }
        if (!assaultEventsEnabled || IsAssaultActive || recoveryRemaining > 0f ||
            assaultElapsed < assaultNextAt - assaultBreathDuration) return;
        BeginAssaultBreath(automatic: true);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Force bypasses only warmup/cooldown, never pause/reward/transition or the single-event gate.
    public bool ForceAssaultEvent(AssaultEventType type) => TryStartAssault(type);
#endif

    // True acknowledges ownership of the request, including its preparation window.
    public bool TryStartRandomSiteAssault()
    {
        if (!CanStartAssault || IsAssaultActive || IsAssaultBreathing || recoveryRemaining > 0f) return false;
        BeginAssaultBreath(automatic: false);
        return true;
    }

    private void BeginAssaultBreath(bool automatic)
    {
        pendingAssault = (AssaultEventType)assaultRandom.Next(5);
        pendingAssaultIsAutomatic = automatic;
        breathRemaining = assaultBreathDuration;
        spawnTimer = 0f;
    }

    private bool TryStartAssault(AssaultEventType type, bool automatic = false)
    {
        if (!CanStartAssault || IsAssaultActive || (int)type < 0 || (int)type > 4) return false;
        GameObject prefab = ResolveAssaultPrefab(type);
        Camera camera = Camera.main;
        ResolveGameplayArea();
        if (prefab == null || camera == null || gameplayArea == null) return false;
        Vector2Int range = type switch
        {
            AssaultEventType.BomberRush => bomberRushCount,
            AssaultEventType.ShooterSquad => shooterSquadCount,
            AssaultEventType.Encirclement => encirclementCount,
            AssaultEventType.Crossfire => crossfireCountPerSide,
            _ => stampedeCount
        };
        float size = RunStateManager.Instance.CurrentSector.StageProfile.AssaultSizeMultiplier;
        int minimum = Mathf.Max(1, Mathf.RoundToInt(range.x * size));
        int maximum = Mathf.Max(minimum, Mathf.RoundToInt(range.y * size));
        int count = assaultRandom.Next(minimum, maximum + 1);
        if (type == AssaultEventType.Crossfire) count *= 2;
        var positions = new List<Vector3>(count);
        Vector2 flow = Vector2.zero;
        int firstSide = assaultRandom.Next(4);
        bool valid = false;
        for (int attempt = 0; attempt < 4 && !valid; attempt++)
            valid = BuildAssaultFormation(type, count, camera, (firstSide + attempt) % 4, positions, out flow);
        if (!valid) return false;

        string title = type switch
        {
            AssaultEventType.BomberRush => "world.message.bomber",
            AssaultEventType.ShooterSquad => "world.message.shooter",
            AssaultEventType.Encirclement => "world.message.encirclement",
            AssaultEventType.Crossfire => "world.message.crossfire",
            _ => "world.message.stampede"
        };
        RunMessageService.Instance?.ShowCustom(title, string.Empty, 2.5f);
        ActiveAssault = type;
        assaultRemaining = Mathf.Max(1f, assaultDuration);
        if (automatic && !HasStartedFirstAutomaticAssault)
            firstAutomaticAssault = FirstAutomaticAssaultProgress.Active;
        // A site wave cannot consume or postpone the sector's first automatic slot.
        if (HasStartedFirstAutomaticAssault)
            assaultNextAt = assaultElapsed + NextAssaultInterval();
        foreach (Vector3 position in positions)
        {
            GameObject enemy = SpawnEnemyAt(prefab, position, false);
            EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
            assaultEnemies.Add(new SpawnedEnemy { instance = enemy, movement = movement,
                baseSpeedMultiplier = currentSpeedMultiplier * (activePhase != null ? activePhase.speedMultiplier : 1f) });
            currentAssault.Add(enemy.GetComponent<EnemyHealth>());
            if (type == AssaultEventType.Stampede && movement is EnemyChaseMovement chase)
            {
                // Parallel lanes through the player's position at launch, then normal chase resumes.
                float travel = Vector2.Dot((Vector2)player.position - (Vector2)position, flow) * 2f;
                chase.SetAssaultDestination((Vector2)position + flow * Mathf.Max(4f, travel));
            }
        }
        Debug.Log($"[AssaultEvents] {type}: spawned {count} production enemies.", this);
        return true;
    }

    private bool BuildAssaultFormation(AssaultEventType type, int count, Camera camera,
        int side, List<Vector3> positions, out Vector2 flow)
    {
        positions.Clear();
        Vector2 center = player.position;
        float depth = Mathf.Abs(player.position.z - camera.transform.position.z);
        Vector2 low = camera.ViewportToWorldPoint(new Vector3(0, 0, depth));
        Vector2 high = camera.ViewportToWorldPoint(new Vector3(1, 1, depth));
        float padding = Mathf.Max(.5f, assaultScreenPadding);
        float spacing = Mathf.Max(.5f, assaultSpacing);
        Vector2 outward = side switch { 0 => Vector2.left, 1 => Vector2.right, 2 => Vector2.up, _ => Vector2.down };
        flow = -outward;
        float ringRadius = Mathf.Max(assaultMinDistance,
            new Vector2(Mathf.Max(Mathf.Abs(low.x - center.x), Mathf.Abs(high.x - center.x)),
                Mathf.Max(Mathf.Abs(low.y - center.y), Mathf.Abs(high.y - center.y))).magnitude + padding);
        for (int i = 0; i < count; i++)
        {
            Vector2 point;
            if (type == AssaultEventType.Encirclement)
            {
                float angle = Mathf.PI * 2f * i / count;
                point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
            }
            else
            {
                bool crossfire = type == AssaultEventType.Crossfire;
                Vector2 normal = crossfire ? (i < count / 2 ? Vector2.left : Vector2.right) : outward;
                Vector2 tangent = new(-normal.y, normal.x);
                float edge = normal.x < 0 ? center.x - low.x : normal.x > 0 ? high.x - center.x :
                    normal.y < 0 ? center.y - low.y : high.y - center.y;
                float distance = Mathf.Max(assaultMinDistance, edge + padding);
                int columns = crossfire ? count / 2 : type == AssaultEventType.ShooterSquad ? count : 5;
                int index = crossfire ? i % columns : i;
                point = center + normal * (distance + index / columns * spacing) +
                    tangent * ((index % columns - (columns - 1) * .5f) * spacing);
            }
            if (!gameplayArea.IsInsideSpawnArea(point, .5f)) return false;
            positions.Add(new Vector3(point.x, point.y, player.position.z));
        }
        return true;
    }

    private GameObject ResolveAssaultPrefab(AssaultEventType type)
    {
        GameObject selected = type switch
        {
            AssaultEventType.BomberRush => assaultBomberPrefab,
            AssaultEventType.ShooterSquad or AssaultEventType.Crossfire => assaultShooterPrefab,
            AssaultEventType.Encirclement => assaultFastPrefab,
            _ => assaultBasicPrefab
        };
        if (selected != null) return selected;
        void Consider(GameObject candidate)
        {
            if (candidate == null || candidate.GetComponent<EnemyHealth>() == null) return;
            if (type == AssaultEventType.BomberRush)
            { if (selected == null && candidate.GetComponent<EnemyBomberMovement>() != null) selected = candidate; return; }
            if (type == AssaultEventType.ShooterSquad || type == AssaultEventType.Crossfire)
            { if (selected == null && candidate.GetComponent<EnemyShooterMovement>() != null) selected = candidate; return; }
            var chase = candidate.GetComponent<EnemyChaseMovement>();
            var identity = candidate.GetComponent<EnemyIdentity>();
            if (chase == null || candidate.GetComponent<EnemyHealth>().IsBoss ||
                (identity != null && !string.IsNullOrEmpty(identity.EnemyId) &&
                 identity.EnemyId.StartsWith("Elite_", System.StringComparison.OrdinalIgnoreCase))) return;
            if (selected == null || type == AssaultEventType.Encirclement &&
                chase.BaseChaseSpeed > selected.GetComponent<EnemyChaseMovement>().BaseChaseSpeed) selected = candidate;
        }
        if (spawnProfile != null)
            foreach (var phase in spawnProfile.Phases)
                if (phase?.enemies != null) foreach (var entry in phase.enemies) Consider(entry?.enemyPrefab);
        if (enemyPrefabs != null) foreach (var prefab in enemyPrefabs) Consider(prefab);
        if (spawnStages != null) foreach (var stage in spawnStages)
            if (stage?.enemyPrefabs != null) foreach (var prefab in stage.enemyPrefabs) Consider(prefab);
        return selected;
    }

    private void RefreshAssaultEnemySpeeds()
    {
        assaultEnemies.RemoveAll(e => e.instance == null);
        foreach (var enemy in assaultEnemies)
            if (enemy.movement != null)
                enemy.movement.SetSpeedMultiplier(enemy.baseSpeedMultiplier * worldAccelerationMultiplier);
    }
}
