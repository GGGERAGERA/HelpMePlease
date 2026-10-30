#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class Subject42DebugMenu
{
    private bool IsMvpDebugMenu => SceneManager.GetActiveScene().name == "MVP" && !hubLegacyLab;
    private static readonly string[] MvpTabLabels = { "Орбиты и оружие", "Улучшения", "Враги и события", "Игрок и ран" };
    private int mvpCategoryFilter = -1;
    private bool mvpAvailableOnly;
    private OrbitalRewardKind mvpPreviewKind = OrbitalRewardKind.ShootingDamage;
    private GameObject mvpPreviewRoot;
    private string mvpStatus;
    private string mvpRingSurfaceBackup;
    private float mvpRingWidthBackup, mvpRingAlphaBackup;
    private bool mvpOrbitSurfaceOpen;
    private Vector2 mvpOrbitSurfacePosition = new(-14f, 0f);
    private Vector2 mvpOrbitDragPointer, mvpOrbitDragPosition;
    private RectTransform mvpOrbitSaveFooter;
    private TextMeshProUGUI mvpOrbitSaveStatus;
    private OrbitalStationRuntime MvpStation => characterSpawner != null ? characterSpawner.Station : null;
    private bool MvpCanEdit => MvpStation != null && MvpStation.IsInitialized && !MvpStation.Owner.IsDead &&
        upgradeManager != null && upgradeManager.IsRewardQueueIdle && !SceneTransitionOverlay.IsTransitioning;

    private void ApplyMvpViewportLayout()
    {
        if (mvpOrbitSurfaceOpen)
        {
            menuPanel.anchorMin = menuPanel.anchorMax = new Vector2(1f, .5f);
            menuPanel.pivot = new Vector2(1f, .5f);
            menuPanel.anchoredPosition = mvpOrbitSurfacePosition;
            ConstrainMvpOrbitSurfacePosition();
            menuHeader.sizeDelta = new Vector2(0f, 44f);
            menuTitle.text = "Внешний вид орбит"; menuTitle.fontSize = 19f;
            Stretch(menuTitle.rectTransform, 12f, 112f);
            menuTabBar.gameObject.SetActive(false);
            closeButtonRect.sizeDelta = new Vector2(32f, 26f); closeButtonRect.anchoredPosition = new Vector2(-8f, 0f);
            visualBackButton.SetActive(true);
            mvpOrbitSaveFooter.gameObject.SetActive(true);
            menuPages.offsetMin = new Vector2(8f, 66f); menuPages.offsetMax = new Vector2(-8f, -48f);
            fullMenuBlockerImage.color = Color.clear; fullMenuBlockerImage.raycastTarget = false;
            return;
        }
        menuPanel.anchorMin = new Vector2(.045f, .045f); menuPanel.anchorMax = new Vector2(.955f, .955f);
        menuPanel.pivot = new Vector2(.5f, .5f); menuPanel.sizeDelta = Vector2.zero; menuPanel.anchoredPosition = Vector2.zero;
        menuHeader.sizeDelta = new Vector2(0f, 62f);
        menuTitle.text = "Отладка · F1"; menuTitle.fontSize = 24f;
        Stretch(menuTitle.rectTransform, 22f, 90f);
        menuTabBar.gameObject.SetActive(true);
        closeButtonRect.sizeDelta = new Vector2(52f, 40f); closeButtonRect.anchoredPosition = new Vector2(-14f, 0f);
        visualBackButton?.SetActive(false);
        mvpOrbitSaveFooter?.gameObject.SetActive(false);
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
        if (mvpOrbitSurfaceOpen)
        {
            layout.spacing = 5f;
            BuildMvpOrbitSurface();
            return;
        }
        AddSectionTitle(MvpTabLabels[tab], tab == 0 ? "Предметы поступают в запас конструктора." : "F1 / Esc — закрыть меню");
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
        bool compact = mvpOrbitSurfaceOpen || activeTab == (IsMvpDebugMenu ? (DebugTab)0 : DebugTab.OrbitalProduction);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = compact ? 54f : 68f;
        var text = CreateText("Содержание", row, "<b>" + title + $"</b>\n<size={(compact ? 11 : 18)}>" + status + "</size>", compact ? 14f : 21f, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform, compact ? 8f : 16f, string.IsNullOrEmpty(actionLabel) ? 8f : compact ? 108f : 175f, 5f, 5f);
        text.textWrappingMode = TextWrappingModes.Normal;
        if (string.IsNullOrEmpty(actionLabel)) return;
        var button = CreateButton(row, actionLabel, action, compact ? 94f : 150f, enabled);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f); rect.pivot = new Vector2(1f, .5f);
        rect.anchoredPosition = new Vector2(compact ? -7f : -12f, 0f); rect.sizeDelta = compact ? new Vector2(94f, 30f) : new Vector2(150f, 42f);
        if (compact)
        {
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 12f;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }

    private static bool IsMvpBuildReward(OrbitalRewardKind kind) => kind is
        OrbitalRewardKind.Pistol or OrbitalRewardKind.Sniper or OrbitalRewardKind.Shotgun or OrbitalRewardKind.Saw or
        OrbitalRewardKind.NewRing or OrbitalRewardKind.AddMount || OrbitalRewardProvider.GetSegmentedLinkKind(kind).HasValue;

    private void BuildMvpOrbits()
    {
        var station = MvpStation;
        if (station == null || !station.IsInitialized || !station.UsesSegmentedOrbit || upgradeManager == null)
        { AddHint("Segmented Orbit ещё не готова. Начните обычный ран из бункера."); return; }
        var state = station.State;
        AddSectionTitle("Эксперимент · Development", "Меняет выбранный тип оружия этой попытки, включая будущие экземпляры. Production-прогресс не засчитывается.");
        var activeWeaponVariants = new System.Collections.Generic.List<string>();
        if (state.MissReturnPistolExperiment) activeWeaponVariants.Add("Возврат промаха");
        if (state.ChargedLineSniperExperiment) activeWeaponVariants.Add("Заряженный снайпер");
        if (state.SweepingShotgunExperiment) activeWeaponVariants.Add("Веерная очередь дробовика");
        if (state.RamSlugShotgunExperiment) activeWeaponVariants.Add("Картечный таран");
        if (state.PulsedSawExperiment) activeWeaponVariants.Add("Импульсная пила");
        if (state.InertialSawExperiment) activeWeaponVariants.Add("Инерционная пила");
        MvpRow("Активные варианты оружия", activeWeaponVariants.Count == 0 ? "Все выключены" : string.Join(" · ", activeWeaponVariants));
        AddHint("Пистолет доступен со старта. Остальные типы получите через штатные награды ниже в запас; закройте F1 и установите оружие через Tab.");
        bool missReturnPistol = state.MissReturnPistolExperiment;
        MvpRow("Эксперимент: возврат промаха" + (missReturnPistol ? " · ВКЛ" : " · ВЫКЛ"),
            "Пистолет: вместо 8 м прямо — 4 м наружу + 4 м обратно только при промахе. Линия фиксирована в мире: заведите на неё преследователей. Все пистолеты попытки; Development без production-прогресса.",
            missReturnPistol ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", MvpCanEdit, () =>
            {
                if (!MvpCanEdit || !station.DebugSetMissReturnPistolExperiment(!missReturnPistol)) return;
                mvpStatus = "Попытка Development. Возврат промаха: раскладка, улучшения и cooldown сохранены. OFF возвращает обычный профиль будущим выстрелам; летящие заканчивают прежний путь. Новый ран — без эксперимента.";
                RefreshCurrentTab();
            });
        bool chargedSniper = state.ChargedLineSniperExperiment;
        MvpRow("Заряженный линейный Sniper" + (chargedSniper ? " · ВКЛ" : " · ВЫКЛ"),
            "0,45 с подготовки → до 3 тел по 24 урона вместо 32 в одно. Выстройте толпу в линию; Q/E ведут направление до выпуска.",
            chargedSniper ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", MvpCanEdit, () =>
            {
                if (!MvpCanEdit || !station.DebugSetChargedLineSniperExperiment(!chargedSniper)) return;
                mvpStatus = "Попытка Development. Эксперимент меняет все Sniper; раскладка и улучшения сохранены. Новый обычный ран — без эксперимента.";
                RefreshCurrentTab();
            });
        bool sweepingShotgun = state.SweepingShotgunExperiment;
        bool ramSlugShotgun = state.RamSlugShotgunExperiment;
        MvpRow("Веерная очередь Shotgun" + (sweepingShotgun ? " · ВКЛ" : " · ВЫКЛ"),
            "Вместо 7 дробин сразу: 3 залпа по 2, шаг 0,12 с, слева → центр → справа. Q/E ведут очередь; 2X — один центральный залп. Все Shotgun этой попытки; Development без production-прогресса.",
            sweepingShotgun ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", MvpCanEdit && (sweepingShotgun || !state.RamSlugShotgunExperiment), () =>
            {
                if (!MvpCanEdit || !station.DebugSetSweepingShotgunExperiment(!sweepingShotgun)) return;
                mvpStatus = "Попытка Development. Shotgun: раскладка и улучшения сохранены; выключение возвращает обычный веер. Новый ран — без эксперимента.";
                RefreshCurrentTab();
            });
        if (!sweepingShotgun && ramSlugShotgun)
            AddHint("Веерная очередь недоступна: сначала выключите картечный таран.");
        MvpRow("Эксперимент: картечный таран" + (ramSlugShotgun ? " · ВКЛ" : " · ВЫКЛ"),
            "Shotgun: вместо широкого веера один медленный короткий снаряд. Урон 10, дальность 3,5 м, до трёх тел; штатное оттеснение поддерживаемых врагов, без обхода щитов. Q/E направляют линию. Development без production-прогресса.",
            ramSlugShotgun ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", MvpCanEdit && (ramSlugShotgun || !state.SweepingShotgunExperiment), () =>
            {
                if (!MvpCanEdit || !station.DebugSetRamSlugShotgunExperiment(!ramSlugShotgun)) return;
                mvpStatus = "Попытка Development без production-прогресса. Картечный таран: установки, улучшения, cooldown и летящие снаряды сохранены. Уже начатая очередь завершается прежним профилем; будущие выстрелы используют выбранный профиль. OFF возвращает обычный Shotgun. Новый ран — без эксперимента.";
                RefreshCurrentTab();
            });
        if (!ramSlugShotgun && sweepingShotgun)
            AddHint("Картечный таран недоступен: сначала выключите веерную очередь.");
        bool inertialSaw = state.InertialSawExperiment;
        bool pulsedSaw = state.PulsedSawExperiment;
        MvpRow("Импульсная пила" + (pulsedSaw ? " · ВКЛ" : " · ВЫКЛ"),
            "0,8 с: увеличенный контакт ×1,35 и вращение; 0,4 с: обычный размер, остановка и нет урона. Подводите пилу в активное окно. Все Saw этой попытки; Development без production-прогресса.",
            pulsedSaw ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", MvpCanEdit && (pulsedSaw || !state.InertialSawExperiment), () =>
            {
                if (!MvpCanEdit || !station.DebugSetPulsedSawExperiment(!pulsedSaw)) return;
                mvpStatus = "Попытка Development без production-прогресса. Импульсная пила: установки, улучшения и интервалы поражённых целей сохранены; выключение возвращает непрерывный контакт. Новый ран — без эксперимента.";
                RefreshCurrentTab();
            });
        if (!pulsedSaw && inertialSaw)
            AddHint("Импульсная пила недоступна: сначала выключите инерционную пилу.");
        MvpRow("Эксперимент: инерционная пила" + (inertialSaw ? " · ВКЛ" : " · ВЫКЛ"),
            "Контакт только при фактическом вращении установленного кольца штатным Q/E: урон ×1,5, прежние радиус и интервал. Без вращения контакта нет; расходуется существующая энергия. Первая итерация с положительным шагом времени после установки/переключения только запоминает фазу. Development без production-прогресса.",
            inertialSaw ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", MvpCanEdit && (inertialSaw || !state.PulsedSawExperiment), () =>
            {
                if (!MvpCanEdit || !station.DebugSetInertialSawExperiment(!inertialSaw)) return;
                mvpStatus = "Попытка Development без production-прогресса. Инерционная пила: установки, улучшения и интервалы поражённых целей сохранены. Первая итерация с положительным шагом времени только запоминает фазу; OFF возвращает обычный контакт. Новый ран — без эксперимента.";
                RefreshCurrentTab();
            });
        if (!inertialSaw && pulsedSaw)
            AddHint("Инерционная пила недоступна: сначала выключите импульсную пилу.");
        AddHint("Выдача пополняет запас. Закройте карточку/F1, откройте Tab и перетащите предмет на подходящую ячейку. Установленное оружие занимает общий лимит.");
        AddSectionTitle("Оружие", "");
        foreach (var kind in new[] { OrbitalRewardKind.Pistol, OrbitalRewardKind.Sniper, OrbitalRewardKind.Shotgun, OrbitalRewardKind.Saw })
        {
            var module = OrbitalRewardProvider.GetModuleKind(kind);
            AddMvpBuildReward(kind, $"Установлено: {state.Modules.Count(w => w.ModuleType == module)} · В запасе: {state.SegmentedWeapons.Count(w => w.ModuleType == module)}");
        }
        AddSectionTitle("Модули", "");
        foreach (var kind in new[] { OrbitalRewardKind.DamageLink, OrbitalRewardKind.FireRateLink,
                     OrbitalRewardKind.ExtraShotLink, OrbitalRewardKind.RicochetLink })
        {
            var link = OrbitalRewardProvider.GetSegmentedLinkKind(kind).Value;
            AddMvpBuildReward(kind, $"Установлено: {state.SegmentedLinks.Count(m => m.Kind == link)} · В запасе: {state.SegmentedLinkStock.Count(m => m == link)}");
        }
        AddSectionTitle("Орбиты / крепления", "");
        AddMvpBuildReward(OrbitalRewardKind.NewRing, $"Открыто: {state.UnlockedSegmentedLevels}/{OrbitalRunState.SegmentedLevels}", "ОТКРЫТЬ");
        AddMvpBuildReward(OrbitalRewardKind.AddMount, $"Лимит оружия: {state.SegmentedWeaponLimit}/{OrbitalRunState.MaxSegmentedWeapons}", "+1 ЛИМИТ");
        MvpRow("Внешний вид орбит", "F9", "НАСТРОИТЬ",
            MvpCanEdit && previousTimeScale > 0f, OpenMvpOrbitSurface);
    }

    private void AddMvpBuildReward(OrbitalRewardKind kind, string status, string actionLabel = "ВЫДАТЬ")
    {
        var data = upgradeManager.DebugRewardCatalogue.FirstOrDefault(d => d.RewardKind == kind);
        if (data == null) return;
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        MvpRow(LocalizationService.EnsureExists().Get(data.upgradeName), reason ?? status,
            actionLabel, MvpCanEdit && reason == null, () => GrantMvpBuildReward(kind));
    }

    private void GrantMvpBuildReward(OrbitalRewardKind kind)
    {
        if (!MvpCanEdit || !IsMvpBuildReward(kind)) return;
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        if (reason != null) { mvpStatus = reason; RefreshCurrentTab(); return; }
        CloseMenu();
        if (upgradeManager.DebugOfferCatalogueReward(kind, true)) return;
        mvpStatus = "Награда стала недоступна; состояние рана не изменено.";
        OpenMenu();
    }

    private void OpenMvpOrbitSurface()
    {
        if (!isOpen || !MvpCanEdit || previousTimeScale <= 0f) return;
        mvpOrbitSurfaceOpen = true;
        menuLiveSimulation = true;
        Time.timeScale = previousTimeScale;
        ApplyMvpViewportLayout();
        RefreshCurrentTab();
    }

    private void ToggleMvpOrbitSurface()
    {
        if (mvpOrbitSurfaceOpen) { CloseMenu(); return; }
        ResolveSceneReferences();
        if (!MvpCanEdit || MvpStation.IsRunEnded || (isOpen ? previousTimeScale : Time.timeScale) <= 0f) return;
        if (!isOpen) OpenMenu();
        SelectTab((DebugTab)0, false);
        OpenMvpOrbitSurface();
    }

    private void ConfigureMvpOrbitSurfaceDrag()
    {
        menuTitle.raycastTarget = true;
        var trigger = menuTitle.gameObject.AddComponent<EventTrigger>();
        var begin = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        begin.callback.AddListener(data =>
        {
            var pointer = (PointerEventData)data;
            if (!mvpOrbitSurfaceOpen || pointer.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)menuPanel.parent,
                pointer.position, pointer.pressEventCamera, out mvpOrbitDragPointer);
            mvpOrbitDragPosition = menuPanel.anchoredPosition;
        });
        var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener(data =>
        {
            var pointer = (PointerEventData)data;
            if (!mvpOrbitSurfaceOpen || pointer.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)menuPanel.parent,
                pointer.position, pointer.pressEventCamera, out var local);
            menuPanel.anchoredPosition = mvpOrbitDragPosition + local - mvpOrbitDragPointer;
            ConstrainMvpOrbitSurfacePosition();
        });
        trigger.triggers.Add(begin);
        trigger.triggers.Add(drag);
    }

    private void ConstrainMvpOrbitSurfacePosition()
    {
        var viewport = ((RectTransform)menuPanel.parent).rect.size;
        float width = Mathf.Min(420f, viewport.x - 16f);
        float height = Mathf.Min(680f, viewport.y - 16f);
        menuPanel.sizeDelta = new Vector2(width, height);
        var position = menuPanel.anchoredPosition;
        position.x = Mathf.Clamp(position.x, width - viewport.x + 8f, -8f);
        float verticalMargin = Mathf.Max(0f, (viewport.y - height) * .5f - 8f);
        position.y = Mathf.Clamp(position.y, -verticalMargin, verticalMargin);
        menuPanel.anchoredPosition = mvpOrbitSurfacePosition = position;
    }

    private void ReturnFromMvpOrbitSurface()
    {
        mvpOrbitSurfaceOpen = false;
        menuLiveSimulation = false;
        Time.timeScale = 0f;
        ApplyMvpViewportLayout();
        RefreshCurrentTab();
    }

    private void BuildMvpOrbitSaveFooter()
    {
        mvpOrbitSaveFooter = CreateRect("Сохранить оформление", menuPanel);
        mvpOrbitSaveFooter.anchorMin = Vector2.zero;
        mvpOrbitSaveFooter.anchorMax = new Vector2(1f, 0f);
        mvpOrbitSaveFooter.pivot = new Vector2(.5f, 0f);
        mvpOrbitSaveFooter.sizeDelta = new Vector2(-16f, 50f);
        mvpOrbitSaveFooter.anchoredPosition = new Vector2(0f, 8f);
        mvpOrbitSaveFooter.gameObject.AddComponent<Image>().color = rowColor;
        mvpOrbitSaveStatus = CreateText("Статус", mvpOrbitSaveFooter,
            "Сохранить между запусками", 13f, TextAlignmentOptions.MidlineLeft, Color.white);
        mvpOrbitSaveStatus.textWrappingMode = TextWrappingModes.Normal;
        Stretch(mvpOrbitSaveStatus.rectTransform, 8f, 130f, 5f, 5f);
        var button = CreateButton(mvpOrbitSaveFooter, "СОХРАНИТЬ", SaveMvpOrbitSurface, 114f);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13f;
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
        rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = new Vector2(114f, 34f);
        rect.anchoredPosition = new Vector2(-7f, 0f);
        mvpOrbitSaveFooter.gameObject.SetActive(false);
    }

    private void SaveMvpOrbitSurface()
    {
        var config = OrbitalPresentationConfig.Active;
        config.SaveRingAppearance();
        mvpRingSurfaceBackup = JsonUtility.ToJson(config.RingSurface);
        mvpRingWidthBackup = config.RingLineWidth;
        mvpRingAlphaBackup = config.RingLineAlpha;
        mvpOrbitSaveStatus.text = "Сохранено между запусками";
    }

    private void BuildMvpOrbitSurface()
    {
        var config = OrbitalPresentationConfig.Active;
        var surface = config.RingSurface;
        if (mvpRingSurfaceBackup == null)
        {
            mvpRingSurfaceBackup = JsonUtility.ToJson(surface);
            mvpRingWidthBackup = config.RingLineWidth;
            mvpRingAlphaBackup = config.RingLineAlpha;
        }
        AddSectionTitle("Внешний вид орбит", "F9 / Esc — закрыть · Тяните за заголовок");
        string pattern = surface.Pattern switch
        {
            OrbitalPresentationConfig.RingPattern.Dashed => "Пунктир",
            OrbitalPresentationConfig.RingPattern.Energy => "Поток энергии",
            _ => "Сплошная линия"
        };
        MvpRow("Рисунок линии", pattern, "СМЕНИТЬ", true, () =>
        {
            surface.Pattern = (OrbitalPresentationConfig.RingPattern)(((int)surface.Pattern + 1) % 3);
            mvpOrbitSaveStatus.text = "Изменения не сохранены";
            RefreshCurrentTab();
        });
        MvpOrbitSlider("Насыщенность", surface.Saturation, 0f, 2f, v => surface.Saturation = v);
        MvpOrbitSlider("Прозрачность", config.RingLineAlpha, 0f, 1f, v => config.RingLineAlpha = v);
        MvpOrbitSlider("Яркость", surface.Brightness, 0f, 3f, v => surface.Brightness = v);
        MvpOrbitSlider("Толщина линии", config.RingLineWidth, .008f, .12f, v => config.RingLineWidth = v, "0.000");
        MvpOrbitSlider("Свечение", surface.Glow, 0f, 3f, v => surface.Glow = v);
        MvpOrbitSlider("Число штрихов", surface.DashCount, 8f, 128f, v => surface.DashCount = Mathf.RoundToInt(v), "0");
        MvpOrbitSlider("Заполнение пунктира", surface.DashFill, .1f, .95f, v => surface.DashFill = v);
        MvpOrbitSlider("Скорость рисунка", surface.PatternSpeed, -.3f, .3f, v => surface.PatternSpeed = v);
        MvpRow("Вернуть настройки", "Последние сохранённые значения или значения при первом открытии меню", "ВЕРНУТЬ", true, () =>
        {
            JsonUtility.FromJsonOverwrite(mvpRingSurfaceBackup, surface);
            config.RingLineWidth = mvpRingWidthBackup; config.RingLineAlpha = mvpRingAlphaBackup;
            mvpOrbitSaveStatus.text = "Настройки восстановлены";
            RefreshCurrentTab();
        });
    }

    private void MvpOrbitSlider(string label, float value, float minimum, float maximum, Action<float> setter, string format = "0.00")
    {
        AddSliderRow(label, value, minimum, maximum, v =>
        {
            setter(v);
            mvpOrbitSaveStatus.text = "Изменения не сохранены";
        }, format);
        var row = contentRoot.GetChild(contentRoot.childCount - 1);
        if (mvpOrbitSurfaceOpen)
            row.GetComponentInChildren<Slider>().navigation = new Navigation { mode = Navigation.Mode.None };
        row.GetComponent<LayoutElement>().preferredHeight = mvpOrbitSurfaceOpen ? 38f : 46f;
        foreach (var text in row.GetComponentsInChildren<TextMeshProUGUI>()) text.fontSize = mvpOrbitSurfaceOpen ? 14f : 18f;
    }

    private void BeginMvpReward(OrbitalRewardKind kind)
    {
        if (IsMvpBuildReward(kind)) return;
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        if (reason != null) { mvpStatus = reason; RefreshCurrentTab(); return; }
        CloseMenu();
        if (upgradeManager.DebugOfferCatalogueReward(kind)) return;
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
            if (i == (int)OrbitalUpgradeCategory.Orbits) continue;
            int category = i;
            string label = i < 0 ? "Все категории" : OrbitalCategories.Label((OrbitalUpgradeCategory)i);
            var button = CreateButton(filters, label, () => { mvpCategoryFilter = category; RefreshCurrentTab(); }, 120f);
            button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (category == mvpCategoryFilter) button.targetGraphic.color = successColor;
        }
        MvpRow("Доступность", mvpAvailableOnly ? "Только доступные сейчас" : "Все активные определения, включая временно недоступные", mvpAvailableOnly ? "ДОСТУПНЫЕ" : "ВСЕ", true,
            () => { mvpAvailableOnly = !mvpAvailableOnly; RefreshCurrentTab(); });
        var all = upgradeManager.DebugRewardCatalogue.Where(d => !IsMvpBuildReward(d.RewardKind)).ToList();
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
        MvpRow("Убрать созданных вручную врагов", $"Учтено: {debugEnemies.Count(e => e != null)}", "УБРАТЬ", debugEnemies.Any(e => e != null), ClearDebugEnemies);
        var activeExperiments = new System.Collections.Generic.List<string>();
        if (available)
        {
            if (enemySpawner.FlankingChasersExperiment) activeExperiments.Add("Фланговый преследователь");
            if (enemySpawner.CommittedVolleyExperiment) activeExperiments.Add("Залповый стрелок");
            if (enemySpawner.RecoilingShootersExperiment) activeExperiments.Add("Стрелок с отходом");
            if (enemySpawner.InterceptingBombersExperiment) activeExperiments.Add("Подрывник-перехватчик");
            if (enemySpawner.StrafingShootersExperiment) activeExperiments.Add("Стрелок на траверсе");
            if (enemySpawner.CommittedRamExperiment) activeExperiments.Add("Таранный преследователь");
            if (enemySpawner.WoundedRetreatExperiment) activeExperiments.Add("Отступающий преследователь");
            if (enemySpawner.BlockLockedShieldsExperiment) activeExperiments.Add("Заклинивающий щит");
        }
        MvpRow("Активные эксперименты врагов", !available ? "Спавнер не готов" :
            activeExperiments.Count == 0 ? "Все выключены" : string.Join(" · ", activeExperiments));
        MvpRow("Эксперимент: фланговый преследователь",
            (available && enemySpawner.FlankingChasersExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · включение необратимо помечает попытку Development",
            "ПЕРЕКЛЮЧИТЬ", available && MvpCanEdit, () =>
            {
                if (!MvpCanEdit || enemySpawner == null) return;
                enemySpawner.DebugSetFlankingChasersExperiment(!enemySpawner.FlankingChasersExperiment);
                RefreshCurrentTab();
            });
        MvpRow("Правило обхода", "Каждый 4-й обычный преследователь идёт по диагонали с наклоном корпуса по движению; вблизи выпрямляется и идёт прямо. Особые враги и assault-волны исключены.");
        MvpRow("После выключения эксперимента", "Новых назначений нет. Живые фланговые враги сразу возвращаются к обычной позе и преследованию; следующий сектор — без эксперимента.");
        var profile = RunStateManager.Instance?.CurrentSector?.SpawnProfile;
        MvpRow("Эксперимент: залповый стрелок",
            (available && enemySpawner.CommittedVolleyExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждый третий обычный стрелок",
            "ПЕРЕКЛЮЧИТЬ", available && MvpCanEdit, () =>
            {
                if (!MvpCanEdit || enemySpawner == null) return;
                enemySpawner.DebugSetCommittedVolleyExperiment(!enemySpawner.CommittedVolleyExperiment);
                RefreshCurrentTab();
            });
        MvpRow("Фиксированная линия", "0,6 с неподвижной подготовки → 3 выстрела через 0,15 с → 1,8 с восстановления. Уходите поперёк линии или сближайтесь после залпа. OFF отменяет залпы уже созданных стрелков.");
        var volleyPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsCommittedVolleyEligible);
        MvpRow("Немедленная проба залпа",
            volleyPrefab == null ? "В профиле сектора нет подходящего обычного стрелка с отдельной моделью." :
            "Безопасный ручной spawn; F1 закроется. Включите AI для пробы.",
            "+1 ЗАЛПОВЫЙ", available && MvpCanEdit && enemySpawner.CommittedVolleyExperiment && volleyPrefab != null, () =>
            {
                if (!MvpCanEdit || enemySpawner == null || volleyPrefab == null) return;
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (player == null) return;
                var enemy = enemySpawner.DebugSpawnCommittedVolleyAround(volleyPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        bool recoilingSpawnerReady = available && enemySpawner.isActiveAndEnabled;
        var recoilingPlayer = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
        bool recoilingPlayerReady = recoilingPlayer != null && recoilingPlayer.gameObject.activeInHierarchy &&
            PlayerRuntimeReference.PlayerHealth != null && !PlayerRuntimeReference.PlayerHealth.IsDead;
        var recoilingPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsRecoilingShooterPrefabEligible);
        MvpRow("Эксперимент: стрелок с отходом",
            (available && enemySpawner.RecoilingShootersExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждый четвёртый подходящий обычный стрелок",
            "ПЕРЕКЛЮЧИТЬ", recoilingSpawnerReady && MvpCanEdit && recoilingPlayerReady &&
            (enemySpawner.RecoilingShootersExperiment || recoilingPrefab != null), () =>
            {
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.isActiveAndEnabled ||
                    player == null || !player.gameObject.activeInHierarchy || PlayerRuntimeReference.PlayerHealth == null ||
                    PlayerRuntimeReference.PlayerHealth.IsDead ||
                    (!enemySpawner.RecoilingShootersExperiment && !EnemySpawner.IsRecoilingShooterPrefabEligible(recoilingPrefab)))
                {
                    enemyDebugStatus = "Для отхода нужны живой MVP, активный спавнер и подходящий prefab; OFF не требует prefab.";
                    RefreshCurrentTab();
                    return;
                }
                enemySpawner.DebugSetRecoilingShootersExperiment(!enemySpawner.RecoilingShootersExperiment);
                enemyDebugStatus = enemySpawner.RecoilingShootersExperiment
                    ? "Отход включён; попытка Development." : "Отход выключен; живые назначения сняты.";
                RefreshCurrentTab();
            });
        MvpRow("Отход после выстрела", "После близкого одиночного выстрела: отход 0,65 с → неподвижное окно 0,65 с. Преследуйте либо смените цель. OFF возвращает обычное движение и позу; снаряды сохраняются. Залп, траверс и отход несовместимы на одном враге. Для чистой пробы выключите эксперименты «залповый стрелок» и «стрелок на траверсе»: они назначаются раньше отхода.");
        MvpRow("Немедленная проба отхода",
            recoilingPrefab == null ? "В профиле сектора нет подходящего стрелка с отдельной моделью; замена не создаётся." :
            "Кнопка сразу создаёт одного стрелка с отходом, не расходуя счётчик каждого четвёртого обычного стрелка. Безопасное размещение; F1 закроется. Включите AI и сблизьтесь для пробы.",
            "+1 С ОТХОДОМ", recoilingSpawnerReady && MvpCanEdit && recoilingPlayerReady &&
            enemySpawner.RecoilingShootersExperiment && recoilingPrefab != null, () =>
            {
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.isActiveAndEnabled ||
                    !enemySpawner.RecoilingShootersExperiment ||
                    !EnemySpawner.IsRecoilingShooterPrefabEligible(recoilingPrefab) ||
                    player == null || !player.gameObject.activeInHierarchy || PlayerRuntimeReference.PlayerHealth == null ||
                    PlayerRuntimeReference.PlayerHealth.IsDead)
                {
                    enemyDebugStatus = "Включите отход; нужны живой игрок, активный спавнер и подходящий стрелок.";
                    RefreshCurrentTab();
                    return;
                }
                var enemy = enemySpawner.DebugSpawnRecoilingShooterAround(recoilingPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        bool interceptingSpawnerReady = available && enemySpawner.isActiveAndEnabled;
        var interceptingPlayer = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
        bool interceptingPlayerReady = interceptingPlayer != null && interceptingPlayer.gameObject.activeInHierarchy &&
            PlayerRuntimeReference.PlayerHealth != null && !PlayerRuntimeReference.PlayerHealth.IsDead;
        var interceptingPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsInterceptingBomberPrefabEligible);
        MvpRow("Эксперимент: подрывник-перехватчик",
            (available && enemySpawner.InterceptingBombersExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждый четвёртый подходящий обычный подрывник",
            "ПЕРЕКЛЮЧИТЬ", interceptingSpawnerReady && MvpCanEdit && interceptingPlayerReady && interceptingPrefab != null, () =>
            {
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.isActiveAndEnabled ||
                    player == null || !player.gameObject.activeInHierarchy || PlayerRuntimeReference.PlayerHealth == null ||
                    PlayerRuntimeReference.PlayerHealth.IsDead || !EnemySpawner.IsInterceptingBomberPrefabEligible(interceptingPrefab))
                {
                    enemyDebugStatus = "Для перехвата нужны живой MVP, активный спавнер и подходящий prefab.";
                    RefreshCurrentTab();
                    return;
                }
                enemySpawner.DebugSetInterceptingBombersExperiment(!enemySpawner.InterceptingBombersExperiment);
                enemyDebugStatus = enemySpawner.InterceptingBombersExperiment
                    ? "Перехват включён; попытка Development." : "Перехват выключен; живые назначения сняты.";
                RefreshCurrentTab();
            });
        MvpRow("Упреждение движения", "Вдали подрывник идёт наперерез движению; меняйте направление. Вблизи возвращается к обычному преследованию. Штатный взрыв неизменен. OFF снимает перехват у живых врагов, не отменяя взрывы.");
        MvpRow("Немедленная проба перехвата",
            interceptingPrefab == null ? "В профиле сектора нет подходящего подрывника; замена не создаётся." :
            "Безопасный ручной spawn без расхода обычной очереди назначений; F1 закроется. Включите AI для пробы.",
            "+1 ПЕРЕХВАТЧИК", interceptingSpawnerReady && MvpCanEdit && interceptingPlayerReady &&
            enemySpawner.InterceptingBombersExperiment && interceptingPrefab != null, () =>
            {
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.isActiveAndEnabled ||
                    !enemySpawner.InterceptingBombersExperiment ||
                    !EnemySpawner.IsInterceptingBomberPrefabEligible(interceptingPrefab) ||
                    player == null || !player.gameObject.activeInHierarchy || PlayerRuntimeReference.PlayerHealth == null ||
                    PlayerRuntimeReference.PlayerHealth.IsDead)
                {
                    enemyDebugStatus = "Включите перехват; нужны живой игрок, активный спавнер и подходящий подрывник.";
                    RefreshCurrentTab();
                    return;
                }
                var enemy = enemySpawner.DebugSpawnInterceptingBomberAround(interceptingPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        MvpRow("Эксперимент: стрелок на траверсе",
            (available && enemySpawner.StrafingShootersExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждое четвёртое подходящее обычное назначение",
            "ПЕРЕКЛЮЧИТЬ", available && MvpCanEdit, () =>
            {
                if (!MvpCanEdit || enemySpawner == null) return;
                enemySpawner.DebugSetStrafingShootersExperiment(!enemySpawner.StrafingShootersExperiment);
                RefreshCurrentTab();
            });
        MvpRow("Боковая одиночная стрельба", "В полосе стрельбы враг движется поперёк линии к игроку, сохраняя обычную одиночную стрельбу; вне полосы сближается или отступает. Меняйте фронт огня и маршрут. Залповый режим несовместим. OFF отменяет живые назначения.");
        var strafingPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsStrafingShooterEligible);
        MvpRow("Немедленная проба траверса",
            strafingPrefab == null ? "В профиле сектора нет подходящего обычного стрелка; замена не создаётся." :
            "Безопасный ручной spawn; F1 закроется. Включите AI для пробы.",
            "+1 НА ТРАВЕРСЕ", available && MvpCanEdit && enemySpawner.StrafingShootersExperiment && strafingPrefab != null, () =>
            {
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.StrafingShootersExperiment ||
                    strafingPrefab == null || !EnemySpawner.IsStrafingShooterEligible(strafingPrefab))
                {
                    enemyDebugStatus = "Включите эксперимент траверса; нужен подходящий стрелок и доступное редактирование.";
                    RefreshCurrentTab();
                    return;
                }
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (player == null)
                {
                    enemyDebugStatus = "Игрок не готов для пробы траверса.";
                    RefreshCurrentTab();
                    return;
                }
                var enemy = enemySpawner.DebugSpawnStrafingShooterAround(strafingPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        bool retreatAvailable = available && enemySpawner.isActiveAndEnabled;
        MvpRow("Эксперимент: отступающий преследователь",
            (retreatAvailable && enemySpawner.WoundedRetreatExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждый четвёртый подходящий преследователь",
            "ПЕРЕКЛЮЧИТЬ", retreatAvailable && (enemySpawner.WoundedRetreatExperiment || MvpCanEdit), () =>
            {
                if (enemySpawner == null || (!enemySpawner.isActiveAndEnabled && !enemySpawner.WoundedRetreatExperiment) || (!enemySpawner.WoundedRetreatExperiment && !MvpCanEdit)) return;
                enemySpawner.DebugSetWoundedRetreatExperiment(!enemySpawner.WoundedRetreatExperiment);
                RefreshCurrentTab();
            });
        MvpRow("Одноразовый отход при ранении", "При HP ≤50% один раз отходит 0,9 с по мировой линии, затем преследует. Добейте раненого или используйте брешь. Попадания/лечение не перезаряжают отход; OFF возвращает обычную позу и погоню.");
        var retreatPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsWoundedRetreatEligible);
        MvpRow("Немедленная проба отхода",
            retreatPrefab == null ? "Нет подходящего обычного преследователя с безопасной отдельной моделью." :
            "Безопасный ручной spawn; F1 закроется. Включите AI для пробы.",
            "+1 ОТСТУПАЮЩИЙ", retreatAvailable && MvpCanEdit && enemySpawner.WoundedRetreatExperiment && retreatPrefab != null, () =>
            {
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.isActiveAndEnabled || retreatPrefab == null) return;
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (player == null || PlayerRuntimeReference.ResolvePlayerHealth() is not { IsDead: false }) return;
                var enemy = enemySpawner.DebugSpawnWoundedRetreatAround(retreatPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        MvpRow("Эксперимент: таранный преследователь",
            (available && enemySpawner.CommittedRamExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждый четвёртый подходящий преследователь",
            "ПЕРЕКЛЮЧИТЬ", available && MvpCanEdit, () =>
            {
                if (!MvpCanEdit || enemySpawner == null) return;
                enemySpawner.DebugSetCommittedRamExperiment(!enemySpawner.CommittedRamExperiment);
                RefreshCurrentTab();
            });
        MvpRow("Таран по фиксированной линии", "До 5 м: 0,65 с подготовки → 0,45 с рывка → 1,2 с восстановления. Уходите поперёк линии, используйте промах. OFF отменяет цикл уже созданных врагов.");
        var ramPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsCommittedRamEligible);
        MvpRow("Немедленная проба тарана",
            ramPrefab == null ? "Нет подходящего обычного преследователя с безопасной отдельной моделью." :
            "Безопасный ручной spawn; F1 закроется. Включите AI для пробы.",
            "+1 ТАРАННЫЙ", available && MvpCanEdit && enemySpawner.CommittedRamExperiment && ramPrefab != null, () =>
            {
                if (!MvpCanEdit || enemySpawner == null || ramPrefab == null) return;
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (player == null) return;
                var enemy = enemySpawner.DebugSpawnCommittedRamAround(ramPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        MvpRow("Эксперимент: заклинивающий щит",
            (available && enemySpawner.BlockLockedShieldsExperiment ? "Включён" : "Выключен") +
            " · текущий сектор · Development · каждый четвёртый подходящий обычный spawn",
            "ПЕРЕКЛЮЧИТЬ", available && MvpCanEdit, () =>
            {
                if (!MvpCanEdit || enemySpawner == null) return;
                enemySpawner.DebugSetBlockLockedShieldsExperiment(!enemySpawner.BlockLockedShieldsExperiment);
                RefreshCurrentTab();
            });
        MvpRow("Окно обхода щита", "Реальный блок фиксирует пластину на 0,9 с; повторные блоки не продлевают окно. Затем возвращается обычное слежение. Спровоцируйте блок и обойдите пластину. OFF отменяет режим живых назначений.");
        var blockLockedShieldPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsBlockLockedShieldPrefabEligible);
        MvpRow("Немедленная проба щита",
            blockLockedShieldPrefab == null ? "В профиле сектора нет подходящего обычного врага с действующим щитом; замена не создаётся." :
            "Безопасный ручной spawn; F1 закроется. Включите AI для пробы.",
            "+1 ЗАКЛИНИВАЮЩИЙ", available && MvpCanEdit && enemySpawner.BlockLockedShieldsExperiment && blockLockedShieldPrefab != null, () =>
            {
                if (!MvpCanEdit || enemySpawner == null || !enemySpawner.BlockLockedShieldsExperiment || blockLockedShieldPrefab == null) return;
                var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                if (player == null)
                {
                    enemyDebugStatus = "Игрок не готов для пробы заклинивающего щита.";
                    RefreshCurrentTab();
                    return;
                }
                var enemy = enemySpawner.DebugSpawnBlockLockedShieldAround(blockLockedShieldPrefab, player.position, out enemyDebugStatus);
                if (enemy == null) { RefreshCurrentTab(); return; }
                debugEnemies.Add(enemy);
                CloseMenu();
            });
        var flankingPrefab = profile?.Phases?.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
            .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
            .FirstOrDefault(EnemySpawner.IsFlankingChaserEligible);
        MvpRow("Немедленная проба обхода",
            flankingPrefab == null ? "В профиле сектора нет подходящего prefab; замена не создаётся." :
            "Включите эксперимент; безопасный ручной spawn, затем F1 закроется. AI должен работать.",
            "+1 ФЛАНГОВЫЙ", available && MvpCanEdit && enemySpawner.FlankingChasersExperiment, () =>
            {
                if (!MvpCanEdit) return;
                if (flankingPrefab == null)
                {
                    enemyDebugStatus = "В профиле сектора нет подходящего prefab флангового преследователя.";
                    RefreshCurrentTab();
                    return;
                }
                SpawnDebugEnemies(flankingPrefab, "Фланговый преследователь", 1, true);
            });
        if (profile != null)
            foreach (var prefab in profile.Phases.Where(p => p != null).SelectMany(p => p.enemies).Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab).Distinct())
            {
                string name = prefab.GetComponent<EnemyDirectionalShield>() != null ? prefab.GetComponent<EnemyShooterMovement>() != null ? "Стрелок со щитом" : "Ближний щитоносец" :
                    prefab.GetComponent<EnemyShooterMovement>() != null ? "Стрелок" : prefab.GetComponent<EnemyBomberMovement>() != null ? "Подрывник" : "Обычный враг";
                var profilePlayer = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                bool profileSpawnReady = available && MvpCanEdit && enemySpawner.isActiveAndEnabled &&
                    RunStateManager.Instance != null && RunStateManager.Instance.CurrentSector?.SpawnProfile == profile &&
                    profilePlayer != null && profilePlayer.gameObject.activeInHierarchy &&
                    PlayerRuntimeReference.PlayerHealth != null && !PlayerRuntimeReference.PlayerHealth.IsDead;
                MvpRow(name, prefab.name + " · одна безопасная ручная установка через спавнер", "+1 ВРАГ", profileSpawnReady,
                    () =>
                    {
                        var runState = RunStateManager.Instance;
                        var player = PlayerRuntimeReference.ResolvePlayerTransform("Player", true);
                        var currentProfile = runState?.CurrentSector?.SpawnProfile;
                        bool currentPrefab = prefab != null && currentProfile?.Phases != null &&
                            currentProfile.Phases.Where(p => p != null && p.enemies != null)
                                .SelectMany(p => p.enemies).Any(e => e != null && e.enemyPrefab == prefab);
                        if (!MvpCanEdit || enemySpawner == null || !enemySpawner.isActiveAndEnabled ||
                            runState == null || !currentPrefab || player == null || !player.gameObject.activeInHierarchy ||
                            PlayerRuntimeReference.PlayerHealth == null || PlayerRuntimeReference.PlayerHealth.IsDead)
                        {
                            enemyDebugStatus = "Нужны доступное редактирование, живой MVP и враг текущего профиля.";
                            RefreshCurrentTab();
                            return;
                        }
                        runState.DebugMarkDevelopmentRun();
                        SpawnDebugEnemies(prefab, name, 1);
                    });
            }
        var events = FindFirstObjectByType<WorldEventSpawner>();
        AddSectionTitle("Эксперимент: зачистка огневой группы", "Текущий сектор · default OFF · публично выключен · включение помечает попытку Development");
        MvpRow("Зачистка огневой группы", events != null && events.FireteamClearanceExperimentEnabled ? "Включено" : "Выключено",
            "ПЕРЕКЛЮЧИТЬ", MvpCanEdit && events != null && events.isActiveAndEnabled, () =>
            {
                if (!MvpCanEdit || events == null || !events.isActiveAndEnabled) return;
                events.DebugSetFireteamClearanceExperiment(!events.FireteamClearanceExperimentEnabled, out enemyDebugStatus);
                RefreshCurrentTab();
            });
        MvpRow("Создать огневую группу", "Один дополнительный Development-экземпляр сверх обычной вместимости. Перед стартом закончите другое активное событие. Начните обычной интеракцией: три отмеченных стрелка за 35 с; за успех — стандартная награда события.",
            "СОЗДАТЬ ЗАЧИСТКУ", MvpCanEdit && events != null && events.isActiveAndEnabled &&
                events.FireteamClearanceExperimentEnabled && events.FireteamClearanceExperimentInstance == null, () =>
            {
                if (!MvpCanEdit || events == null || !events.isActiveAndEnabled) return;
                if (!events.FireteamClearanceExperimentEnabled || events.FireteamClearanceExperimentInstance != null)
                {
                    enemyDebugStatus = !events.FireteamClearanceExperimentEnabled ? "Сначала включите эксперимент зачистки." : "Зачистка уже создана.";
                    RefreshCurrentTab();
                    return;
                }
                if (events.DebugSpawnFireteamClearance(out enemyDebugStatus)) CloseMenu();
                else RefreshCurrentTab();
            });
        MvpRow("Убрать только зачистку", "OFF и удаление отменяют собственное событие и его группу без награды.",
            "УБРАТЬ ЗАЧИСТКУ", MvpCanEdit && events != null && events.isActiveAndEnabled &&
                events.FireteamClearanceExperimentInstance != null, () =>
            {
                if (!MvpCanEdit || events == null || !events.isActiveAndEnabled) return;
                events.DebugClearFireteamClearance(out enemyDebugStatus);
                RefreshCurrentTab();
            });
        AddSectionTitle("Эксперимент: эвакуация груза", "Текущий сектор · публично выключен · включение помечает попытку Development");
        MvpRow("Сопровождение груза", events != null && events.EscortCargoExperimentEnabled ? "Включено" : "Выключено",
            "ПЕРЕКЛЮЧИТЬ", events != null && MvpCanEdit, () =>
            {
                if (!MvpCanEdit || events == null) return;
                events.DebugSetEscortCargoExperiment(!events.EscortCargoExperimentEnabled, out enemyDebugStatus);
                RefreshCurrentTab();
            });
        MvpRow("Создать груз", "Штатное размещение и лимит событий. После создания F1 закроется; подойдите и примите событие обычной интеракцией.",
            "СОЗДАТЬ ГРУЗ", events != null && MvpCanEdit && events.EscortCargoExperimentEnabled, () =>
            {
                if (!MvpCanEdit || events == null) return;
                if (events.DebugSpawnEscortCargo(out enemyDebugStatus)) CloseMenu();
                else RefreshCurrentTab();
            });
        MvpRow("Убрать только груз", "Без результата и награды. Выключение переключателя само по себе груз не удаляет.",
            "УБРАТЬ ГРУЗ", events != null && MvpCanEdit && events.EscortCargoExperimentInstance != null, () =>
            {
                if (!MvpCanEdit || events == null) return;
                events.DebugClearEscortCargo(out enemyDebugStatus);
                RefreshCurrentTab();
            });
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
        BuildMvpPickupTools();
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
