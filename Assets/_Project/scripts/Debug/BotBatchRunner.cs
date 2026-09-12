#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

// Sequential orchestration only. All gameplay and cleanup are owned by the same single-run session.
[RequireComponent(typeof(BotRunSession))]
public sealed class BotBatchRunner : MonoBehaviour
{
    public bool IsActive { get; private set; }
    public BotBatchResult Result { get; private set; }
    public string OutputPath { get; private set; }
    public int CurrentRunIndex => Result == null ? 0 : Math.Min(Result.Results.Count + (IsActive ? 1 : 0), Result.RequestedRuns);
    private BotRunSession session;
    private bool nextPending, stopping;
    private float nextDeadline, originalScale;
    private bool goldenPath;

    public bool StartGoldenPathBatch(int count, BotSeedMode mode, int fixedSeed, float speed)
        => StartBatchInternal(count, mode, fixedSeed, speed, true);

    private void Awake() => session = GetComponent<BotRunSession>();
    public bool StartBatch(int count, BotSeedMode mode, int fixedSeed, float speed)
        => StartBatchInternal(count, mode, fixedSeed, speed, false);

    private bool StartBatchInternal(int count, BotSeedMode mode, int fixedSeed, float speed, bool golden)
    {
        if (IsActive || count < 1 || count > 100 || !(golden ? session.CanStartGoldenPath : session.CanStart) || (speed != 1f && speed != 5f && speed != 10f)) return false;
        goldenPath = golden;
        PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
        originalScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Result = new BotBatchResult { RequestedRuns = count, SeedMode = mode.ToString(), FixedSeed = fixedSeed, SimulationSpeed = speed };
        if (golden) Result.Strategy = "GoldenPath";
        OutputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "BotBatches",
            "latest_batch.json"));
        IsActive = true; stopping = false;
        session.Finished += OnFinished;
        ScheduleNext();
        return true;
    }
    private void ScheduleNext() { nextPending = true; nextDeadline = Time.realtimeSinceStartup + 30f; }
    private void Update()
    {
        if (!IsActive || !nextPending) return;
        // User inspection pauses batch orchestration, without consuming an infrastructure timeout.
        if (Subject42DebugMenu.IsDebugMenuOpen) { nextDeadline = Time.realtimeSinceStartup + 30f; return; }
        if (!(goldenPath ? session.CanStartGoldenPath : session.CanStart))
        {
            if (Time.realtimeSinceStartup > nextDeadline) Complete("Error", "Next sector cannot start: transition or required scene dependencies unavailable");
            return;
        }
        nextPending = false;
        int seed = Result.SeedMode == BotSeedMode.Fixed.ToString() ? Result.FixedSeed : goldenPath
            ? unchecked(Result.FixedSeed + Result.Results.Count) : BotRunSeed.NextAuto();
        bool started = goldenPath ? session.StartGoldenPath(seed, Result.SimulationSpeed, originalScale)
            : session.StartBotRun(seed, Result.SimulationSpeed, originalScale, saveStandaloneResult: false);
        if (!started)
            Complete("Error", "Single-run session rejected the next run");
    }
    private void OnFinished(BotRunResult run)
    {
        if (!IsActive) return;
        Result.Results.Add(run);
        Result.Recalculate();
        if (stopping) return;
        if (goldenPath && run.Result != BotRunOutcome.GoldenPathPassed.ToString())
        { Complete("Failed", $"Seed {run.Seed}: {run.Reason}"); return; }
        if (run.Result == BotRunOutcome.Aborted.ToString()) { Complete("Stopped", run.Reason); return; }
        if (Result.CompletedRuns >= Result.RequestedRuns) { Complete("Completed", null); return; }
        if (!Save()) { Complete("Error", "Could not checkpoint batch results"); return; }
        ScheduleNext();
    }
    public void StopBatch()
    {
        if (!IsActive) return;
        stopping = true;
        nextPending = false;
        session.StopBot();
        Complete("Stopped", "STOP requested");
    }
    private void Complete(string status, string reason)
    {
        IsActive = nextPending = false;
        session.Finished -= OnFinished;
        Result.Status = status; Result.StopReason = reason;
        Result.CompletedAt = DateTime.UtcNow.ToString("O");
        Result.Recalculate();
        if (!Save())
        {
            Result.Status = "Error";
            Result.StopReason = "Final JSON/CSV save failed; all collected results remain available in memory";
        }
        if (goldenPath)
        {
            try
            {
                string historyPath = Path.Combine(Path.GetDirectoryName(OutputPath), "golden_path_history.json");
                var history = File.Exists(historyPath) ? JsonUtility.FromJson<GoldenPathBatchHistory>(File.ReadAllText(historyPath)) : new GoldenPathBatchHistory();
                history.Batches.RemoveAll(b => b.BatchId == Result.BatchId);
                history.Batches.Add(Result);
                File.WriteAllText(historyPath, JsonUtility.ToJson(history, true));
            }
            catch (Exception error)
            {
                Result.Status = "Error"; Result.StopReason = "Golden Path history save failed: " + error.Message;
                Save();
            }
        }
        Debug.Log(Result.Report());
        if (Result.Status == "Error") Debug.LogWarning("[Bot Batch] " + Result.StopReason);
    }
    private bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, JsonUtility.ToJson(Result, true));
            File.WriteAllText(Path.ChangeExtension(OutputPath, ".csv"), Result.Csv());
            return true;
        }
        catch (Exception error) { Debug.LogWarning("[Bot Batch] Save failed: " + error.Message); return false; }
    }
    private void OnDisable() => StopBatch();
}
#endif
