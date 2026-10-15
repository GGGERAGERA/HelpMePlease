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

    private void AddCaptureSection()
    {
        var session = CaptureSession;
        AddSectionTitle("ПРЕСЕТЫ КАДРА", "Применяйте повторно; затем закройте F1 и записывайте через OBS");
        if (session.Config == null) AddHint("Данные TrailerCapturePresets не найдены");
        else foreach (var preset in session.Config.presets)
        {
            string reason = session.PresetUnavailableReason(preset);
            AddRow(preset.label, reason ?? preset.description, reason == null ? mutedColor : warningColor,
                "ПРИМЕНИТЬ", reason == null, () => { session.Apply(preset); BindInputOwner(); RefreshCurrentTab(); });
        }
        AddSectionTitle("УПРАВЛЕНИЕ СЪЁМКОЙ", "HUD и числа урона независимы; границы событий и маркеры опасности сохраняются");
        AddCaptureControls();
        string resetReason = session.ResetUnavailableReason;
        AddRow("Сброс съёмочных настроек", resetReason ?? "Вернуть исходный билд, здоровье, угрозу и видимость",
            warningColor, "СБРОСИТЬ", resetReason == null, () => { session.ResetSettings(); BindInputOwner(); RefreshCurrentTab(); });
        AddHint(session.Status);
    }

    private void AddCaptureControls()
    {
        var session = CaptureSession;
        string reason = session.UnavailableReason;
        bool available = reason == null;
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        AddRow("Восстановить здоровье", reason ?? $"{health.CurrentHealth:0} / {health.MaxHealth:0}",
            mutedColor, "ВОССТАНОВИТЬ", available, () => { session.Heal(); RefreshCurrentTab(); });
        AddCaptureToggle("Неуязвимость", health != null && health.DebugInvulnerable, reason,
            () => session.SetInvulnerable(!health.DebugInvulnerable));
        var hud = HUDManager.Instance;
        AddCaptureToggle("Игровой HUD", hud != null && hud.DebugHudVisible,
            reason ?? (hud == null ? "HUD не найден" : null), () => session.SetHudVisible(!hud.DebugHudVisible));
        AddCaptureToggle("Числа урона", DamagePopup.DebugDamageNumbersVisible, reason,
            () => session.SetNumbersVisible(!DamagePopup.DebugDamageNumbersVisible));
        AddCaptureSpawnControls(reason);
    }

    private void AddCaptureSpawnControls(string reason)
    {
        var session = CaptureSession;
        var flow = RunFlowController.Instance;
        AddCaptureToggle("Обычный спавн врагов", enemySpawner != null && enemySpawner.IsSpawningEnabled,
            reason ?? (enemySpawner == null ? "Спавнер не найден" : flow != null && flow.IsFilmingBoss ? "Остановлен на время боя с боссом" : null),
            () => session.SetSpawning(!enemySpawner.IsSpawningEnabled));
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
        AddSectionTitle("ИГРОК", "Только текущий ран; состояние переключателей берётся из gameplay");
        var session = CaptureSession;
        string reason = session.UnavailableReason;
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        AddRow("Здоровье", reason ?? $"{health.CurrentHealth:0} / {health.MaxHealth:0}", mutedColor,
            "ВОССТАНОВИТЬ", reason == null, () => { session.Heal(); RefreshCurrentTab(); });
        AddCaptureToggle("Неуязвимость", health != null && health.DebugInvulnerable, reason,
            () => session.SetInvulnerable(!health.DebugInvulnerable));
        var station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
        AddSectionTitle("ОРБИТЫ И ОРУЖИЕ", station != null && station.IsInitialized
            ? $"Колец: {station.Rings.Count} · модулей: {station.Modules.Count} · ядро: {station.Core.Level}"
            : "ORBITAL не готов");
        AddHint("Билды выдаются пресетами в «Съёмка». ПКМ: Bullet Time; Shift + WASD: смещение центра орбит.");
        var state = station != null ? station.State : null;
        bool canAddRing = reason == null && state.Rings.Count < 4 && state.CanAddRing(out _);
        AddRow("Добавить орбиту", reason ?? (canAddRing ? "Штатный лимит; для съёмки максимум 4 кольца" : "Достигнут лимит колец"),
            mutedColor, "ДОБАВИТЬ", canAddRing, () =>
            {
                if (session.PrepareBuildEdit()) station.AddRing();
                RefreshCurrentTab();
            });
        int ringId = station != null && station.SelectedRing != null ? station.SelectedRing.RingId : 0;
        bool canAddMount = reason == null && ringId != 0 && state.CanAddMount(ringId, out _);
        AddRow("Крепление на выбранной орбите", reason ?? (canAddMount ? "Добавить свободный mount" : "Выберите орбиту с запасом вместимости"),
            mutedColor, "ДОБАВИТЬ", canAddMount, () =>
            {
                if (session.PrepareBuildEdit()) station.AddMount();
                RefreshCurrentTab();
            });
        foreach (OrbitalModuleKind kind in Enum.GetValues(typeof(OrbitalModuleKind)))
        {
            var mounts = state == null ? Array.Empty<(int ring, int mount)>() : state.Rings
                .SelectMany(ring => Enumerable.Range(0, ring.MountCount).Select(mount => (ring.StableRingId, mount)))
                .Where(slot => state.CanInstallModule(kind, slot.Item1, slot.mount, out _)).ToArray();
            bool canPlace = mounts.Length > 0 && (kind != OrbitalModuleKind.LinkNode ||
                mounts.Any(a => mounts.Any(b => state.CanInstallLinkPair(a.Item1, a.mount, b.Item1, b.mount, out _))));
            string label = kind switch
            {
                OrbitalModuleKind.Pistol => "Пистолет",
                OrbitalModuleKind.Saw => "Пила",
                OrbitalModuleKind.Shotgun => "Дробовик",
                OrbitalModuleKind.ArcEmitter => "Дуговой излучатель",
                _ => "Связующие узлы (пара)"
            };
            AddRow(label, reason ?? (canPlace ? "После закрытия F1 выберите орбиту и свободный mount" : "Нет совместимых свободных mounts"),
                mutedColor, "УСТАНОВИТЬ", reason == null && canPlace, () =>
                {
                    if (!session.PrepareBuildEdit()) return;
                    station.BeginModulePlacement(kind);
                    CloseMenu();
                });
        }
        foreach (OrbitalStationRuntime.RmbMode mode in Enum.GetValues(typeof(OrbitalStationRuntime.RmbMode)))
        {
            string label = mode switch
            {
                OrbitalStationRuntime.RmbMode.BulletTime => "Поле замедления",
                OrbitalStationRuntime.RmbMode.Repulse => "Отталкивание",
                _ => "Смена направления вращения"
            };
            AddRow(label, station != null && station.RightMouseMode == mode ? "ВЫБРАНО" : reason ?? "Режим ПКМ",
                mutedColor, "ВЫБРАТЬ", reason == null, () => { session.SetRightMouseMode(mode); RefreshCurrentTab(); });
        }
    }

    private void AddWorldEventsSection()
    {
        var session = CaptureSession;
        string reason = session.UnavailableReason;
        AddSectionTitle("МИР", "Настоящий спавн и текущая угроза");
        AddCaptureSpawnControls(reason);
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
        AddSectionTitle("УГРОЗА", "Давление обычных врагов; пресеты съёмки ограничены значением 60");
        foreach (int pressure in new[] { 0, 25, 50 })
            AddRow($"Давление {pressure}", reason ?? $"Сейчас: {RunStateManager.Instance.ThreatValue:0.0}", mutedColor,
                "ВЫСТАВИТЬ", reason == null, () => { session.SetThreat(pressure); RefreshCurrentTab(); });
        AddSectionTitle("СОБЫТИЯ И БОСС", "Запускаются через игровые сервисы; повторный запуск заменяет съёмочный бой");
        var events = FindFirstObjectByType<WorldEventSpawner>();
        AddRow("Очистить события", reason ?? "Убрать текущие события без выдачи наград", mutedColor,
            "ОЧИСТИТЬ", reason == null && events != null, () => { session.ClearWorldEvents(); RefreshCurrentTab(); });
        if (session.Config != null)
            foreach (var preset in session.Config.presets)
                if (preset.shot == TrailerCaptureConfig.Shot.RocketStorm || preset.shot == TrailerCaptureConfig.Shot.Boss)
                {
                    string unavailable = session.PresetUnavailableReason(preset);
                    AddRow(preset.label, unavailable ?? preset.description, mutedColor, "ЗАПУСТИТЬ", unavailable == null,
                        () => { session.Apply(preset); BindInputOwner(); RefreshCurrentTab(); });
                }
        AddHint(session.Status);
    }

    private void AddAdditionalSection()
    {
        AddSectionTitle("ДОПОЛНИТЕЛЬНЫЕ ИНСТРУМЕНТЫ", "Лабораторные команды отделены от подготовки кадров");
        AddRow("Визуальная лаборатория", "Настройка текущих визуальных эффектов", mutedColor, "ОТКРЫТЬ",
            characterSpawner != null, () => SelectTab(DebugTab.VisualTest));
        AddRow("Боевые эффекты и манекен", "Технические проверки combat feel", mutedColor, "ОТКРЫТЬ",
            characterSpawner != null, () => SelectTab(DebugTab.FeelTest));
        AddRow("Лаборатория ORBITAL", "Ручная установка модулей и проверки состояния", mutedColor, "ОТКРЫТЬ",
            characterSpawner != null, () => SelectTab(DebugTab.OrbitalProduction));
        string[] labels = { "Bot Lab и Batch Runner", "Ручной спавн и AI", "Лаборатория опыта" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            AddRow(labels[i], additionalSection == i ? "ОТКРЫТО НИЖЕ" : "Технические инструменты",
                mutedColor, "ОТКРЫТЬ", characterSpawner != null,
                () => { additionalSection = index; RefreshCurrentTab(); });
        }
        if (characterSpawner == null) { AddHint("Начните ран, чтобы открыть игровые лаборатории"); return; }
        if (additionalSection == 0) AddBotLabSection();
        else if (additionalSection == 1) AddEnemiesSection();
        else AddExperienceVisualLabSection();
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
