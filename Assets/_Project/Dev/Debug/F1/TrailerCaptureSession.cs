#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

// A scene-scoped owner for F1 capture overrides. Combat stays in production services.
public sealed class TrailerCaptureSession : MonoBehaviour
{
    public TrailerCaptureConfig Config { get; private set; }
    public bool HasSettings => run != null;
    public string ResetUnavailableReason => !HasSettings ? "Нет применённых настроек" : UnavailableReason;
    public string Status { get; private set; } = "Настройки действуют только в этом ране";
    private RunStateManager run;
    private int runId;
    private string orbitalSnapshot;
    private float healthSnapshot, maxHealthSnapshot, threatSnapshot, threatElapsedSnapshot;
    private float densitySnapshot;
    private WorldRuleData worldRuleSnapshot;
    private bool worldRuleChanged;
    private bool spawnSnapshot, hudSnapshot, numbersSnapshot, invulnerabilitySnapshot;
    private bool buildChanged;
    private bool playerMoved;
    private Vector3 playerPositionSnapshot;
    private OrbitalStationRuntime.RmbMode rightMouseSnapshot;
    private PlayerHealth health;
    private OrbitalStationRuntime station;
    private EnemySpawner enemies;
    private WorldEventSpawner events;
    private RunFlowController flow;
    private HUDManager hud;
    private WorldEvent ownedEvent;
    private WorldRuleController worldRules;
    private LevelModifiersApplier modifiers;

    private void Awake() => Config = Resources.Load<TrailerCaptureConfig>("TrailerCapturePresets");
    private void Resolve()
    {
        var player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        health = player != null ? player.GetComponent<PlayerHealth>() : null;
        station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
        enemies = FindFirstObjectByType<EnemySpawner>();
        events = FindFirstObjectByType<WorldEventSpawner>();
        flow = RunFlowController.Instance;
        hud = HUDManager.Instance;
        worldRules = WorldRuleController.Instance;
        modifiers = FindFirstObjectByType<LevelModifiersApplier>();
    }

    public string UnavailableReason
    {
        get
        {
            Resolve();
            var current = RunStateManager.Instance;
            if (current == null || current.IsRunEnded || health == null || health.IsDead) return "Сначала запустите ран с живым игроком";
            if (station == null || !station.IsInitialized) return "ORBITAL ещё не готов";
            if (SceneTransitionOverlay.IsTransitioning) return "Дождитесь перехода сцены";
            if (TutorialController.IsTutorialSector) return "Сначала завершите обучение";
            if (UpgradeManager.Instance != null && !UpgradeManager.Instance.IsRewardQueueIdle) return "Закройте выбор награды";
            if (station.InputOwner == null || !station.InputOwner.CanTransition) return "Завершите действие с орбитой";
            if (flow == null || (flow.Phase != RunPhase.NormalSector && !flow.IsFilmingBoss)) return "Сейчас идёт завершение сектора или обычный бой с боссом";
            return null;
        }
    }

    public string PresetUnavailableReason(TrailerCaptureConfig.Preset preset)
    {
        string reason = UnavailableReason;
        if (reason != null) return reason;
        if (preset == null) return "Нет данных пресета";
        if (!preset.TryBuild(RunStateManager.Instance.OrbitalStationState.RunId, out _, out string error)) return error;
        if (enemies == null || !enemies.isActiveAndEnabled) return "Спавнер врагов не готов";
        if (preset.shot == TrailerCaptureConfig.Shot.Boss) return flow.FilmingBossUnavailableReason;
        if (preset.shot == TrailerCaptureConfig.Shot.RocketStorm)
        {
            if (events == null || !events.isActiveAndEnabled || Config.rocketStormPrefab == null) return "Rocket Storm не настроен в этой сцене";
            if (!Config.rocketStormPrefab.AvailableInProduction) return "Rocket Storm отключён в production";
            if (!Config.rocketStormPrefab.TryValidateConfiguration(out error)) return error;
            if (events.ActiveEvent != null && events.ActiveEvent != ownedEvent) return "Сначала завершите или очистите активное событие";
        }
        return null;
    }

    private bool Capture()
    {
        if (UnavailableReason != null) return false;
        if (run != null) return run.RunId == runId;
        run = RunStateManager.Instance;
        runId = run.RunId;
        orbitalSnapshot = JsonUtility.ToJson(run.OrbitalStationState);
        healthSnapshot = health.CurrentHealth;
        playerPositionSnapshot = health.transform.position;
        maxHealthSnapshot = health.MaxHealth;
        threatSnapshot = run.ThreatValue;
        threatElapsedSnapshot = run.ThreatElapsedTime;
        spawnSnapshot = enemies != null && enemies.IsSpawningEnabled;
        densitySnapshot = enemies != null ? enemies.DebugCaptureDensity : 1f;
        worldRuleSnapshot = worldRules != null ? worldRules.ActiveRule : null;
        hudSnapshot = hud == null || hud.DebugHudVisible;
        numbersSnapshot = DamagePopup.DebugDamageNumbersVisible;
        invulnerabilitySnapshot = health.DebugInvulnerable;
        rightMouseSnapshot = station.RightMouseMode;
        run.DebugMarkDevelopmentRun();
        run.RegisterSceneCleanup(Release);
        return true;
    }

    public bool Apply(TrailerCaptureConfig.Preset preset)
    {
        string reason = PresetUnavailableReason(preset);
        if (reason != null) { Status = reason; return false; }
        if (!preset.TryBuild(RunStateManager.Instance.OrbitalStationState.RunId, out var candidate, out reason) || !Capture())
        { Status = reason ?? "Сессия съёмки недоступна"; return false; }
        ClearOwnedEncounter();
        if (preset.shot == TrailerCaptureConfig.Shot.RocketStorm || preset.shot == TrailerCaptureConfig.Shot.Boss)
            events?.ClearAllDebugEvents();
        bool spawningBeforeApply = enemies.IsSpawningEnabled;
        enemies.StopSpawning();
        DespawnOrdinaryEnemies();
        var previousState = run.OrbitalStationState;
        station.Teardown();
        if (!run.DebugReplaceOrbitalState(candidate) || !station.RebuildRuntimeFromState())
        {
            run.DebugReplaceOrbitalState(previousState);
            bool restored = station.RebuildRuntimeFromState();
            if (restored && spawningBeforeApply) enemies.ResumeSpawning();
            Status = restored ? "Пресет не применён; предыдущий ORBITAL-билд восстановлен" : "ORBITAL не удалось восстановить; проверьте ссылки сцены";
            return false;
        }
        buildChanged = true;
        health.Heal(health.MaxHealth);
        run.SetThreatForDebug(preset.threat);
        FindFirstObjectByType<RunThreatController>()?.DebugRefreshPreset();
        station.RightMouseMode = OrbitalStationRuntime.RmbMode.BulletTime;
        if (preset.shot == TrailerCaptureConfig.Shot.Boss)
        {
            if (!flow.DebugStartFilmingBoss()) { Status = "Бой с боссом сейчас недоступен"; return false; }
        }
        else
        {
            enemies.ResumeSpawning();
            var basic = enemies.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Basic);
            var shooter = enemies.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Shooter);
            for (int i = 0; i < Mathf.Min(Mathf.Clamp(preset.openingEnemies, 0, 10), enemies.DebugCaptureMaxAlive); i++)
                enemies.SpawnSpecificEnemyAround(i % 3 == 2 && shooter != null ? shooter : basic,
                    health.transform.position, 8f, 13f, 5f);
            if (preset.shot == TrailerCaptureConfig.Shot.RocketStorm && !StartRocketStorm()) return false;
        }
        Status = $"Применён: {preset.label}. Закройте F1 и включите запись в OBS";
        return true;
    }

    private bool StartRocketStorm()
    {
        // Placement uses the real territory/obstacle validator; try nearby open areas.
        Vector3 origin = health.transform.position;
        for (int i = 0; i < 17; i++)
        {
            Vector3 offset = i == 0 ? Vector3.zero : Quaternion.Euler(0, 0, (i - 1) * 45f) * Vector3.right * (i <= 8 ? 10f : 20f);
            if (!events.SpawnDebugEventAt(Config.rocketStormPrefab, origin + offset, true, out var instance)) continue;
            ownedEvent = instance;
            // Move to the authored launch area and start through its ordinary owner.
            health.transform.position = instance.transform.position;
            playerMoved = true;
            Physics2D.SyncTransforms();
            instance.StartEvent();
            if (instance.IsStarted) return true;
            events.ClearDebugEvent(instance);
            ownedEvent = null;
            health.transform.position = origin;
            break;
        }
        Status = "Для Rocket Storm не нашлось свободной зоны; переместитесь на открытое место";
        return false;
    }

    private void ClearOwnedEncounter()
    {
        flow?.DebugResetFilmingBoss();
        if (ownedEvent != null) events?.ClearDebugEvent(ownedEvent);
        ownedEvent = null;
    }

    private void DespawnOrdinaryEnemies()
    {
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray())
            if (enemy != null && !enemy.IsBoss && enemy.gameObject.scene == gameObject.scene)
            {
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
        enemies?.ClearDebugSpawnedEnemies();
    }

    public void Heal() { if (Capture()) health.Heal(health.MaxHealth); }
    public bool PrepareBuildEdit()
    {
        if (!Capture()) return false;
        buildChanged = true;
        return true;
    }
    public void SetRightMouseMode(OrbitalStationRuntime.RmbMode mode) { if (Capture()) station.RightMouseMode = mode; }
    public void SetInvulnerable(bool value) { if (Capture()) health.DebugInvulnerable = value; }
    public void SetHudVisible(bool value) { if (Capture()) hud?.DebugSetHudVisible(value); }
    public void SetNumbersVisible(bool value) { if (Capture()) DamagePopup.DebugDamageNumbersVisible = value; }
    public void SetSpawning(bool value)
    {
        if (!Capture() || enemies == null || flow.IsFilmingBoss) return;
        if (value) enemies.ResumeSpawning(); else enemies.StopSpawning();
    }
    public string SpawnDensityUnavailableReason => UnavailableReason ??
        (enemies == null ? "Спавнер не найден" : flow.IsFilmingBoss ? "Обычный спавн остановлен на время босса" : null);
    public void SetSpawnDensity(float value)
    {
        if (SpawnDensityUnavailableReason != null || !Capture()) return;
        enemies.DebugSetCaptureDensity(value);
        Status = $"Плотность обычного спавна ×{enemies.DebugCaptureDensity:0.0}; лимит {enemies.DebugCaptureMaxAlive}";
    }
    public void FillEnemies()
    {
        if (SpawnDensityUnavailableReason != null || !Capture() || !enemies.IsSpawningEnabled) return;
        enemies.DebugFillCaptureEnemies();
        Status = "Добавлены обычные враги через спавнер, в пределах лимита и доступных мест";
    }
    public string WorldRuleUnavailableReason => UnavailableReason ??
        (worldRules == null || modifiers == null || !modifiers.IsInitialized ? "Система глобальных аномалий не готова" : null);
    public bool SetWorldRule(WorldRuleData rule)
    {
        string reason = WorldRuleUnavailableReason;
        if (reason != null || Config == null || rule == null || !Config.worldRules.Contains(rule))
        { Status = reason ?? "Нет данных глобальной аномалии"; return false; }
        if (!Capture()) return false;
        // Existing enemies carry spawn-time health/Golden modifiers. Replace ordinary
        // spawns instead of stacking or trying to reverse those combat modifiers.
        DespawnOrdinaryEnemies();
        if (!modifiers.DebugApplyCaptureWorldRule(rule)) { Status = "Аномалию не удалось применить"; return false; }
        worldRuleChanged = true;
        Status = $"Глобальная аномалия: {rule.DisplayName}. Новые враги получат её штатные эффекты";
        return true;
    }
    public void ClearEnemies() { if (Capture()) DespawnOrdinaryEnemies(); }
    public void ClearWorldEvents()
    {
        if (!Capture()) return;
        events?.ClearAllDebugEvents();
        ownedEvent = null;
        Status = "События убраны через штатную очистку, без наград";
    }
    public void SetThreat(float value)
    {
        if (!Capture()) return;
        run.SetThreatForDebug(value);
        FindFirstObjectByType<RunThreatController>()?.DebugRefreshPreset();
    }

    public bool ResetSettings()
    {
        if (run == null) return false;
        if (!run.IsActiveRun(runId)) { Release(); return false; }
        string reason = ResetUnavailableReason;
        if (reason != null) { Status = reason; return false; }
        ClearOwnedEncounter();
        enemies?.StopSpawning();
        DespawnOrdinaryEnemies();
        if (buildChanged && station != null)
        {
            station.Teardown();
            if (!run.DebugReplaceOrbitalState(JsonUtility.FromJson<OrbitalRunState>(orbitalSnapshot)) ||
                !station.RebuildRuntimeFromState()) { Status = "Сброс ORBITAL не завершён"; return false; }
        }
        if (health != null)
        {
            health.DebugInvulnerable = invulnerabilitySnapshot;
            if (!health.IsDead) health.SetRuntimeHealth(maxHealthSnapshot, healthSnapshot);
            if (playerMoved) { health.transform.position = playerPositionSnapshot; Physics2D.SyncTransforms(); }
        }
        run.DebugRestoreThreat(threatSnapshot, threatElapsedSnapshot);
        if (station != null) station.RightMouseMode = rightMouseSnapshot;
        FindFirstObjectByType<RunThreatController>()?.DebugRefreshPreset();
        if (worldRuleChanged) modifiers?.DebugApplyCaptureWorldRule(worldRuleSnapshot);
        enemies?.DebugSetCaptureDensity(densitySnapshot);
        RestoreVisibility();
        if (enemies != null && spawnSnapshot) enemies.ResumeSpawning();
        run.UnregisterSceneCleanup(Release);
        run = null;
        buildChanged = false;
        playerMoved = false;
        worldRuleChanged = false;
        orbitalSnapshot = null;
        Status = "Съёмочные настройки сброшены. Этот ран остаётся без записи прогресса";
        return true;
    }

    private void RestoreVisibility()
    {
        hud?.DebugSetHudVisible(hudSnapshot);
        DamagePopup.DebugDamageNumbersVisible = numbersSnapshot;
    }
    private void Release()
    {
        if (run == null) return;
        ClearOwnedEncounter();
        enemies?.ClearDebugSpawnedEnemies();
        enemies?.DebugSetCaptureDensity(densitySnapshot);
        if (worldRuleChanged) worldRules?.Clear();
        RestoreVisibility();
        if (health != null) health.DebugInvulnerable = invulnerabilitySnapshot;
        run.UnregisterSceneCleanup(Release);
        run = null;
        buildChanged = false;
        playerMoved = false;
        worldRuleChanged = false;
        orbitalSnapshot = null;
    }
    private void OnDestroy() => Release();
}
#endif
