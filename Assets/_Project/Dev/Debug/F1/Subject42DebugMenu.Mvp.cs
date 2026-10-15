#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class Subject42DebugMenu
{
    private bool IsMvpDebugMenu => SceneManager.GetActiveScene().name == "MVP";
    private static readonly string[] MvpTabLabels = { "Орбиты и оружие", "Улучшения", "Враги и события", "Игрок и ран" };
    private int mvpCategoryFilter = -1;
    private bool mvpAvailableOnly;
    private OrbitalRewardKind mvpPreviewKind = OrbitalRewardKind.Pistol;
    private GameObject mvpPreviewRoot;
    private string mvpStatus;
    private OrbitalStationRuntime MvpStation => characterSpawner != null ? characterSpawner.Station : null;
    private bool MvpCanEdit => MvpStation != null && MvpStation.IsInitialized && !MvpStation.Owner.IsDead &&
        upgradeManager != null && upgradeManager.IsRewardQueueIdle && !SceneTransitionOverlay.IsTransitioning;

    private void ApplyMvpViewportLayout()
    {
        menuPanel.anchorMin = new Vector2(.045f, .045f); menuPanel.anchorMax = new Vector2(.955f, .955f);
        menuPanel.pivot = new Vector2(.5f, .5f); menuPanel.sizeDelta = Vector2.zero; menuPanel.anchoredPosition = Vector2.zero;
        menuHeader.sizeDelta = new Vector2(0f, 62f);
        menuTitle.text = "ПРОВЕРКА MVP · F1"; menuTitle.fontSize = 26f;
        Stretch(menuTitle.rectTransform, 22f, 90f);
        menuTabBar.gameObject.SetActive(true);
        closeButtonRect.sizeDelta = new Vector2(52f, 40f); closeButtonRect.anchoredPosition = new Vector2(-14f, 0f);
        visualBackButton?.SetActive(false);
        menuPages.offsetMin = new Vector2(18f, 18f); menuPages.offsetMax = new Vector2(-18f, -120f);
        fullMenuBlockerImage.color = new Color(0f, 0f, 0f, .68f); fullMenuBlockerImage.raycastTarget = true;
    }

    private void BuildMvpTab(int tab)
    {
        ResolveSceneReferences();
        if (mvpPreviewRoot != null) { mvpPreviewRoot.SetActive(false); Destroy(mvpPreviewRoot); mvpPreviewRoot = null; }
        var viewport = contentRoot.parent as RectTransform;
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = new Vector2(tab == 1 ? .65f : 1f, 1f);
        viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(-7f, 0f);
        var scrollbar = tabRoots[tab].GetComponent<ScrollRect>().verticalScrollbar;
        if (scrollbar != null)
        {
            var bar = scrollbar.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(tab == 1 ? .65f : 1f, 0f);
            bar.anchorMax = new Vector2(tab == 1 ? .65f : 1f, 1f);
        }
        var layout = contentRoot.GetComponent<VerticalLayoutGroup>(); layout.spacing = 8f;
        AddSectionTitle(MvpTabLabels[tab], "WASD — движение · ПКМ — станция к курсору · Shift — замедление · F1 / Esc — закрыть меню");
        if (!string.IsNullOrEmpty(mvpStatus)) AddHint(mvpStatus);
        switch (tab)
        {
            case 0: BuildMvpOrbits(); break;
            case 1: BuildMvpCatalogue(); break;
            case 2: BuildMvpEnemies(); break;
            case 3: BuildMvpPlayer(); break;
        }
    }

    private void MvpRow(string title, string status, string actionLabel = null, bool enabled = false, UnityEngine.Events.UnityAction action = null, bool selected = false)
    {
        RectTransform row = CreateRect(title, contentRoot);
        row.gameObject.AddComponent<Image>().color = selected ? new Color(.09f, .27f, .33f, 1f) : rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 68f;
        var text = CreateText("Содержание", row, "<b>" + title + "</b>\n<size=18>" + status + "</size>", 21f, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform, 16f, string.IsNullOrEmpty(actionLabel) ? 16f : 175f, 5f, 5f);
        text.textWrappingMode = TextWrappingModes.Normal;
        if (string.IsNullOrEmpty(actionLabel)) return;
        var button = CreateButton(row, actionLabel, action, 150f, enabled);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f); rect.pivot = new Vector2(1f, .5f);
        rect.anchoredPosition = new Vector2(-12f, 0f); rect.sizeDelta = new Vector2(150f, 42f);
    }

    private void BuildMvpOrbits()
    {
        var station = MvpStation;
        if (station == null || !station.IsInitialized) { AddHint("Станция ещё не готова. Начните обычный ран из бункера."); return; }
        foreach (var ring in station.Rings)
        {
            int id = ring.RingId;
            int occupied = station.State.Modules.Count(m => m.StableRingId == id);
            bool selected = station.SelectedRing == ring;
            MvpRow($"Орбита {ring.State.Order + 1}" ,
                $"Занято {occupied} · свободно {ring.State.MountCount - occupied} · построено {ring.State.MountCount}/{ring.State.MountCapacity}",
                selected ? "ВЫБРАНА" : "ВЫБРАТЬ", !selected && MvpCanEdit,
                () => { station.DebugSelectExistingRing(id); RefreshCurrentTab(); }, selected);
        }
        bool canRing = MvpCanEdit && station.State.CanAddRing(out _);
        MvpRow("Добавить орбиту", !MvpCanEdit ? "Завершите текущую награду или установку" : canRing ? $"Орбит {station.Rings.Count}/{OrbitalProgressionConfig.Default.MaxNormalRings}" : "Достигнут предел орбит",
            "ДОБАВИТЬ", canRing, () => { RunStateManager.Instance.DebugMarkDevelopmentRun(); station.AddRing(); if (station.State.UsesCustomPaths) CloseMenu(); else RefreshCurrentTab(); });
        int selectedId = station.SelectedRing?.RingId ?? 0;
        bool canMount = MvpCanEdit && station.State.CanAddMount(selectedId, out _);
        MvpRow("Добавить точку крепления", !MvpCanEdit ? "Завершите текущую награду или установку" : selectedId == 0 ? "Выберите орбиту" : canMount ? "На выбранную орбиту, в пределах её вместимости" : "Вместимость заполнена; доступное увеличение есть в «Улучшениях»",
            "ДОБАВИТЬ", canMount, () => { RunStateManager.Instance.DebugMarkDevelopmentRun(); station.AddMount(selectedId, out _); RefreshCurrentTab(); });
        AddSectionTitle("Добавить оружие", "Штатная установка на подсвеченные точки · Esc / ПКМ — отмена · шарики устанавливаются парой");
        foreach (var kind in new[] { OrbitalRewardKind.Pistol, OrbitalRewardKind.ArcEmitter, OrbitalRewardKind.LinkPair, OrbitalRewardKind.Sniper, OrbitalRewardKind.Shotgun, OrbitalRewardKind.Saw })
        {
            var data = upgradeManager.DebugRewardCatalogue.First(d => d.RewardKind == kind);
            string reason = upgradeManager.DebugRewardUnavailableReason(kind);
            MvpRow(LocalizationService.EnsureExists().Get(data.upgradeName), reason ?? "Выберите свободную точку после закрытия меню", "УСТАНОВИТЬ", reason == null, () => BeginMvpReward(kind, true));
        }
    }

    private void BeginMvpReward(OrbitalRewardKind kind, bool installImmediately = false)
    {
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        if (reason != null) { mvpStatus = reason; RefreshCurrentTab(); return; }
        CloseMenu();
        if (upgradeManager.DebugOfferCatalogueReward(kind, installImmediately)) return;
        mvpStatus = "Награда стала недоступна; состояние рана не изменено."; OpenMenu();
    }

    private void BuildMvpCatalogue()
    {
        if (upgradeManager == null) { AddHint("Система наград ещё не готова."); return; }
        var filters = CreateRect("Фильтры категорий", contentRoot);
        filters.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        var horizontal = filters.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 6f;
        horizontal.childControlWidth = true; horizontal.childForceExpandWidth = true; horizontal.childControlHeight = true;
        for (int i = -1; i < 4; i++)
        {
            int category = i;
            string label = i < 0 ? "Все категории" : OrbitalCategories.Label((OrbitalUpgradeCategory)i);
            var button = CreateButton(filters, label, () => { mvpCategoryFilter = category; RefreshCurrentTab(); }, 120f);
            button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (category == mvpCategoryFilter) button.targetGraphic.color = successColor;
        }
        MvpRow("Доступность", mvpAvailableOnly ? "Только доступные сейчас" : "Все активные определения, включая временно недоступные", mvpAvailableOnly ? "ДОСТУПНЫЕ" : "ВСЕ", true,
            () => { mvpAvailableOnly = !mvpAvailableOnly; RefreshCurrentTab(); });
        bool canRandom = MvpCanEdit && upgradeManager.GetEligibleNormalRewards().Count > 0;
        MvpRow("Случайный выбор из трёх", canRandom ? "Настоящий пул и обычные требования; при нехватке вариантов показываются только существующие" : "Нет доступных вариантов или уже открыта награда", "ПОКАЗАТЬ", canRandom,
            () => { CloseMenu(); upgradeManager.DebugOfferRandomCatalogueChoices(); });
        var all = upgradeManager.DebugRewardCatalogue;
        var filtered = all.Where(d => (mvpCategoryFilter < 0 || (int)d.EffectCategory == mvpCategoryFilter) &&
            (!mvpAvailableOnly || upgradeManager.DebugRewardUnavailableReason(d.RewardKind) == null)).ToArray();
        AddHint($"Показано {filtered.Length} из {all.Count}. Выберите «Карточка» для просмотра в штатном UI справа.");
        foreach (var data in filtered)
        {
            string reason = upgradeManager.DebugRewardUnavailableReason(data.RewardKind);
            var row = CreateRect("Каталог · " + data.RewardKind, contentRoot);
            row.gameObject.AddComponent<Image>().color = data.RewardKind == mvpPreviewKind ? new Color(.09f, .23f, .29f, 1f) : rowColor;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 164f;
            var text = CreateText("Определение карточки", row,
                $"<b>{LocalizationService.EnsureExists().Get(data.upgradeName)}</b> · {OrbitalCategories.Label(data.EffectCategory)}\n" +
                ProductionUpgradePresentation.GetCardDescription(data) + "\n<size=17>" + upgradeManager.DebugRewardLevel(data) + "\n" +
                (reason == null ? "<color=#75D895>Доступно сейчас</color>" : "<color=#FFC16A>" + reason + "</color>") + "</size>",
                19f, TextAlignmentOptions.MidlineLeft, Color.white);
            text.textWrappingMode = TextWrappingModes.Normal; Stretch(text.rectTransform, 12f, 148f, 5f, 5f);
            var button = CreateButton(row, "КАРТОЧКА", () => { mvpPreviewKind = data.RewardKind; RefreshCurrentTab(); }, 128f);
            var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f); rect.pivot = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-10f, 0f); rect.sizeDelta = new Vector2(128f, 40f);
        }
        var selected = filtered.FirstOrDefault(d => d.RewardKind == mvpPreviewKind) ?? filtered.FirstOrDefault();
        if (selected == null) { AddHint("Нет карточек, соответствующих выбранным фильтрам."); return; }
        mvpPreviewKind = selected.RewardKind;
        BuildMvpCardPreview(selected);
    }

    private void BuildMvpCardPreview(OrbitalRewardData data)
    {
        if (upgradeManager.DebugCardTemplate == null) return;
        var preview = CreateRect("Настоящая карточка награды", tabRoots[(int)activeTab].transform); mvpPreviewRoot = preview.gameObject;
        preview.anchorMin = new Vector2(.665f, 0f); preview.anchorMax = Vector2.one; preview.offsetMin = Vector2.zero; preview.offsetMax = Vector2.zero;
        preview.gameObject.AddComponent<Image>().color = rowColor;
        var heading = CreateText("Заголовок просмотра", preview, "ШТАТНАЯ КАРТОЧКА", 22f, TextAlignmentOptions.Center, Color.white);
        heading.rectTransform.anchorMin = new Vector2(0f, 1f); heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.pivot = new Vector2(.5f, 1f); heading.rectTransform.sizeDelta = new Vector2(0f, 46f);
        var card = Instantiate(upgradeManager.DebugCardTemplate, preview, false);
        var rect = card.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -54f); rect.sizeDelta = new Vector2(300f, 500f); rect.localScale = Vector3.one;
        // Keep the authored normal card appearance even when its reward is unavailable.
        // Applying is a separate button with the real eligibility check.
        card.Setup(data, null);
        var status = CreateText("Доступность карточки", preview, upgradeManager.DebugRewardLevel(data) + "\n\n" +
            (upgradeManager.DebugRewardUnavailableReason(data.RewardKind) ?? "Доступно. Откроется обычный выбор этой карточки; затем выберите её, чтобы применить."),
            19f, TextAlignmentOptions.TopLeft, Color.white);
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = new Vector2(0f, 0f); status.rectTransform.anchorMax = new Vector2(1f, 1f);
        status.rectTransform.offsetMin = new Vector2(16f, 70f); status.rectTransform.offsetMax = new Vector2(-16f, -572f);
        string reason = upgradeManager.DebugRewardUnavailableReason(data.RewardKind);
        var apply = CreateButton(preview, "ВЫБРАТЬ НАГРАДУ", () => BeginMvpReward(data.RewardKind), 270f, reason == null);
        var applyRect = apply.GetComponent<RectTransform>(); applyRect.anchorMin = applyRect.anchorMax = new Vector2(.5f, 0f); applyRect.pivot = new Vector2(.5f, 0f);
        applyRect.anchoredPosition = new Vector2(0f, 15f); applyRect.sizeDelta = new Vector2(270f, 42f);
    }

    private void BuildMvpEnemies()
    {
        bool available = enemySpawner != null;
        MvpRow("Обычная генерация врагов", available ? enemySpawner.IsSpawningEnabled ? "Включена" : "Остановлена" : "Спавнер не готов", "ПЕРЕКЛЮЧИТЬ", available, ToggleEnemyAutoSpawn);
        MvpRow("Поведение врагов", EnemyDebugAiFreeze.IsFrozen ? "AI заморожен" : "AI работает", "ПЕРЕКЛЮЧИТЬ", available, ToggleEnemyAiFreeze);
        var profile = RunStateManager.Instance?.CurrentSector?.SpawnProfile;
        if (profile != null)
            foreach (var prefab in profile.Phases.Where(p => p != null).SelectMany(p => p.enemies).Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab).Distinct())
            {
                string name = prefab.GetComponent<EnemyDirectionalShield>() != null ? prefab.GetComponent<EnemyShooterMovement>() != null ? "Стрелок со щитом" : "Ближний щитоносец" :
                    prefab.GetComponent<EnemyShooterMovement>() != null ? "Стрелок" : prefab.GetComponent<EnemyBomberMovement>() != null ? "Подрывник" : "Обычный враг";
                MvpRow(name, prefab.name + " · одна безопасная ручная установка через спавнер", "+1 ВРАГ", available && MvpStation != null,
                    () => { RunStateManager.Instance.DebugMarkDevelopmentRun(); SpawnDebugEnemies(prefab, name, 1); });
            }
        MvpRow("Убрать созданных вручную врагов", $"Учтено: {debugEnemies.Count(e => e != null)}", "УБРАТЬ", debugEnemies.Any(e => e != null), ClearDebugEnemies);
        var sector = ProductionExplorationSectorController.ActiveInstance;
        if (sector != null)
        {
            AddSectionTitle("События текущего сектора", "Существующие игровые сервисы, без лабораторных пресетов");
            MvpRow("Контейнер награды рядом с игроком", "Настоящее содержимое и обычная выдача награды", "СОЗДАТЬ", MvpCanEdit && !WorldLootRewardReel.IsActive, () => { CloseMenu(); sector.DebugSpawnRewardChestNearPlayer(); });
            MvpRow("Парные порталы", "Безопасное размещение существующим сервисом", "СОЗДАТЬ", MvpCanEdit, () => { mvpStatus = sector.SpawnPortalPair() ? "Порталы созданы" : "Нет безопасных мест для порталов"; RefreshCurrentTab(); });
            MvpRow("Ресурсные узлы", "Создать узлы в текущем секторе", "СОЗДАТЬ", MvpCanEdit, () => { mvpStatus = $"Создано узлов: {sector.SpawnResourceNodes()}"; RefreshCurrentTab(); });
        }
        AddHint(enemyDebugStatus);
    }

    private void BuildMvpPlayer()
    {
        var player = PlayerRuntimeReference.CachedPlayer; var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        var run = RunStateManager.Instance; var experience = ExperienceManager.Instance;
        MvpRow("Здоровье игрока", health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "Игрок не готов", "ВОССТАНОВИТЬ", health != null && !health.IsDead, () => { health.Heal(health.MaxHealth); RefreshCurrentTab(); });
        MvpRow("Неуязвимость", health != null && health.DebugInvulnerable ? "Включена" : "Выключена", "ПЕРЕКЛЮЧИТЬ", health != null, () => { run?.DebugMarkDevelopmentRun(); health.DebugInvulnerable = !health.DebugInvulnerable; RefreshCurrentTab(); });
        MvpRow("Опыт до следующего уровня", experience != null ? $"Уровень {experience.CurrentLevel} · {experience.CurrentExp}/{experience.ExpToNextLevel}" : "Система опыта не готова", "ДАТЬ ОПЫТ", MvpCanEdit && experience != null,
            () => { CloseMenu(); run.DebugMarkDevelopmentRun(); experience.AddExperience(Mathf.Max(1, experience.ExpToNextLevel - experience.CurrentExp)); });
        MvpRow("Текущий ран", $"Сектор {run?.CurrentSector?.SectorNumber} · орбит {MvpStation?.Rings.Count} · оружий {MvpStation?.Modules.Count} · угроза {run?.ThreatValue:0.0}");
        bool canExit = MvpCanEdit && runFlowController != null && runFlowController.CanDebugCompleteCurrentLevel && runFlowController.IsExitUnlocked;
        MvpRow("Завершить текущий сектор", canExit ? "Через штатный выход и выбор следующего сектора" : "Выход ещё недоступен или ран занят наградой", "ЗАВЕРШИТЬ", canExit, () => { CloseMenu(); CompleteLevel(); });
        MvpRow("Вернуться в бункер", "Штатное завершение рана и очистка временного состояния", "ВЕРНУТЬСЯ", run != null && !run.IsRunEnded && RunEndService.Instance != null && !SceneTransitionOverlay.IsTransitioning,
            () => { CloseMenu(); RunEndService.Instance.ReturnToBunker(); });
    }
}
#endif
