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

    private void Awake() => session = GetComponent<BotRunSession>();
    public bool StartBatch(int count, BotSeedMode mode, int fixedSeed, float speed)
    {
        if (IsActive || count < 1 || count > 100 || !session.CanStart || (speed != 1f && speed != 5f && speed != 10f)) return false;
        PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
        originalScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Result = new BotBatchResult { RequestedRuns = count, SeedMode = mode.ToString(), FixedSeed = fixedSeed, SimulationSpeed = speed };
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
        if (!session.CanStart)
        {
            if (Time.realtimeSinceStartup > nextDeadline) Complete("Error", "Next sector cannot start: transition or required scene dependencies unavailable");
            return;
        }
        nextPending = false;
        int seed = Result.SeedMode == BotSeedMode.Fixed.ToString() ? Result.FixedSeed : BotRunSeed.NextAuto();
        if (!session.StartBotRun(seed, Result.SimulationSpeed, originalScale, saveStandaloneResult: false))
            Complete("Error", "Single-run session rejected the next run");
    }
    private void OnFinished(BotRunResult run)
    {
        if (!IsActive) return;
        Result.Results.Add(run);
        Result.Recalculate();
        if (stopping) return;
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
