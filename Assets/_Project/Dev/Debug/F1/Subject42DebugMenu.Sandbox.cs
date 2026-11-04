#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
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
        !MvpCanEdit ? "Нужен живой игрок и свободная очередь наград." : CaptureSession.UnavailableReason;

    private void OpenSandbox()
    {
        if (IsSandboxScene) HubOpenPage(DebugTab.Sandbox);
    }

    private void BuildSandbox()
    {
        ResolveSceneReferences();
        string reason = SandboxUnavailableReason;
        AddHint(reason ?? "Изменения помечают попытку Development.");
        if (!string.IsNullOrEmpty(sandboxStatus)) AddHint(sandboxStatus);

        BuildDebugCategory("Бой", "", () => BuildSandboxCombat(reason == null));
        var station = MvpStation;
        if (station == null || !station.IsInitialized || upgradeManager == null) return;
        if (station.Rig != null) BuildRigRewardCatalogue();
        else if (station.UsesSegmentedOrbit) BuildSandboxOrbitStock(station.State, reason == null);
    }

    private void BuildSandboxOrbitStock(OrbitalRunState state, bool ready)
    {
        AddSectionTitle("Оружие", "В запас конструктора");
        AddStock(OrbitalRewardKind.Pistol, "Пистолет");
        AddStock(OrbitalRewardKind.Sniper, "Снайперка");
        AddStock(OrbitalRewardKind.Shotgun, "Дробовик");

        AddSectionTitle("Модули", "В запас конструктора");
        AddStock(OrbitalRewardKind.DamageLink, "DMG");
        AddStock(OrbitalRewardKind.FireRateLink, "FR");
        AddStock(OrbitalRewardKind.ExtraShotLink, "2X");
        AddStock(OrbitalRewardKind.RicochetLink, "RIC");

        AddSectionTitle("Орбиты / крепления", "");
        SandboxRow($"Орбиты: {state.UnlockedSegmentedLevels}/{OrbitalRunState.SegmentedLevels}",
            ("ОТКРЫТЬ ВСЕ", ready && state.UnlockedSegmentedLevels < OrbitalRunState.SegmentedLevels,
                () => SandboxAction(() => OpenAllSandboxOrbits(state))));
        SandboxRow($"Лимит оружия: {state.SegmentedWeaponLimit}/{OrbitalRunState.MaxSegmentedWeapons}",
            ("+1 ЛИМИТ", ready && state.SegmentedWeaponLimit < OrbitalRunState.MaxSegmentedWeapons,
                () => SandboxAction(() => GrantSandboxOrbitReward(OrbitalRewardKind.AddMount))));

        void AddStock(OrbitalRewardKind kind, string label)
        {
            var link = OrbitalRewardProvider.GetSegmentedLinkKind(kind);
            var weapon = OrbitalRewardProvider.GetModuleKind(kind);
            int stock = link.HasValue ? state.SegmentedLinkStock.Count(v => v == link.Value) :
                state.SegmentedWeapons.Count(v => v.ModuleType == weapon);
            SandboxRewardRow(kind, label, stock, OrbitalRewardIconResolver.Resolve(kind), ready);
        }
    }

    private void SandboxRewardRow(OrbitalRewardKind kind, string label, int stock, OrbitalRewardIconResolver.Icon icon, bool ready)
    {
        var row = SandboxIconRow(label, $"{label} · в запасе {stock}", icon,
            ("+1", ready && upgradeManager.DebugRewardUnavailableReason(kind) == null, () => GrantMvpBuildReward(kind)));
        row.GetComponentInChildren<Button>().name = "Rig reward · " + kind;
    }

    private void BuildSandboxCombat(bool ready)
    {
        bool manualReady = ready && enemySpawner != null && enemySpawner.isActiveAndEnabled;
        bool spawning = enemySpawner != null && enemySpawner.IsSpawningEnabled;
        bool autoReady = manualReady && CaptureSession.SpawnDensityUnavailableReason == null;
        SandboxRow(spawning ? "Автоспавн включён" : "Автоспавн выключен",
            (spawning ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", autoReady,
                () => SandboxAction(() => CaptureSession.SetSpawning(!enemySpawner.IsSpawningEnabled))));
        var health = PlayerRuntimeReference.PlayerHealth;
        bool invulnerable = health != null && health.DebugInvulnerable;
        SandboxRow(invulnerable ? "Неуязвимость включена" : "Неуязвимость выключена",
            (invulnerable ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", ready && health != null,
                () => SandboxAction(() => CaptureSession.SetInvulnerable(!health.DebugInvulnerable))));
        SandboxRow("Очистка арены · без опыта и наград",
            ("УБИТЬ ВСЕХ ВРАГОВ", ready,
                () => SandboxAction(() => sandboxStatus = $"Убрано врагов: {CaptureSession.ClearSandboxEnemies()}.")));

        BuildDebugCategory("Все враги", "Ручное добавление работает при выключенном автоспавне", () =>
        {
            foreach (var prefab in SandboxEnemyPrefabs().Where(prefab => prefab != null && prefab.GetComponent<EnemyHealth>() != null)
                         .Distinct().OrderBy(SandboxEnemyName, StringComparer.CurrentCulture))
            {
                string name = SandboxEnemyName(prefab);
                var renderer = prefab.GetComponent<EnemyWhiteFlash>()?.TargetRenderer;
                if (renderer == null || renderer.sprite == null)
                    renderer = prefab.GetComponentsInChildren<SpriteRenderer>()
                        .Where(sprite => sprite.enabled && sprite.sprite != null && sprite.color.a > 0f &&
                            sprite.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0 &&
                            sprite.sprite.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0)
                        .OrderByDescending(sprite => sprite.sprite.rect.width * sprite.sprite.rect.height)
                        .FirstOrDefault();
                var icon = renderer != null ? new OrbitalRewardIconResolver.Icon(renderer.sprite, renderer.color) : default;
                SandboxIconRow(prefab.name, name, icon,
                    ("+1", manualReady, () => SandboxAction(() => SpawnSandboxEnemies(prefab, name, 1))),
                    ("+5", manualReady, () => SandboxAction(() => SpawnSandboxEnemies(prefab, name, 5))));
            }
        }, expanded: false);
    }

    private IEnumerable<GameObject> SandboxEnemyPrefabs()
    {
        if (enemySpawner != null)
            foreach (var prefab in enemySpawner.DebugEnemyPrefabs())
                yield return prefab;
        yield return turretEnemyPrefab;
        yield return eyesEnemyPrefab;

        var sector = RunStateManager.Instance?.CurrentSector;
        yield return sector?.BossPrefab;
        foreach (var prefab in ProfileEnemies(sector?.SpawnProfile))
            yield return prefab;
        if (levelChoiceManager != null)
            foreach (var stage in levelChoiceManager.DebugStageProfiles)
            {
                yield return stage?.BossPrefab;
                foreach (var prefab in ProfileEnemies(stage?.SpawnProfile))
                    yield return prefab;
            }
        var map = MetaProgressionManager.Instance?.SurfaceMap?.Definition;
        if (map?.Sectors != null)
            foreach (var location in map.Sectors)
                foreach (var prefab in ProfileEnemies(location?.BuildRunConfig(map.Id).SpawnProfile))
                    yield return prefab;
    }

    private static IEnumerable<GameObject> ProfileEnemies(EnemySpawnProfile profile)
    {
        if (profile?.Phases == null) yield break;
        foreach (var phase in profile.Phases)
            if (phase?.enemies != null)
                foreach (var entry in phase.enemies)
                    if (entry != null) yield return entry.enemyPrefab;
    }

    private static string SandboxEnemyName(GameObject prefab) => prefab.name switch
    {
        "p_Enemy_classic" => "Преследователь",
        "p_Enemy_classic_start" => "Стартовый преследователь",
        "p_Enemy_default" => "Элитный преследователь",
        "p_Enemy_Flanker" => "Обходчик",
        "p_Enemy_Flanker_Start" => "Стартовый обходчик",
        "p_Enemy_Breacher" => "Проломщик",
        "p_Enemy_Breacher_Start" => "Стартовый проломщик",
        "p_Enemy_Shooter" => "Стрелок",
        "p_Enemy_Bomber" => "Подрывник",
        "BreakableShieldBearer" or "ShieldBearer" => "Ближний щитоносец",
        "BreakableShieldShooter" or "ShieldShooter" => "Стрелок со щитом",
        "p_Enemy_SpiderBigGait" => "Большой паук",
        "p_Enemy_SpiderBlue" => "Синий паук",
        "p_Enemy_SpiderDark" => "Тёмный паук",
        "p_Enemy_SpiderWhite" => "Белый паук",
        "p_EnemyTurret1" => "Турель 1",
        "p_EnemyTurret2" => "Турель 2",
        "p_EnemyEye1 Variant" => "Глаз",
        _ => prefab.GetComponent<EnemyHealth>() is { IsBoss: true } boss
            ? LocalizationService.EnsureExists().Get(boss.BossName)
            : prefab.name
    };

    private void SpawnSandboxEnemies(GameObject prefab, string name, int count)
    {
        var player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        if (enemySpawner == null || !enemySpawner.isActiveAndEnabled || player == null)
        {
            sandboxStatus = "Нужен активный спавнер и игрок.";
            return;
        }
        int spawned = 0;
        for (int i = 0; i < count; i++)
            if (enemySpawner.SpawnSpecificEnemyAround(prefab, player.position, 3f, 8f, 3f,
                    countTowardSpawnLimits: false, spawnClearance: .45f) != null)
                spawned++;
        sandboxStatus = $"{name} · добавлено {spawned} из {count}.";
        if (spawned < count) sandboxStatus += " Не хватило безопасных позиций.";
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

    private void OpenAllSandboxOrbits(OrbitalRunState state)
    {
        while (state.UnlockedSegmentedLevels < OrbitalRunState.SegmentedLevels)
            if (!GrantSandboxOrbitReward(OrbitalRewardKind.NewRing)) return;
    }

    private bool GrantSandboxOrbitReward(OrbitalRewardKind kind)
    {
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        if (reason == null && upgradeManager.DebugOfferCatalogueReward(kind, true)) return true;
        sandboxStatus = reason ?? "Награда недоступна: завершите текущее действие.";
        return false;
    }

    private void SandboxRow(string title, params (string Label, bool Enabled, UnityAction Action)[] actions)
    {
        SandboxIconRow(title, title, default, actions);
    }

    private RectTransform SandboxIconRow(string id, string title, OrbitalRewardIconResolver.Icon icon,
        params (string Label, bool Enabled, UnityAction Action)[] actions)
    {
        var row = CreateRect(id, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;

        if (icon.Sprite != null)
        {
            var image = CreateRect("Icon", row).gameObject.AddComponent<Image>();
            image.sprite = icon.Sprite;
            image.color = icon.ImageTint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.AddComponent<LayoutElement>().preferredWidth = 36f;
        }
        var label = CreateText("Название", row, title, 15f, TextAlignmentOptions.MidlineLeft, Color.white);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        foreach (var action in actions)
        {
            var button = CreateButton(row, action.Label, action.Action, 1f, action.Enabled);
            button.gameObject.AddComponent<LayoutElement>().preferredWidth = Mathf.Max(64f, action.Label.Length * 8f + 16f);
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13f;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        return row;
    }
}
#endif
