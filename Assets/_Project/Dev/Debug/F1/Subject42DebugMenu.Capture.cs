#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

public sealed partial class Subject42DebugMenu
{
    private TrailerCaptureSession captureSession;
    private TrailerCaptureSession CaptureSession => captureSession != null ? captureSession :
        captureSession = gameObject.GetComponent<TrailerCaptureSession>() ?? gameObject.AddComponent<TrailerCaptureSession>();

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
        if (characterSpawner == null) { AddHint("Начните ран, чтобы открыть игровые лаборатории"); return; }
        BuildDebugCategory("Боты / Golden Path", "", AddBotLabSection);
        BuildDebugCategory("Варианты оружия", "Один профиль на тип оружия", BuildMvpWeaponExperiments, expanded: false);
        BuildDebugCategory("Эксперименты врагов", "", BuildMvpEnemyExperiments, expanded: false);
        BuildDebugCategory("XP: визуальная лаборатория", "", AddExperienceVisualLabSection, expanded: false);
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
