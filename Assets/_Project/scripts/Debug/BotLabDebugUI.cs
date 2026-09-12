#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Same F1 menu and UI builders, kept in a partial to avoid growing the existing menu further.
public sealed partial class Subject42DebugMenu
{
    private TextMeshProUGUI botTelemetryText;
    private float nextBotUiRefresh;
    public static void RestoreBotResumeScale(float scale)
    {
        if (activeInstance != null && activeInstance.isOpen) activeInstance.previousTimeScale = scale;
    }
    private void BindBotLabScene()
    {
        if (BotRunSession.Current != null)
            BotRunSession.Current.BindScene(characterSpawner, runFlowController, upgradeManager, GameplayAreaService.Instance);
    }
    private BotRunSession GetBotSession()
    {
        bool created = BotRunSession.Current == null;
        var session = BotRunSession.Ensure();
        if (created) session.BindScene(characterSpawner, runFlowController, upgradeManager, GameplayAreaService.Instance);
        return session;
    }
    private void AddBotLabSection()
    {
        // Building all tabs must not create a persistent session in ordinary gameplay.
        var session = BotRunSession.Current;
        var batch = session != null ? session.GetComponent<BotBatchRunner>() : null;
        bool busy = session != null && (session.IsRunning || session.IsStarting || (batch != null && batch.IsActive));
        AddToggleRow("BOT ENABLED", session != null && session.BotEnabled, true,
            () => { var current = GetBotSession(); if (current.BotEnabled) StopBotLab(); current.SetBotEnabled(!current.BotEnabled); });
        AddSectionTitle("Strategy: Survivor", "Один свежий сектор выбранным персонажем");
        AddRow("Seed mode", (session?.SeedMode ?? BotSeedMode.Auto).ToString().ToUpperInvariant(), mutedColor, "AUTO/FIXED", !busy, () =>
        {
            var current = GetBotSession();
            current.SeedMode = current.SeedMode == BotSeedMode.Auto ? BotSeedMode.Fixed : BotSeedMode.Auto;
            RefreshCurrentTab();
        });
        AddBotSeedInput(!busy);
        AddRow("Simulation speed", $"{session?.SelectedSpeed ?? 1f}x", mutedColor, "1 / 5 / 10", !busy, () =>
        {
            var current = GetBotSession();
            current.SelectedSpeed = current.SelectedSpeed == 1f ? 5f : current.SelectedSpeed == 5f ? 10f : 1f;
            RefreshCurrentTab();
        });
        AddRow("Start Bot Run", "Перезапускает первый сектор", mutedColor, "START",
            !busy && characterSpawner != null && (session == null || session.CanStart), () =>
            {
                var current = GetBotSession();
                if (!current.CanStart) return;
                CloseMenu();
                current.StartBotRun();
            });
        AddRow("Replay fixed seed", "Всегда использует введённый seed", mutedColor, "RUN SEED",
            !busy && characterSpawner != null && (session == null || session.CanStart), () =>
            {
                var current = GetBotSession();
                if (!current.CanStart) return;
                CloseMenu();
                current.StartBotRun(current.FixedSeed);
            });
        AddBotBatchButtons(!busy && characterSpawner != null && (session == null || session.CanStart));
        foreach (int count in new[] { 1, 10, 100 })
            AddRow("Golden Path ×" + count, "3 sectors → boss → victory → bunker → second run", mutedColor, "GOLDEN ×" + count,
                !busy, () =>
                {
                    var current = GetBotSession();
                    if (!current.CanStartGoldenPath) return;
                    var runner = current.GetComponent<BotBatchRunner>();
                    if (runner == null) runner = current.gameObject.AddComponent<BotBatchRunner>();
                    CloseMenu();
                    runner.StartGoldenPathBatch(count, current.SeedMode, current.FixedSeed, current.SelectedSpeed);
                });
        AddRow("Stop Bot", "Возвращает обычное управление", mutedColor, "STOP",
            busy, () =>
            {
                CloseMenu();
                StopBotLab();
            });
        botTelemetryText = CreateText("Bot telemetry", contentRoot, "Idle", 17f,
            TextAlignmentOptions.TopLeft, Color.white);
        botTelemetryText.gameObject.AddComponent<LayoutElement>().preferredHeight = 285f;
        RefreshBotLabTelemetry();
        AddHint("Duration — game time; watchdog — active real time. FIXED batch повторяет один seed.\nConsole + Artifacts/BotRuns и BotBatches (JSON/CSV). Stop во время reward оставляет выбор игроку.");
    }
    private void StopBotLab()
    {
        var current = BotRunSession.Current;
        if (current == null) return;
        var batch = current.GetComponent<BotBatchRunner>();
        if (batch != null && batch.IsActive) batch.StopBatch();
        else current.StopBot();
    }
    private void AddBotSeedInput(bool editable)
    {
        var row = CreateRect("Fixed seed", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        var label = CreateText("Seed label", row, "Fixed Seed", 18f, TextAlignmentOptions.MidlineLeft, Color.white);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(.4f, 1f);
        label.rectTransform.offsetMin = new Vector2(16f, 0f); label.rectTransform.offsetMax = Vector2.zero;
        var inputRoot = CreateRect("Seed input", row);
        inputRoot.anchorMin = new Vector2(.4f, 0f); inputRoot.anchorMax = Vector2.one;
        inputRoot.offsetMin = Vector2.zero; inputRoot.offsetMax = Vector2.zero;
        inputRoot.gameObject.AddComponent<Image>().color = rowColor;
        var text = CreateText("Value", inputRoot, "", 18f, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform, 12f, 12f);
        var input = inputRoot.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = inputRoot; input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.text = (BotRunSession.Current?.FixedSeed ?? 12345).ToString(System.Globalization.CultureInfo.InvariantCulture);
        input.interactable = editable;
        input.onEndEdit.AddListener(value =>
        {
            var current = GetBotSession();
            if (int.TryParse(value, out int seed)) current.FixedSeed = seed;
            input.SetTextWithoutNotify(current.FixedSeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });
    }
    private void AddBotBatchButtons(bool available)
    {
        var row = CreateRect("Batch runs", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        int[] counts = { 10, 50, 100 };
        for (int i = 0; i < counts.Length; i++)
        {
            int count = counts[i];
            var button = CreateButton(row, count + " RUNS", () =>
            {
                var current = GetBotSession();
                if (!current.CanStart) return;
                var batch = current.GetComponent<BotBatchRunner>();
                if (batch == null) batch = current.gameObject.AddComponent<BotBatchRunner>();
                CloseMenu();
                batch.StartBatch(count, current.SeedMode, current.FixedSeed, current.SelectedSpeed);
            }, 100f);
            button.interactable = available;
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i / 3f, 0f); rect.anchorMax = new Vector2((i + 1) / 3f, 1f);
            rect.offsetMin = new Vector2(3f, 0f); rect.offsetMax = new Vector2(-3f, 0f);
        }
    }
    private void UpdateBotLabTelemetry()
    {
        if (!isOpen || activeTab != DebugTab.QA || Time.unscaledTime < nextBotUiRefresh) return;
        nextBotUiRefresh = Time.unscaledTime + .25f;
        RefreshBotLabTelemetry();
    }
    private void RefreshBotLabTelemetry()
    {
        if (botTelemetryText == null) return;
        var session = BotRunSession.Current;
        var data = session != null ? session.Result : null;
        var batch = session != null ? session.GetComponent<BotBatchRunner>() : null;
        string progress = batch != null && batch.Result != null
            ? $"Batch {batch.Result.Status}: Run {batch.CurrentRunIndex}/{batch.Result.RequestedRuns}\nFinished: {batch.Result.CompletedRuns} · Wins: {batch.Result.SectorCompleted} · Deaths: {batch.Result.PlayerDead} · Stuck/Error: {batch.Result.StuckErrors}\n"
            : "";
        botTelemetryText.text = data == null ? "Bot state: Idle" :
            progress + $"Seed: {data.Seed} · {data.SimulationSpeed}x\nBot state: {session.State}\nDuration: {data.Duration:0.0}s\nKills: {data.Kills}\n" +
            $"Damage dealt (ORBITAL): {data.DamageDealt:0.##}\nDamage taken: {data.DamageTaken:0.##}\n" +
            $"XP collected: {data.XPCollected}   Level: {data.Level}\nEnemies alive: {data.EnemiesAlive}\n{data.Reason}";
    }
}
#endif
