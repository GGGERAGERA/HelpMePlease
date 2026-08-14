using UnityEngine;

public sealed class RunThreatController : MonoBehaviour
{
    private RunThreatConfig config;
    private EnemySpawner enemySpawner;
    private int appliedPresetIndex = -1;
    private ThreatTier displayedTier;
    private float nextHudRefresh;

    public ThreatTier DisplayedTier => displayedTier;
    public int AppliedPresetIndex => appliedPresetIndex;

    public void Initialize(RunThreatConfig threatConfig, EnemySpawner spawner)
    {
        config = threatConfig;
        enemySpawner = spawner;
        ApplyCurrentPreset(true);
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

        if (!force && presetIndex == appliedPresetIndex)
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
        RunThreatConfig.Preset preset = config.GetPreset(presetIndex);

        if (preset != null)
        {
            enemySpawner?.SetRunThreatPreset(
                presetIndex,
                preset.spawnIntervalMultiplier,
                preset.maxAliveCap,
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
            $"THREAT {ThreatTierPresentation.Format(tier)}",
            "ENEMY PRESSURE INCREASED",
            1.8f
        );
    }
}
