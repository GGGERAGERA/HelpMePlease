#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

public sealed partial class Subject42DebugMenu
{
    private TrailerCaptureSession captureSession;
    private int additionalSection;
    private TrailerCaptureSession CaptureSession => captureSession != null ? captureSession :
        captureSession = gameObject.GetComponent<TrailerCaptureSession>() ?? gameObject.AddComponent<TrailerCaptureSession>();

    private void AddCaptureSpawnControls(string reason)
    {
        var session = CaptureSession;
        AddRow("Очистить обычных врагов", reason ?? "Убрать без убийств, опыта и наград; босс сохраняется",
            mutedColor, "ОЧИСТИТЬ", reason == null, () => { session.ClearEnemies(); RefreshCurrentTab(); });
        string densityReason = session.SpawnDensityUnavailableReason;
        AddHint(densityReason ?? $"Спавнер: живых {enemySpawner.DebugCaptureAliveCount} / {enemySpawner.DebugCaptureMaxAlive}, " +
            $"плотность ×{enemySpawner.DebugCaptureDensity:0.0}. Паузы волн и ограничения типов сохраняются");
        foreach (var option in new[] { ("Разреженно", .5f), ("Штатно", 1f), ("Плотно", 1.5f), ("Насыщенно", 2f) })
        {
            bool selected = enemySpawner != null && Mathf.Approximately(enemySpawner.DebugCaptureDensity, option.Item2);
            AddRow($"{option.Item1} · ×{option.Item2:0.0}", densityReason ?? (selected ? "ВЫБРАНО" : "Меняет темп и лимит обычного спавна; уменьшение убирает избыток без наград"),
                selected ? successColor : mutedColor, "ВЫБРАТЬ", densityReason == null,
                () => { session.SetSpawnDensity(option.Item2); RefreshCurrentTab(); });
        }
        string fillReason = densityReason ?? (enemySpawner != null && !enemySpawner.IsSpawningEnabled ? "Сначала включите обычный спавн" : null);
        AddRow("Добавить врагов сейчас", fillReason ?? "До 10 через текущий профиль, в пределах лимита и свободных мест",
            mutedColor, "ДОБАВИТЬ", fillReason == null, () => { session.FillEnemies(); RefreshCurrentTab(); });
    }

    private void AddCaptureToggle(string label, bool value, string reason, UnityEngine.Events.UnityAction action)
    {
        AddRow(label, reason ?? (value ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО"),
            reason != null ? warningColor : value ? successColor : mutedColor,
            value ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", reason == null, () => { action(); RefreshCurrentTab(); });
    }

    private void AddPlayerBuildSection()
    {
        AddSectionTitle("Игрок", "Изменения для текущего забега");
        var session = CaptureSession;
        string reason = session.UnavailableReason;
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        AddRow("Здоровье", reason ?? $"{health.CurrentHealth:0} / {health.MaxHealth:0}", mutedColor,
            "ВОССТАНОВИТЬ", reason == null, () => { session.Heal(); RefreshCurrentTab(); });
        AddCaptureToggle("Неуязвимость", health != null && health.DebugInvulnerable, reason,
            () => session.SetInvulnerable(!health.DebugInvulnerable));
    }

    private void AddWorldEventsSection()
    {
        var session = CaptureSession;
        string reason = session.UnavailableReason;
        AddSectionTitle("МИР", "События, глобальная аномалия и текущая угроза");
        AddSectionTitle("ГЛОБАЛЬНАЯ АНОМАЛИЯ", "Одна на локацию. Смена убирает обычных врагов; новые получают штатные эффекты");
        string ruleReason = session.WorldRuleUnavailableReason;
        var activeRule = WorldRuleController.Instance != null ? WorldRuleController.Instance.ActiveRule : null;
        if (session.Config == null || session.Config.worldRules.Length == 0) AddHint("Нет данных глобальных аномалий");
        else foreach (var rule in session.Config.worldRules)
        {
            if (rule == null) continue;
            string label = CaptureWorldRuleLabel(rule.RuleType);
            bool selected = rule.RuleType == (activeRule != null ? activeRule.RuleType : WorldRuleType.None);
            AddRow(label, ruleReason ?? (selected ? "ВЫБРАНО" : rule.ShortDescription), selected ? successColor : mutedColor,
                "ВЫБРАТЬ", ruleReason == null, () => { session.SetWorldRule(rule); RefreshCurrentTab(); });
        }
        AddSectionTitle("Угроза", "Давление обычных врагов");
        foreach (int pressure in new[] { 0, 25, 50 })
            AddRow($"Давление {pressure}", reason ?? $"Сейчас: {RunStateManager.Instance.ThreatValue:0.0}", mutedColor,
                "ВЫСТАВИТЬ", reason == null, () => { session.SetThreat(pressure); RefreshCurrentTab(); });
        AddSectionTitle("Тестовое событие", "Повторный запуск заменяет текущий тестовый бой");
        var events = FindFirstObjectByType<WorldEventSpawner>();
        AddRow("Очистить события", reason ?? "Убрать текущие события без выдачи наград", mutedColor,
            "ОЧИСТИТЬ", reason == null && events != null, () => { session.ClearWorldEvents(); RefreshCurrentTab(); });
        AddCaptureEncounter(TrailerCaptureConfig.Shot.RocketStorm);
        AddHint(session.Status);
    }

    private void AddCaptureEncounter(TrailerCaptureConfig.Shot shot)
    {
        var session = CaptureSession;
        if (session.Config == null) return;
        foreach (var preset in session.Config.presets)
        {
            if (preset.shot != shot) continue;
            string unavailable = session.PresetUnavailableReason(preset, preserveBuild: true);
            AddRow(preset.label, unavailable ?? preset.description, mutedColor, "ЗАПУСТИТЬ", unavailable == null,
                () => { session.Apply(preset, preserveBuild: true); BindInputOwner(); RefreshCurrentTab(); });
        }
    }

    private void AddAdditionalSection()
    {
        if (IsProductionBunker)
        {
            AddBunkerProgressionSection();
            return;
        }
        string[] labels = { "Боты / Golden Path", "Оружие: эксперименты", "Враги: эксперименты", "XP: визуальная лаборатория" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            AddOptionRow(labels[i], additionalSection == i, characterSpawner != null,
                () => { additionalSection = index; RefreshCurrentTab(); });
        }
        if (characterSpawner == null) { AddHint("Начните ран, чтобы открыть игровые лаборатории"); return; }
        switch (additionalSection)
        {
            case 0: AddBotLabSection(); break;
            case 1: BuildMvpWeaponExperiments(); break;
            case 2: BuildMvpEnemyExperiments(); break;
            case 3: AddExperienceVisualLabSection(); break;
        }
    }

    private static string CaptureWorldRuleLabel(WorldRuleType type) => type switch
    {
        WorldRuleType.None => "Без глобальной аномалии",
        WorldRuleType.Darkness => "Темнота",
        WorldRuleType.Snow => "Снег",
        WorldRuleType.Wind => "Ветер",
        WorldRuleType.Golden => "Золотой уровень",
        WorldRuleType.Rain => "Дождь",
        WorldRuleType.Condensation => "Конденсация",
        _ => type.ToString()
    };
}
#endif
