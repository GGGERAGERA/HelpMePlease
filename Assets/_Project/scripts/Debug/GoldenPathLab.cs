#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Only instantiated by the isolated editor lab scene. Uses the existing run/session/results.
public sealed class GoldenPathLab : MonoBehaviour
{
    public const string ScenePath = "Assets/_Project/Scenes/Dev/GoldenPathLab.unity";
    public static GoldenPathLab Current { get; private set; }
    public CharacterData Character;
    public float SelectedSpeed { get; private set; } = 5f;
    public bool IsBusy { get; private set; }
    public BotBatchResult DisplayedBatch => batch != null && batch.IsActive ? batch.Result : latest;
    public string DisplayedStatus
    {
        get
        {
            var counts = Counts(DisplayedBatch);
            string status = StatusFor(counts[1], counts[2], counts[3], counts[4], counts.Take(4).Sum(), DisplayedBatch?.RequestedRuns ?? 0);
            return IsBusy && status != "GAME REGRESSION FOUND" ? "BOT FAILS / ABORTED PRESENT" : status;
        }
    }
    public enum ResultsView { LatestBatch, History, Failures }
    public sealed class RunRow
    {
        public BotBatchResult Batch;
        public BotRunResult Run;
        public float Speed => Batch.SimulationSpeed;
    }
    public ResultsView View { get; private set; }
    public RunRow SelectedRow { get; private set; }
    public int VisibleRowCount => Rows.Length;
    public RunRow[] Rows => (View == ResultsView.LatestBatch
        ? (DisplayedBatch?.Results ?? new()).Where(r => r?.GoldenPath != null).Select(r => new RunRow { Batch = DisplayedBatch, Run = r })
        : (history?.Batches ?? new()).Where(b => b != null).OrderBy(b => b.StartedAt, StringComparer.Ordinal)
            .SelectMany(b => b.Results.Where(r => r?.GoldenPath != null).Select(r => new RunRow { Batch = b, Run = r })))
        .Where(e => View != ResultsView.Failures || GoldenPathSummary.Classify(e.Run) != "PASS")
        .Reverse().Take(20).ToArray();
    public string HistoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts/BotBatches/golden_path_history.json"));
    public string SummaryPath => Path.Combine(Path.GetDirectoryName(HistoryPath), "golden_path_summary.md");

    private GoldenPathBatchHistory history;
    private BotBatchResult latest;
    private BotBatchRunner batch;
    private BotRunSession session;
    private bool cancel;
    private float nextRefresh;
    private DateTime historyStamp;
    private string seedText = "48151623", notice = "Ready", lastBatchId;
    private Vector2 scroll, detailScroll;
    private GUIStyle title, label, muted, small, metric, button, heading, wrap;
    private static readonly Color Background = new(.045f, .063f, .095f), Panel = new(.08f, .105f, .15f),
        Ink = new(.9f, .94f, .98f), Muted = new(.56f, .65f, .75f), Green = new(.32f, .88f, .62f),
        Yellow = new(1f, .76f, .3f), Red = new(1f, .38f, .4f), Orange = new(1f, .5f, .18f);

    private void Awake()
    {
        if (Current != null && Current != this) { Destroy(gameObject); return; }
        Current = this;
        DontDestroyOnLoad(gameObject);
        RefreshResults();
    }

    public static string StatusFor(int game, int combat, int assertion, int aborted, int completed, int requested)
    {
        if (game > 0) return "GAME REGRESSION FOUND";
        if (combat + assertion + aborted > 0 || requested <= 0 || completed < requested) return "BOT FAILS / ABORTED PRESENT";
        return "GAMEPLAY REGRESSION CLEAN";
    }

    private static int[] Counts(BotBatchResult value)
    {
        string[] kinds = { "PASS", "GAME_REGRESSION", "BOT_COMBAT_FAIL", "ASSERTION_FAIL", "ABORTED" };
        return kinds.Select(k => value?.Results?.Count(r => r?.GoldenPath != null && GoldenPathSummary.Classify(r) == k) ?? 0).ToArray();
    }

    public void RefreshResults()
    {
        try
        {
            if (!File.Exists(HistoryPath)) { history = new(); latest = null; notice = "No history yet. Start a Golden Path run."; return; }
            var loaded = JsonUtility.FromJson<GoldenPathBatchHistory>(File.ReadAllText(HistoryPath));
            if (loaded?.Batches == null) throw new InvalidDataException("History has no Batches array.");
            history = loaded;
            latest = history.Batches.Where(b => b != null).OrderBy(b => b.StartedAt, StringComparer.Ordinal).LastOrDefault();
            historyStamp = File.GetLastWriteTimeUtc(HistoryPath);
            if (lastBatchId != latest?.BatchId) { scroll = Vector2.zero; lastBatchId = latest?.BatchId; }
            if (SelectedRow != null)
            {
                var selectedBatch = history.Batches.FirstOrDefault(b => b.BatchId == SelectedRow.Batch.BatchId);
                var selectedRun = selectedBatch?.Results.FirstOrDefault(r => r.RunId == SelectedRow.Run.RunId);
                if (selectedRun != null) SelectedRow = new RunRow { Batch = selectedBatch, Run = selectedRun };
            }
        }
        catch (Exception error) { notice = "Cannot read history: " + error.Message; }
    }

    public void SetView(ResultsView view) { View = view; scroll = Vector2.zero; }
    public void SelectRow(RunRow row) { SelectedRow = row; detailScroll = Vector2.zero; }
    public static Color RowColor(string category) => category switch
    {
        "PASS" => Green, "GAME_REGRESSION" => Red, "ASSERTION_FAIL" => Orange, _ => Yellow
    };
    public bool RerunSelectedSeed() => SelectedRow != null && StartBatch(1, SelectedRow.Run.Seed, SelectedRow.Speed);
    public void CopySeed() { if (SelectedRow != null) GUIUtility.systemCopyBuffer = SelectedRow.Run.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    public void CopyFailure() { if (SelectedRow != null) GUIUtility.systemCopyBuffer = SelectedDetailText; }
    public string SelectedDetailText
    {
        get
        {
            if (SelectedRow == null) return "Select a row to inspect a run.\n\nUse Failures to find earlier failed or aborted attempts, even when the latest batch passed.";
            var r = SelectedRow.Run; var g = r.GoldenPath;
            var text = new StringBuilder();
            text.AppendLine($"Seed: {r.Seed}\nResult category: {GoldenPathSummary.Classify(r)}\nSimulation speed: {SelectedRow.Speed:0.##}×\nSector: {g.Failures.FirstOrDefault()?.Sector ?? r.Sector}");
            text.AppendLine("\nEXACT FAILURE ASSERTIONS");
            if (g.Failures.Count == 0) text.AppendLine("None.");
            foreach (var failure in g.Failures)
                text.AppendLine($"{failure.Assertion}\nSector {failure.Sector} · simulation time {failure.SimulationTime:0.##}s\n{failure.Reason}\n");
            text.AppendLine("FULL REASON\n" + (r.Reason ?? "Not recorded."));
            text.AppendLine($"\nBossKilled: {g.BossKilled}\nBunkerClean: {g.BunkerClean}\nSecondRunBaseline: {g.SecondRunBaseline}");
            text.AppendLine($"DamageTaken: {r.DamageTaken:0.##}\nRemainingHP: {r.RemainingHP:0.##}\nRewardsTaken count: {g.RewardsTaken}\nAssertionsPassed / Failed: {g.AssertionsPassed} / {g.AssertionsFailed}");
            text.AppendLine($"Batch: {SelectedRow.Batch.BatchId}\nRun: {r.RunId}\nStarted: {r.StartedUtc}");
            text.AppendLine("\nRECENT DAMAGE · last 10 records");
            if (g.RecentDamage == null || g.RecentDamage.Count == 0) text.AppendLine("No damage records in this run's telemetry.");
            else foreach (string damage in g.RecentDamage.TakeLast(10)) text.AppendLine(damage + "\n");
            return text.ToString();
        }
    }

    public bool TryGetLastFailed(out BotRunResult run, out float speed)
    {
        var found = history?.Batches?.Where(b => b != null).OrderBy(b => b.StartedAt, StringComparer.Ordinal)
            .SelectMany(b => b.Results.Where(r => r?.GoldenPath != null).Select(r => new { Run = r, Batch = b }))
            .LastOrDefault(e => GoldenPathSummary.Classify(e.Run) != "PASS" && GoldenPathSummary.Classify(e.Run) != "ABORTED");
        run = found?.Run; speed = found?.Batch.SimulationSpeed ?? SelectedSpeed;
        return run != null;
    }

    public bool StartBatch(int count)
    {
        if (!int.TryParse(seedText, out int seed)) { notice = "Enter a valid integer seed."; return false; }
        return StartBatch(count, seed, SelectedSpeed);
    }

    private bool StartBatch(int count, int seed, float speed)
    {
        if (IsBusy || BotRunSession.Current?.GetComponent<BotBatchRunner>()?.IsActive == true) return false;
        if (Character == null || Character.characterPrefab == null) { notice = "Assign a production CharacterData in the lab Inspector."; return false; }
        IsBusy = true; cancel = false; SelectedSpeed = speed;
        StartCoroutine(ExecuteBatch(count, seed, speed));
        return true;
    }

    private IEnumerator ExecuteBatch(int count, int seed, float speed)
    {
        notice = $"Preparing bunker · seed {seed} · {speed:0}×";
        yield return SceneManager.LoadSceneAsync("MainMenu");
        float deadline = Time.realtimeSinceStartup + 30f;
        while (FindFirstObjectByType<BunkerRunStarter>() == null && Time.realtimeSinceStartup < deadline && !cancel) yield return null;
        session = BotRunSession.Ensure();
        batch = session.GetComponent<BotBatchRunner>() ?? session.gameObject.AddComponent<BotBatchRunner>();
        if (!cancel && FindFirstObjectByType<BunkerRunStarter>() != null && RunSelectionManager.Instance != null)
        {
            RunSelectionManager.Instance.SelectCharacter(Character);
            if (batch.StartGoldenPathBatch(count, BotSeedMode.Auto, seed, speed))
            {
                while (batch.IsActive) yield return null;
                notice = $"Batch finished · {batch.Result.Status} · {DateTime.Now:HH:mm:ss}";
            }
            else notice = "Runner rejected the start. Check the bunker/transition state.";
        }
        else notice = cancel ? "Canceled before batch start." : "Bunker startup timed out.";
        RefreshResults();
        // Scene is intentionally absent from production Build Settings.
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        IsBusy = false;
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + .5f;
        if (File.Exists(HistoryPath) && File.GetLastWriteTimeUtc(HistoryPath) != historyStamp) RefreshResults();
    }

    private void OnDestroy()
    {
        if (Current != this) return;
        if (batch != null && batch.IsActive) batch.StopBatch();
        Current = null;
    }

    private void Styles()
    {
        if (title != null) return;
        GUIStyle Style(int size, Color color, bool bold = false) => new(GUI.skin.label)
        { fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, normal = { textColor = color }, richText = false, padding = new RectOffset(0, 0, 0, 0) };
        title = Style(30, Ink, true); label = Style(17, Ink); muted = Style(15, Muted); small = Style(14, Ink);
        metric = Style(12, Muted); metric.wordWrap = false;
        heading = Style(24, Ink, true); wrap = Style(15, Ink); wrap.wordWrap = true;
        button = new GUIStyle(GUI.skin.button) { fontSize = 16, richText = false, alignment = TextAnchor.MiddleCenter };
    }
    private static void Fill(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    private void Text(float x, float y, float width, string text, GUIStyle style = null, float height = 30) => GUI.Label(new Rect(x, y, width, height), text, style ?? label);
    private bool Button(float x, float y, float width, string text) => GUI.Button(new Rect(x, y, width, 40), text, button);
    private static string Short(string value, int count) => string.IsNullOrEmpty(value) ? "—" : value.Replace('\n', ' ').Replace('\r', ' ').Length <= count ? value.Replace('\n', ' ').Replace('\r', ' ') : value.Replace('\n', ' ').Replace('\r', ' ').Substring(0, count - 1) + "…";
    private void Open(string path) { if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path); else notice = "File not available yet: " + Path.GetFileName(path); }

    private void OnGUI()
    {
        Styles(); GUI.depth = -10000;
        Fill(new Rect(0, 0, Screen.width, Screen.height), Background);
        Matrix4x4 old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
        Text(40, 26, 1000, "GOLDEN PATH / LAB", title, 42);
        Text(1120, 39, 300, "SUBJECT #42  ·  DEV / QA", muted);
        var shown = DisplayedBatch; var counts = Counts(shown);
        string status = DisplayedStatus;
        Color statusColor = status == "GAME REGRESSION FOUND" ? Red : status == "BOT FAILS / ABORTED PRESENT" ? Yellow : Green;
        Fill(new Rect(40, 88, 1360, 70), Panel); Fill(new Rect(40, 88, 5, 70), statusColor);
        Color before = GUI.contentColor; GUI.contentColor = statusColor;
        Text(62, 98, 1200, status, heading, 35); GUI.contentColor = before;
        Text(62, 132, 1300, "Latest batch assessment · " + (shown?.BatchId ?? "no batch recorded"), muted);
        string[] names = { "SPEED", "REQUESTED", "COMPLETED", "PASS", "GAME REGRESSIONS", "BOT COMBAT FAILS", "ASSERTION FAILS", "ABORTED" };
        string[] values = { shown == null ? "—" : shown.SimulationSpeed + "×", (shown?.RequestedRuns ?? 0).ToString(), counts.Take(4).Sum().ToString(), counts[0].ToString(), counts[1].ToString(), counts[2].ToString(), counts[3].ToString(), counts[4].ToString() };
        for (int i = 0; i < names.Length; i++) { float x = 40 + i * 171; Fill(new Rect(x, 174, 163, 66), Panel); Text(x + 12, 184, 150, names[i], metric); Text(x + 12, 208, 150, values[i], heading); }
        GUI.enabled = !IsBusy;
        if (Button(40, 262, 245, "Run Golden Path ×1")) StartBatch(1);
        if (Button(297, 262, 245, "Run Golden Path ×10")) StartBatch(10);
        if (Button(554, 262, 245, "Run Golden Path ×100")) StartBatch(100);
        if (Button(824, 262, 160, (SelectedSpeed == 5 ? "● " : "") + "Speed 5×")) SelectedSpeed = 5;
        if (Button(996, 262, 160, (SelectedSpeed == 10 ? "● " : "") + "Speed 10×")) SelectedSpeed = 10;
        Text(1180, 272, 70, "Seed", muted);
        seedText = GUI.TextField(new Rect(1225, 264, 175, 36), seedText, 12, new GUIStyle(GUI.skin.textField) { fontSize = 18 });
        bool canRerun = TryGetLastFailed(out var failed, out float failedSpeed);
        GUI.enabled = !IsBusy && canRerun;
        if (Button(40, 316, 300, "Rerun Last Failed Seed")) StartBatch(1, failed.Seed, failedSpeed);
        GUI.enabled = true;
        if (Button(352, 316, 180, "Refresh Results")) RefreshResults();
        if (Button(544, 316, 270, "Open golden_path_summary.md")) Open(SummaryPath);
        if (Button(826, 316, 270, "Open golden_path_history.json")) Open(HistoryPath);
        GUI.enabled = IsBusy;
        if (Button(1110, 316, 290, "Stop batch")) { cancel = true; if (batch != null && batch.IsActive) batch.StopBatch(); }
        GUI.enabled = true;
        Text(40, 366, 1360, canRerun ? $"Rerun target: {failed.Seed} at original {failedSpeed:0}×  ·  New batches use {SelectedSpeed:0}×  ·  Character: {Character?.name}" : $"No failed seed recorded  ·  New batches use {SelectedSpeed:0}×  ·  Character: {Character?.name}", muted);
        Fill(new Rect(40, 403, 1360, 52), Panel);
        string live = IsBusy && batch != null && batch.IsActive
            ? $"RUNNING   ·   seed {session.Result?.Seed}   ·   {batch.Result.CompletedRuns}/{batch.Result.RequestedRuns} complete   ·   sector {RunStateManager.Instance?.CurrentSector?.SectorNumber ?? 0}   ·   {batch.Result.SimulationSpeed:0}×   ·   {session.State}"
            : IsBusy ? "RUNNING   ·   " + notice : notice;
        Text(56, 417, 1320, live, label);
        if (Button(40, 474, 195, (View == ResultsView.LatestBatch ? "● " : "") + "Latest batch")) SetView(ResultsView.LatestBatch);
        if (Button(247, 474, 195, (View == ResultsView.History ? "● " : "") + "History")) SetView(ResultsView.History);
        if (Button(454, 474, 195, (View == ResultsView.Failures ? "● " : "") + "Failures")) SetView(ResultsView.Failures);
        Text(669, 486, 200, "Latest 20 · newest first", muted);
        float[] widths = { 90, 160, 50, 75, 80, 100, 230 };
        string[] headers = { "Seed", "Result", "Sector", "Boss killed", "Bunker clean", "2nd run clean", "Reason" };
        Fill(new Rect(40, 528, 820, 32), new Color(.12f, .16f, .22f));
        float offset = 50;
        for (int i = 0; i < widths.Length; i++) { Text(offset, 537, widths[i], headers[i], metric); offset += widths[i]; }
        var rows = Rows;
        scroll = GUI.BeginScrollView(new Rect(40, 560, 820, 260), scroll, new Rect(0, 0, 790, Mathf.Max(260, rows.Length * 34)));
        for (int index = 0; index < rows.Length; index++)
        {
            var row = rows[index]; var r = row.Run; var g = r.GoldenPath;
            string category = GoldenPathSummary.Classify(r);
            Color color = RowColor(category);
            bool selected = SelectedRow?.Batch.BatchId == row.Batch.BatchId && SelectedRow?.Run.RunId == r.RunId;
            string reason = r.Result == "GoldenPathPassed" ? "Route verified" : (g.Failures.FirstOrDefault()?.Reason ?? r.Reason);
            var rowRect = new Rect(0, index * 34, 790, 34);
            Fill(rowRect, Color.Lerp(Background, color, selected ? .3f : .1f));
            Fill(new Rect(0, index * 34, selected ? 5 : 3, 34), color);
            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none)) SelectRow(row);
            string[] cells = { r.Seed.ToString(), category, (g.Failures.FirstOrDefault()?.Sector ?? r.Sector).ToString(), g.BossKilled ? "YES" : "NO", g.BunkerClean ? "YES" : "NO", g.SecondRunBaseline ? "YES" : "NO", Short(reason, 29) };
            offset = 10;
            for (int i = 0; i < widths.Length; i++) { GUI.contentColor = i < 2 ? color : Color.white; Text(offset, index * 34 + 8, widths[i] - 6, cells[i], small); offset += widths[i]; }
            GUI.contentColor = Color.white;
        }
        if (rows.Length == 0) Text(16, 22, 1000, "No completed run rows yet.", muted);
        GUI.EndScrollView();
        Text(40, 834, 820, View == ResultsView.LatestBatch ? "Latest batch only · historical failures are available in Failures." : "Historical attempts · a later PASS does not erase a failed attempt.", muted);
        Fill(new Rect(888, 474, 512, 378), Panel);
        Text(906, 486, 476, SelectedRow == null ? "RUN DETAILS" : $"SEED {SelectedRow.Run.Seed}  ·  {SelectedRow.Speed:0.##}×", heading, 34);
        Text(906, 521, 476, SelectedRow == null ? "Click a row on the left" : GoldenPathSummary.Classify(SelectedRow.Run), muted);
        string detailText = SelectedDetailText;
        float detailHeight = wrap.CalcHeight(new GUIContent(detailText), 462);
        detailScroll = GUI.BeginScrollView(new Rect(906, 553, 478, 234), detailScroll, new Rect(0, 0, 456, detailHeight + 10));
        GUI.Label(new Rect(0, 0, 456, detailHeight + 10), detailText, wrap);
        GUI.EndScrollView();
        GUI.enabled = SelectedRow != null && !IsBusy;
        if (Button(906, 802, 226, "RERUN SELECTED SEED")) RerunSelectedSeed();
        GUI.enabled = SelectedRow != null;
        if (Button(1142, 802, 106, "COPY SEED")) { CopySeed(); notice = "Seed copied."; }
        if (Button(1258, 802, 126, "COPY FAILURE")) { CopyFailure(); notice = "Full selected run details copied."; }
        GUI.enabled = true;
        Text(40, 868, 1360, "Editor-only lab · existing BotBatchRunner + history · results refresh automatically · not included in production flow", muted);
        GUI.matrix = old;
    }
}
#endif
