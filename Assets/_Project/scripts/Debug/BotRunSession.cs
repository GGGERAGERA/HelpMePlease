#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;

// Created only by BOT LAB. Survives the explicit restart, never binds by global object search.
public sealed class BotRunSession : MonoBehaviour
{
    public static BotRunSession Current { get; private set; }
    public bool BotEnabled { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsStarting { get; private set; }
    public BotRunResult Result { get; private set; }
    public string State => IsStarting ? "Starting fresh sector" : IsRunning ? controller.State : Result?.Result ?? "Idle";
    public string OutputPath { get; private set; }
    public event Action<BotRunResult> Finished;
    public BotSeedMode SeedMode { get; set; } = BotSeedMode.Auto;
    public int FixedSeed { get; set; } = 12345;
    public float SelectedSpeed { get; set; } = 1f;

    private CharacterSpawner spawner;
    private RunFlowController flow;
    private UpgradeManager rewards;
    private GameplayAreaService area;
    private PlayerHealth health;
    private OrbitalStationRuntime station;
    private BotController controller;
    private BotTelemetry telemetry;
    private BotProgressWatchdog gameplayWatchdog;
    private BotProgressWatchdog rewardWatchdog;
    private Vector2 progressPosition;
    private int progressVersion;
    private string rewardState;
    private float requestedAt, startedAt, nextChoice;
    private string pendingException;
    private bool ownsSeed, ownsSpeed;
    private bool saveStandaloneResult = true;
    private float restoreScale = 1f;

    public static BotRunSession Ensure()
    {
        if (Current == null) new GameObject("Bot Lab Session (Debug)").AddComponent<BotRunSession>();
        return Current;
    }
    private void Awake()
    {
        Current = this;
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += OnLog;
    }
    public void BindScene(CharacterSpawner playerSpawner, RunFlowController runFlow,
        UpgradeManager upgradeManager, GameplayAreaService gameplayArea)
    {
        if (IsRunning) Finish(BotRunOutcome.Aborted, "Scene changed during run");
        spawner = playerSpawner;
        flow = runFlow;
        rewards = upgradeManager;
        area = gameplayArea;
    }
    public void SetBotEnabled(bool value)
    {
        BotEnabled = value;
        if (!value) StopBot();
    }
    public bool CanStart => !IsRunning && !IsStarting && !SceneTransitionOverlay.IsTransitioning &&
        spawner != null && spawner.SpawnedCharacterData != null && flow != null &&
        RunStateManager.Instance?.CurrentSector != null &&
        !RunRoute.IsFinalSector(RunStateManager.Instance.CurrentSector.SectorNumber) &&
        SceneTransitionOverlay.CanLoad(spawner.gameObject.scene.name);

    public bool StartBotRun(int? seed = null, float? speed = null, float? originalScale = null, bool saveStandaloneResult = true)
    {
        if (!CanStart) return false;
        float requestedSpeed = speed ?? SelectedSpeed;
        if (requestedSpeed != 1f && requestedSpeed != 5f && requestedSpeed != 10f) return false;
        PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
        restoreScale = originalScale ?? (Time.timeScale > 0f ? Time.timeScale : 1f);
        CharacterData character = spawner.SpawnedCharacterData;
        string scene = spawner.gameObject.scene.name;
        Result = new BotRunResult { Character = character.name, Scene = scene, Sector = RunRoute.FirstSector,
            Seed = seed ?? (SeedMode == BotSeedMode.Fixed ? FixedSeed : BotRunSeed.NextAuto()), SimulationSpeed = requestedSpeed };
        OutputPath = null;
        this.saveStandaloneResult = saveStandaloneResult;
        requestedAt = Time.realtimeSinceStartup;
        BotEnabled = true;
        IsStarting = true;
        pendingException = null;
        health = null; station = null;
        // New scene's debug menu explicitly rebinds the dependencies after Start.
        spawner = null; flow = null; rewards = null; area = null;
        try
        {
            if (!SceneTransitionOverlay.Load(scene, () =>
            {
                BotRunSeed.Begin(Result.Seed);
                ownsSeed = true;
                RunStateManager.Instance.BeginNewRun(character, null);
            }))
            { Finish(BotRunOutcome.Error, "Production scene transition was rejected"); return false; }
            return true;
        }
        catch (Exception error) { Finish(BotRunOutcome.Error, error.ToString()); return false; }
    }

    // Also useful to test the controller in an already initialized normal sector.
    public bool BeginCurrentSector()
    {
        if (IsRunning || spawner == null || spawner.SpawnedPlayer == null || flow == null ||
            rewards == null || area == null || area.PlayableArea == null || ExperienceManager.Instance == null)
            return false;
        var sector = RunStateManager.Instance?.CurrentSector;
        if (sector == null || RunRoute.IsFinalSector(sector.SectorNumber) || flow.IsLevelCompleted) return false;
        health = spawner.SpawnedPlayer.GetComponent<PlayerHealth>();
        var movement = spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>();
        station = spawner.SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        if (health == null || health.IsDead || movement == null || !movement.enabled ||
            station == null || !station.IsInitialized || movement.MovementIntent != null) return false;
        if (!IsStarting)
        {
            saveStandaloneResult = true;
            OutputPath = null;
            Result = new BotRunResult { Character = spawner.SpawnedCharacterData.name, Scene = spawner.gameObject.scene.name,
                Sector = sector.SectorNumber, Seed = BotRunSeed.NextAuto() };
            restoreScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            BotRunSeed.Begin(Result.Seed);
            ownsSeed = true;
        }
        Result.LayoutSignature = string.Join(";", ProductionAnomalySite.ActiveSites.Select(s => s.name + ":" +
            s.transform.position.ToString("F3"))) + "|" + string.Join(";", ProductionSectorExit.ActiveExits.Select(e => e.transform.position.ToString("F3")));
        controller = new BotController(movement, area);
        station.InputOwner.PrepareForExternalPause();
        station.InputOwner.DebugSuppressPlayerInput = true;
        telemetry = new BotTelemetry(Result, health, ExperienceManager.Instance, rewards, station.Combat);
        gameplayWatchdog = new BotProgressWatchdog();
        rewardWatchdog = new BotProgressWatchdog();
        progressPosition = movement.transform.position;
        progressVersion = telemetry.ProgressVersion;
        rewardState = null;
        startedAt = Time.realtimeSinceStartup;
        nextChoice = 0f;
        IsStarting = false;
        IsRunning = BotEnabled = true;
        ownsSpeed = true;
        Time.timeScale = Result.SimulationSpeed;
        return true;
    }
    public void StopBot()
    {
        if (IsRunning || IsStarting) Finish(BotRunOutcome.Aborted, "Stop Bot / disabled");
    }
    private void Update()
    {
        try { Tick(); }
        catch (Exception error) { Finish(BotRunOutcome.Error, error.ToString()); }
    }
    private void Tick()
    {
        if (!IsRunning && !IsStarting) return;
        if (pendingException != null) { Finish(BotRunOutcome.Error, pendingException); return; }
        if (IsStarting)
        {
            if (!SceneTransitionOverlay.IsTransitioning && BeginCurrentSector()) return;
            if (Time.realtimeSinceStartup - requestedAt > 30f)
                Finish(BotRunOutcome.Error, "Sector initialization exceeded 30 seconds");
            return;
        }
        Result.WallDuration = Time.realtimeSinceStartup - startedAt;
        if (health == null || flow == null || station == null)
        { Finish(BotRunOutcome.Aborted, "Gameplay objects unloaded"); return; }
        telemetry.Sample(Time.deltaTime);
        if (health.IsDead) { Finish(BotRunOutcome.PlayerDead, "Player died"); return; }
        // Drain any rewards from the same trigger before ending, but never choose the next sector.
        if (flow.IsLevelCompleted && rewards.IsRewardQueueIdle)
        { Finish(BotRunOutcome.SectorCompleted, "Production sector exit reached"); return; }
        if (SceneTransitionOverlay.IsTransitioning)
        { Finish(BotRunOutcome.Aborted, "External scene transition"); return; }
        if (Subject42DebugMenu.IsDebugMenuOpen)
        { controller.Pause("Debug menu paused"); return; }

        if (!rewards.IsRewardQueueIdle)
        {
            controller.Pause("Reward: " + station.RewardFlow.CompactStatus);
            string current = station.RewardFlow.CompactStatus + ":" + station.RewardFlow.SessionToken + ":" + telemetry.ProgressVersion;
            bool progressed = current != rewardState;
            rewardState = current;
            if (rewardWatchdog.Tick(Time.unscaledDeltaTime, true, progressed))
            { Finish(BotRunOutcome.Stuck, "Reward flow made no progress for 30 seconds: " + current); return; }
            if (Time.unscaledTime < nextChoice) return;
            nextChoice = Time.unscaledTime + .15f;
            if (station.RewardFlow.PendingReward.HasValue) station.RewardFlow.DebugChooseFirstValidTarget();
            else if (rewards.DebugCurrentChoices is { Count: > 0 }) rewards.DebugSelectCurrentChoice(0);
            return;
        }
        rewardState = null;
        rewardWatchdog.Tick(0f, false, true);
        if (Time.timeScale <= 0f) { controller.Pause("Paused"); return; }
        Vector2 position = health.transform.position;
        bool progress = Vector2.Distance(position, progressPosition) >= .5f || telemetry.ProgressVersion != progressVersion;
        if (progress) { progressPosition = position; progressVersion = telemetry.ProgressVersion; }
        if (gameplayWatchdog.Tick(Time.unscaledDeltaTime, true, progress))
        { Finish(BotRunOutcome.Stuck, "No movement, kill, XP, reward or sector progress for 30 active real seconds"); return; }
        // A moving bot may still circle forever; report, never repair the world.
        if (Result.Duration >= 600f)
        { Finish(BotRunOutcome.Stuck, "Sector not completed within 10 gameplay minutes"); return; }
        controller.Tick(Result.Duration);
    }

    public void Finish(BotRunOutcome outcome, string reason)
    {
        if (!IsRunning && !IsStarting && controller == null) return;
        IsRunning = IsStarting = false;
        // Release ownership before serialization or reporting can fail. Do not overwrite another pause owner's scale.
        controller?.Dispose(); controller = null;
        if (health != null) telemetry?.Sample(0f);
        telemetry?.Dispose(); telemetry = null;
        if (station != null && station.InputOwner != null) station.InputOwner.DebugSuppressPlayerInput = false;
        if (ownsSeed) { BotRunSeed.End(); ownsSeed = false; }
        if (ownsSpeed)
        {
            PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
            if (rewards != null && !rewards.IsRewardQueueIdle) rewards.DebugSetRewardResumeScale(restoreScale);
            Subject42DebugMenu.RestoreBotResumeScale(restoreScale);
            if (Time.timeScale > 0f && !SceneTransitionOverlay.IsTransitioning) Time.timeScale = restoreScale;
            ownsSpeed = false;
        }
        if (Result == null) Result = new BotRunResult();
        Result.Result = outcome.ToString(); Result.Reason = reason;
        try
        {
            if (station != null && station.IsInitialized)
                Result.FinalOrbital = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(station.State));
            Debug.Log(Result.Report());
            if (outcome == BotRunOutcome.Stuck || outcome == BotRunOutcome.Error)
                Debug.LogWarning("[Bot Lab] " + outcome + ": " + reason);
            if (saveStandaloneResult)
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "BotRuns"));
                Directory.CreateDirectory(root);
                string path = Path.Combine(root, "latest_run.json");
                File.WriteAllText(path, JsonUtility.ToJson(Result, true));
                OutputPath = path;
                Debug.Log("[Bot Lab] Result: " + OutputPath);
            }
        }
        catch (Exception error) { Debug.LogWarning("[Bot Lab] Could not capture/save report: " + error.Message); }
        Finished?.Invoke(Result);
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if ((IsRunning || IsStarting) && type == LogType.Exception) pendingException = message + "\n" + stack;
    }
    private void OnDisable() => Finish(BotRunOutcome.Aborted, "Bot Lab disabled / Play Mode stopped");
    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        if (Current == this) Current = null;
    }
}
#endif
