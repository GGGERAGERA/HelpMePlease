using UnityEngine;

public sealed class RunThreatController : MonoBehaviour
{
    private RunThreatConfig config;
    private EnemySpawner enemySpawner;
    private int appliedPresetIndex = -1;
    private float nextHudRefresh;

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

        int presetIndex = config.GetPresetIndex(runState.ThreatValue);

        if (!force && presetIndex == appliedPresetIndex)
        {
            if (Time.unscaledTime >= nextHudRefresh)
            {
                nextHudRefresh = Time.unscaledTime + 0.2f;
                HUDManager.Instance?.SetThreat(
                    runState.ThreatValue,
                    presetIndex + 1
                );
            }
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

        HUDManager.Instance?.SetThreat(runState.ThreatValue, presetIndex + 1);
        nextHudRefresh = Time.unscaledTime + 0.2f;

        if (!force)
        {
            RunMessageService.Instance?.ShowCustom(
                $"THREAT {ToRoman(presetIndex + 1)}",
                "ENEMY PRESSURE INCREASED",
                1.8f
            );
        }
    }

    private static string ToRoman(int level)
    {
        return level switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => "VI"
        };
    }
}
