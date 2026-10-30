#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class Subject42DebugMenu
{
    private string sandboxStatus;
    private bool IsSandboxScene => SceneManager.GetActiveScene().path == "Assets/_Project/Scenes/MainBuild/MVP.unity";
    private string SandboxUnavailableReason => !IsSandboxScene ? "Песочница доступна в production MVP." :
        !MvpCanEdit || !MvpStation.UsesSegmentedOrbit ? "Нужен живой игрок и свободная очередь наград." :
        CaptureSession.UnavailableReason;

    private void OpenSandbox()
    {
        if (IsSandboxScene) HubOpenUtilityPage(DebugTab.Sandbox);
    }

    private void BuildSandbox()
    {
        ResolveSceneReferences();
        string reason = SandboxUnavailableReason;
        bool ready = reason == null;
        string spawnReason = ready ? CaptureSession.SpawnDensityUnavailableReason : reason;
        bool spawnReady = spawnReason == null;
        var station = MvpStation;
        var state = station?.State;
        var health = PlayerRuntimeReference.PlayerHealth;
        AddHint(reason ?? "Изменения помечают попытку Development: production-прогресс не засчитывается.");
        if (!string.IsNullOrEmpty(sandboxStatus)) AddHint(sandboxStatus);
        SandboxRow("Быстрый тест",
            ("ПОДГОТОВИТЬ ТЕСТ", ready, () => SandboxAction(PrepareSandboxTest)),
            ("НАЧАТЬ БОЙ", spawnReady, () => SandboxAction(StartSandboxCombat)));
        if (state == null || !state.UsesSegmentedOrbit) return;

        AddSectionTitle("БИЛД", "Выдача в запас; установка в штатном конструкторе Tab.");
        SandboxRow("Оружие · запас",
            Weapon(OrbitalRewardKind.Pistol, "Пистолет"), Weapon(OrbitalRewardKind.Sniper, "Снайперка"),
            Weapon(OrbitalRewardKind.Shotgun, "Дробовик"), Weapon(OrbitalRewardKind.Saw, "Пила"));
        SandboxRow("Модули · запас",
            Weapon(OrbitalRewardKind.DamageLink, "DMG"), Weapon(OrbitalRewardKind.FireRateLink, "FR"),
            Weapon(OrbitalRewardKind.ExtraShotLink, "2X"), Weapon(OrbitalRewardKind.RicochetLink, "RIC"));
        SandboxRow($"Орбиты: {state.UnlockedSegmentedLevels}/{OrbitalRunState.SegmentedLevels}",
            ("ОТКРЫТЬ ВСЕ", ready && state.UnlockedSegmentedLevels < OrbitalRunState.SegmentedLevels,
                () => SandboxAction(() => SandboxOpenAllOrbits())));
        SandboxRow($"Лимит оружия: {state.SegmentedWeaponLimit}/12",
            ("+1 ЛИМИТ", ready && state.SegmentedWeaponLimit < OrbitalRunState.MaxSegmentedWeapons,
                () => SandboxAction(() => SandboxGrantBuildReward(OrbitalRewardKind.AddMount))),
            ("ОТКРЫТЬ TAB", ready && previousTimeScale > 0f, () => SandboxAction(OpenSandboxConstructor)));

        AddSectionTitle("БОЙ", "Обычные враги текущего профиля; штатные лимиты и паузы волн.");
        bool spawning = enemySpawner != null && enemySpawner.IsSpawningEnabled;
        SandboxRow("Спавн: " + (spawning ? "включён" : "остановлен"),
            (spawning ? "ОСТАНОВИТЬ" : "ВКЛЮЧИТЬ", spawnReady,
                () => SandboxAction(() => SetEnemyAutoSpawn(!enemySpawner.IsSpawningEnabled, captureSettings: true))),
            ("ДОБАВИТЬ ВРАГОВ", spawnReady && spawning, () => SandboxAction(CaptureSession.FillEnemies)));
        SandboxRow("Плотность", Density(.5f, "×0,5"), Density(1f, "×1"), Density(1.5f, "×1,5"), Density(2f, "×2"));
        SandboxRow("AI: " + (EnemyDebugAiFreeze.IsFrozen ? "заморожен" : "работает"),
            (EnemyDebugAiFreeze.IsFrozen ? "РАЗМОРОЗИТЬ AI" : "ЗАМОРОЗИТЬ AI", ready,
                () => SandboxAction(() => SetEnemyAiFrozen(!EnemyDebugAiFreeze.IsFrozen, captureSettings: true))),
            ("ОЧИСТИТЬ ОБЫЧНЫХ", ready, () => SandboxAction(CaptureSession.ClearEnemies)));
        AddHint($"Живых у спавнера: {enemySpawner?.DebugCaptureAliveCount ?? 0} / {enemySpawner?.DebugCaptureMaxAlive ?? 0}. Очистка без XP и наград, босс сохраняется.");
        if (ready && spawnReason != null) AddHint(spawnReason);

        AddSectionTitle("ИГРОК", health != null ? $"HP {health.CurrentHealth:0}/{health.MaxHealth:0}" : "Игрок не готов");
        bool invulnerable = health != null && health.DebugInvulnerable;
        SandboxRow("Бессмертие: " + (invulnerable ? "включено" : "выключено"),
            (invulnerable ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", ready,
                () => SandboxAction(() => CaptureSession.SetInvulnerable(!health.DebugInvulnerable))),
            ("ВОССТАНОВИТЬ HP", ready && health != null && health.CurrentHealth < health.MaxHealth,
                () => SandboxAction(CaptureSession.Heal)));

        BuildMvpWeaponExperiments(sandbox: true);

        (string, bool, UnityAction) Weapon(OrbitalRewardKind kind, string label)
        {
            var weapon = OrbitalRewardProvider.GetModuleKind(kind);
            var link = OrbitalRewardProvider.GetSegmentedLinkKind(kind);
            int stock = link.HasValue ? state.SegmentedLinkStock.Count(v => v == link.Value) :
                state.SegmentedWeapons.Count(v => v.ModuleType == weapon);
            return ($"{label} +1 · {stock}", ready && upgradeManager.DebugRewardUnavailableReason(kind) == null,
                () => SandboxAction(() => GrantMvpBuildReward(kind)));
        }
        (string, bool, UnityAction) Density(float value, string label)
        {
            bool selected = enemySpawner != null && Mathf.Approximately(enemySpawner.DebugCaptureDensity, value);
            return (label + (selected ? " (выбрано)" : ""), spawnReady && !selected,
                () => SandboxAction(() => CaptureSession.SetSpawnDensity(value)));
        }

    }

    private void SandboxAction(Action action)
    {
        string reason = SandboxUnavailableReason;
        if (reason != null) { sandboxStatus = reason; RefreshCurrentTab(); return; }
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        sandboxStatus = null;
        action();
        if (isOpen) RefreshCurrentTab();
    }

    private bool SandboxGrantBuildReward(OrbitalRewardKind kind)
    {
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        if (reason == null && upgradeManager.DebugOfferCatalogueReward(kind, true)) return true;
        sandboxStatus = reason ?? "Награда недоступна: завершите текущее действие.";
        return false;
    }

    private bool SandboxOpenAllOrbits()
    {
        while (MvpStation.State.UnlockedSegmentedLevels < OrbitalRunState.SegmentedLevels)
            if (!SandboxGrantBuildReward(OrbitalRewardKind.NewRing)) return false;
        return true;
    }

    private void PrepareSandboxTest()
    {
        CaptureSession.SetInvulnerable(true);
        SetEnemyAutoSpawn(false, captureSettings: true);
        if (!SandboxOpenAllOrbits()) return;
        while (MvpStation.State.SegmentedWeaponLimit < OrbitalRunState.MaxSegmentedWeapons)
            if (!SandboxGrantBuildReward(OrbitalRewardKind.AddMount)) return;
        sandboxStatus = "Тест подготовлен: 3 орбиты · лимит 12 · бессмертие · спавн остановлен. Запас сохранён.";
    }

    private void OpenSandboxConstructor()
    {
        CloseMenu();
        if (MvpStation.InputOwner.TryToggleConstructor()) return;
        sandboxStatus = "Конструктор сейчас недоступен.";
        OpenMenu();
    }

    private void StartSandboxCombat()
    {
        SetEnemyAutoSpawn(true, captureSettings: true);
        if (enemySpawner.IsSpawningEnabled) CloseMenu();
        else sandboxStatus = CaptureSession.Status;
    }

    private void SandboxRow(string title, params (string Label, bool Enabled, UnityAction Action)[] actions)
    {
        var row = CreateRect(title, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 5f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        var label = CreateText("Состояние", row, title, 15f, TextAlignmentOptions.MidlineLeft, Color.white);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = actions.Length > 2 ? .9f : 1.4f;
        foreach (var action in actions)
        {
            var button = CreateButton(row, action.Label, action.Action, 1f, action.Enabled);
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13f;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }
}
#endif
