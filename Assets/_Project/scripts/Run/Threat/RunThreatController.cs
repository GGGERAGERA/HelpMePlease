using UnityEngine;

public sealed class RunThreatController : MonoBehaviour
{
    private RunThreatConfig config;
    private EnemySpawner enemySpawner;
    private int appliedPresetIndex = -1;
    private ThreatTier displayedTier;
    private float nextHudRefresh;
    private bool openingApplied;

    private void OnEnable() => EnemyHealth.Spawned += ApplyMovement;
    private void OnDisable()
    {
        EnemyHealth.Spawned -= ApplyMovement;
        foreach (var enemy in EnemyHealth.ActiveInstances)
            if (enemy != null && enemy.TryGetComponent<EnemyChaseMovement>(out var movement))
                movement.SetThreatMovement(1f, 0f);
    }

    private void ApplyMovement(EnemyHealth enemy)
    {
        if (config == null || enemy == null || enemy.IsBoss ||
            !enemy.TryGetComponent<EnemyChaseMovement>(out var movement)) return;
        var run = RunStateManager.Instance;
        if (run != null) movement.SetThreatMovement(config.MeleeSpeed(run.ThreatValue),
            config.MeleeResponseSeconds(run.ThreatValue));
    }

    public ThreatTier DisplayedTier => displayedTier;
    public int AppliedPresetIndex => appliedPresetIndex;

    public void Initialize(RunThreatConfig threatConfig, EnemySpawner spawner)
    {
        config = threatConfig;
        enemySpawner = spawner;
        ApplyCurrentPreset(true);
        foreach (var enemy in EnemyHealth.ActiveInstances) ApplyMovement(enemy);
    }

    private void Update()
    {
        RunStateManager runState = RunStateManager.Instance;

        if (config == null || runState == null || runState.IsRunEnded ||
            Time.timeScale == 0f)
        {
            return;
        }

        runState.AdvanceThreat(Time.deltaTime, config.ValuePerSecond);
        ApplyCurrentPreset(false);
        foreach (var enemy in EnemyHealth.ActiveInstances) ApplyMovement(enemy);
    }

    private void ApplyCurrentPreset(bool force)
    {
        RunStateManager runState = RunStateManager.Instance;

        if (config == null || runState == null)
            return;

        ThreatTier currentTier = ThreatTierPresentation.FromPressure(
            runState.ThreatValue
        );
        int presetIndex = config.GetPresetIndex(runState.ThreatValue);
        bool opening = config.IsOpening(runState.ThreatValue, runState.ThreatElapsedTime);

        if (!force && presetIndex == appliedPresetIndex && opening == openingApplied)
        {
            if (Time.unscaledTime >= nextHudRefresh)
            {
                nextHudRefresh = Time.unscaledTime + 0.2f;
                HUDManager.Instance?.SetThreat(
                    runState.ThreatValue,
                    currentTier
                );
            }

            if (currentTier > displayedTier)
                ShowTierIncrease(currentTier);

            displayedTier = currentTier;
            return;
        }

        appliedPresetIndex = presetIndex;
        openingApplied = opening;
        RunThreatConfig.Preset preset = config.GetPreset(presetIndex);

        if (preset != null)
        {
            enemySpawner?.SetRunThreatPreset(
                presetIndex,
                preset.spawnIntervalMultiplier * (opening ? config.OpeningSpawnIntervalMultiplier : 1f),
                opening ? Mathf.Min(preset.maxAliveCap, config.OpeningAliveCap) : preset.maxAliveCap,
                preset.batchSize
            );
        }

        HUDManager.Instance?.SetThreat(runState.ThreatValue, currentTier);
        nextHudRefresh = Time.unscaledTime + 0.2f;

        if (!force && currentTier > displayedTier)
            ShowTierIncrease(currentTier);

        displayedTier = currentTier;
    }

    private static void ShowTierIncrease(ThreatTier tier)
    {
        RunMessageService.Instance?.ShowCustom(
            "run.threatIncreased",
            "run.pressureIncreased",
            1.8f
        );
    }
}
