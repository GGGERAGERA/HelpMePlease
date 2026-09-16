#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Read-only interpretation of persisted telemetry. No gameplay or runner state changes.
public static class GoldenPathSummary
{
    private const string Pass = "PASS", Game = "GAME_REGRESSION", Combat = "BOT_COMBAT_FAIL",
        Assertion = "ASSERTION_FAIL", Aborted = "ABORTED";
    private static readonly HashSet<string> GameChecks = new(StringComparer.Ordinal)
    {
        "Runtime.NoException", "Run.ActiveAfterStart", "Run.SectorSequence", "Run.NoSector4",
        "Run.SceneStillPresent", "Run.CompletedSectorOnce", "Progression.SectorChoiceDeadline",
        "Orbital.StartingBaseline", "Orbital.CoreExists", "Orbital.ValidState", "Orbital.RuntimeMatchesState",
        "Orbital.StableRunId", "Orbital.RingsAndMountsPersist", "Orbital.RingUpgradesPersist",
        "Orbital.CoreUpgradesPersist", "Orbital.ModulesNeverOverwritten",
        "Rewards.GrantedOnce", "Rewards.ChosenRewardApplied", "Rewards.DirectMountSelectionCompleted",
        "Rewards.SelectionReleased", "Rewards.TransitionOnlyWhenResolved", "Rewards.QueueDeadline",
        "Boss.AfterSector3Only", "Boss.SpawnOnceAfterSector3", "Boss.Exists", "Boss.DeathRegistered", "Boss.VictoryOnce",
        "Cleanup.BunkerRunState", "Cleanup.SceneObjects", "Cleanup.RewardsAndBoss", "Cleanup.CompleteBaseline"
    };

    private sealed class Entry
    {
        public BotBatchResult Batch;
        public BotRunResult Run;
        public int Index;
        public string Kind;
        public string Scenario => $"{Run.Seed}/{Run.SeedVersion}/{Batch.SeedVersion}/{Batch.Strategy}/{Run.Character}/{Number(Batch.SimulationSpeed)}";
    }
    private sealed class Issue
    {
        public Entry Entry;
        public GoldenPathFailure Failure;
        public string Kind;
        public Entry Recovery;
    }

    public static void WriteFromHistory(string historyPath)
    {
        var history = JsonUtility.FromJson<GoldenPathBatchHistory>(File.ReadAllText(historyPath));
        if (history == null || history.Batches == null) throw new InvalidDataException("Golden Path history has no Batches array.");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(historyPath), "golden_path_summary.md"), Render(history), new UTF8Encoding(false));
    }

    private static IEnumerable<GoldenPathFailure> Failures(BotRunResult run) =>
        run.GoldenPath?.Failures?.Where(f => f != null) ?? Enumerable.Empty<GoldenPathFailure>();

    private static string FailureKind(GoldenPathFailure failure)
    {
        if (failure.Assertion == "Run.PlayerAlive") return Combat;
        // The old arena token was shared by unrelated body rewards: a token alone
        // cannot prove duplicate application. Preserve it as validator evidence.
        if (failure.Assertion == "Rewards.GrantedOnce" && (failure.Reason ?? "").StartsWith("Reward token committed twice", StringComparison.Ordinal)) return Assertion;
        // Older telemetry recorded only a combined identity/delta validator failure,
        // without the expected/actual application delta. It cannot prove a game bug.
        if (failure.Assertion == "Rewards.ChosenRewardApplied" && failure.Reason == "Chosen reward did not apply exactly once") return Assertion;
        return GameChecks.Contains(failure.Assertion ?? "") ? Game : Assertion;
    }

    public static string Classify(BotRunResult run)
    {
        if (run.Result == "Aborted") return Aborted; // Overrides the nested GoldenPath FAIL.
        var kinds = Failures(run).Select(FailureKind).ToArray();
        if (kinds.Contains(Game)) return Game;
        if (kinds.Contains(Assertion)) return Assertion;
        if (kinds.Contains(Combat) || run.Result == "PlayerDead") return Combat;
        if (run.GoldenPath?.AssertionsFailed > 0) return Assertion;
        if (run.GoldenPath?.Result == "PASS" && run.Result == "GoldenPathPassed") return Pass;
        return Assertion;
    }

    public static string Render(GoldenPathBatchHistory history)
    {
        var batches = (history?.Batches ?? new()).Where(b => b != null)
            .OrderBy(b => DateTimeOffset.TryParse(b.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : DateTimeOffset.MinValue).ToArray();
        var entries = batches.SelectMany(b => (b.Results ?? new()).Where(r => r?.GoldenPath != null)
            .Select(r => new Entry { Batch = b, Run = r, Kind = Classify(r) })).ToArray();
        for (int i = 0; i < entries.Length; i++) entries[i].Index = i;
        var latest = batches.LastOrDefault();
        var current = entries.Where(e => ReferenceEquals(e.Batch, latest)).ToArray();
        var issues = entries.Where(e => e.Kind != Aborted).SelectMany(e =>
        {
            var failures = Failures(e.Run).ToArray();
            if (failures.Length == 0 && e.Kind == Assertion)
                failures = new[] { new GoldenPathFailure { Assertion = "Unclassified.Result", Sector = e.Run.Sector, Reason = e.Run.Reason ?? e.Run.Result } };
            return failures.Select(f => new Issue { Entry = e, Failure = f, Kind = FailureKind(f) });
        }).Where(i => i.Kind != Combat)
            .GroupBy(i => i.Entry.Scenario + "/" + i.Failure.Assertion + "/" + i.Kind +
                (i.Failure.Assertion == "Runtime.NoException" ? "/" + FirstLine(i.Failure.Reason) : ""))
            .Select(g => g.Last()).ToArray();
        foreach (var issue in issues)
            issue.Recovery = entries.FirstOrDefault(e => e.Index > issue.Entry.Index && e.Scenario == issue.Entry.Scenario && e.Kind == Pass);
        var openGame = issues.Where(i => i.Kind == Game && i.Recovery == null).ToArray();
        var openAssertions = issues.Where(i => i.Kind == Assertion && i.Recovery == null).ToArray();
        var combat = entries.Where(e => e.Kind == Combat).GroupBy(e => e.Scenario).Select(g => g.Last()).ToArray();
        bool CombatStillOpen(Entry e) => entries.Last(x => x.Scenario == e.Scenario && x.Kind != Aborted).Kind == Combat;
        int combatOpen = combat.Count(CombatStillOpen);
        int aborted = current.Count(e => e.Kind == Aborted);
        var md = new StringBuilder("# Golden Path Summary\n\n## Current status\n\n");
        md.AppendLine($"- latest batch id: {Cell(latest?.BatchId)}");
        md.AppendLine($"- simulation speed: {(latest == null ? "—" : Number(latest.SimulationSpeed) + "×")}");
        md.AppendLine($"- requested/completed runs: {latest?.RequestedRuns ?? 0}/{current.Count(e => e.Kind != Aborted)}");
        foreach (string kind in new[] { Pass, Game, Combat, Assertion, Aborted }) md.AppendLine($"- {kind}: {current.Count(e => e.Kind == kind)}");
        md.AppendLine("\nCounts above describe the latest batch and are recalculated from individual outcomes; ABORTED is never counted as FAIL.\n");
        md.AppendLine("## Latest batch\n");
        Table(md, "seed | result | sector | boss killed | bunker clean | second run clean | reason");
        foreach (var entry in current)
        {
            var r = entry.Run;
            Row(md, r.Seed, entry.Kind, Sector(entry), Yes(r.GoldenPath.BossKilled), Yes(r.GoldenPath.BunkerClean), Yes(r.GoldenPath.SecondRunBaseline), Reason(r));
        }
        if (current.Length == 0) md.AppendLine("\nNo recorded runs.");
        md.AppendLine("\n## Game regressions\n");
        md.AppendLine("Unresolved gameplay/runtime invariant failures across history. Ordinary bot deaths are excluded.\n");
        IssueTable(md, openGame);
        if (openAssertions.Length > 0)
        {
            md.AppendLine("\nUnresolved ASSERTION_FAIL records (unknown or insufficient evidence to classify as a game regression):\n");
            IssueTable(md, openAssertions);
        }
        md.AppendLine("\n## Bot combat failures\n");
        md.AppendLine("Last combat failure per scenario; CURRENT means its latest non-aborted outcome still fails in combat.\n");
        Table(md, "seed | speed | sector | damage taken | last damage sources | reason | status");
        foreach (var e in combat)
            Row(md, e.Run.Seed, Number(e.Batch.SimulationSpeed) + "×", Sector(e), Number(e.Run.DamageTaken), DamageSources(e.Run), Reason(e.Run), CombatStillOpen(e) ? "CURRENT" : "HISTORICAL");
        if (combat.Length == 0) md.AppendLine("\nNone.");
        md.AppendLine("\n## Unstable seeds\n");
        var unstable = entries.GroupBy(e => e.Run.Seed).Where(g => g.Any(e => e.Kind == Pass) && g.Any(e => e.Kind != Pass && e.Kind != Aborted)).ToArray();
        Table(md, "seed | PASS | FAIL (excluding aborts) | results by speed");
        foreach (var g in unstable)
            Row(md, g.Key, g.Count(e => e.Kind == Pass), g.Count(e => e.Kind != Pass && e.Kind != Aborted),
                string.Join("; ", g.GroupBy(e => e.Batch.SimulationSpeed).Select(s => $"{Number(s.Key)}×: {s.Count(e => e.Kind == Pass)} PASS, {s.Count(e => e.Kind != Pass && e.Kind != Aborted)} FAIL, {s.Count(e => e.Kind == Aborted)} ABORTED")));
        if (unstable.Length == 0) md.AppendLine("\nNone.");
        md.AppendLine("\n## Fixed historical regressions\n");
        md.AppendLine("FIXED/HISTORICAL means a later full PASS on the same seed, seed versions, character, strategy and speed. It is evidence of recovery, not proof of a code fix or of the original root cause. A recurring failure reopens the issue.\n");
        Table(md, "seed | speed | recorded category | assertion | sector | reason | status | later PASS batch");
        foreach (var i in issues.Where(i => i.Recovery != null))
            Row(md, i.Entry.Run.Seed, Number(i.Entry.Batch.SimulationSpeed) + "×", i.Kind, i.Failure.Assertion, i.Failure.Sector, FirstLine(i.Failure.Reason), "FIXED/HISTORICAL", i.Recovery.Batch.BatchId);
        if (!issues.Any(i => i.Recovery != null)) md.AppendLine("\nNone.");
        md.AppendLine("\n## Final verdict\n");
        md.AppendLine("Scope: unresolved scenario failures across history; ABORTED counts the latest batch. A PASS at a different speed does not close an older scenario.\n");
        md.AppendLine($"GAME REGRESSIONS: {openGame.Length}  \nBOT COMBAT FAILS: {combatOpen}  \nASSERTION FAILS: {openAssertions.Length}  \nABORTED: {aborted}\n");
        string status = entries.Length == 0 ? "NO DATA" : openGame.Length > 0 ? "GAMEPLAY REGRESSIONS DETECTED" : openAssertions.Length > 0 ? "ASSERTION REVIEW REQUIRED" : !entries.Any(e => e.Kind == Pass) ? "INSUFFICIENT COMPLETED EVIDENCE" : "GAMEPLAY REGRESSION CLEAN";
        md.AppendLine("STATUS: " + status);
        md.AppendLine("\nGameplay-clean does not mean combat-stable or a completed batch. Classification uses recorded invariant names; unknown checks remain ASSERTION_FAIL. Source: golden_path_history.json (unchanged).");
        return md.ToString().Replace("\r\n", "\n");
    }

    private static void IssueTable(StringBuilder md, Issue[] issues)
    {
        if (issues.Length == 0) { md.AppendLine("None."); return; }
        Table(md, "seed | speed | assertion | sector | reason");
        foreach (var i in issues) Row(md, i.Entry.Run.Seed, Number(i.Entry.Batch.SimulationSpeed) + "×", i.Failure.Assertion, i.Failure.Sector, FirstLine(i.Failure.Reason));
    }
    private static int Sector(Entry e) => Failures(e.Run).FirstOrDefault()?.Sector ?? e.Run.Sector;
    private static string Reason(BotRunResult r) => r.Result == "Aborted" ? FirstLine(r.Reason) :
        string.Join("; ", Failures(r).Select(f => f.Assertion + ": " + FirstLine(f.Reason)).DefaultIfEmpty(r.GoldenPath?.Result == "PASS" ? "Route verified" : FirstLine(r.Reason)));
    private static string DamageSources(BotRunResult r) => string.Join("; ", (r.GoldenPath?.RecentDamage ?? new()).TakeLast(3).Select(s =>
        string.Join(", ", s.Split(';').Select(x => x.Trim()).Where(x => x.StartsWith("source=", StringComparison.Ordinal) || x.StartsWith("t=", StringComparison.Ordinal) || x.StartsWith("damage=", StringComparison.Ordinal)))));
    private static string FirstLine(string text) => (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Yes(bool value) => value ? "yes" : "no";
    private static string Cell(object value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        text = text.Replace("\r", " ").Replace("\n", " ").Replace("\\", "\\\\").Replace("|", "\\|").Replace("<", "&lt;").Replace(">", "&gt;").Replace("`", "\\`");
        return string.IsNullOrWhiteSpace(text) ? "—" : text.Length > 320 ? text.Substring(0, 317) + "..." : text;
    }
    private static void Table(StringBuilder md, string header)
    {
        md.AppendLine("| " + header + " |");
        md.AppendLine("| " + string.Join(" | ", header.Split('|').Select(_ => "---")) + " |");
    }
    private static void Row(StringBuilder md, params object[] cells) => md.AppendLine("| " + string.Join(" | ", cells.Select(Cell)) + " |");
}
#endif
