#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class Subject42DebugMenu
{
    private bool IsMvpWorldPage => IsSandboxScene && activeTab == DebugTab.WorldEvents;
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
            hubSearchButton?.SetActive(false);
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
            var back = visualBackButton.GetComponent<Button>();
            back.onClick.RemoveAllListeners();
            back.onClick.AddListener(ReturnFromMvpOrbitSurface);
            back.GetComponentInChildren<TextMeshProUGUI>().text = "МЕНЮ";
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchoredPosition = new Vector2(-48f, 0f);
            backRect.sizeDelta = new Vector2(54f, 26f);
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

    private void BuildMvpWorldPage()
    {
        ResolveSceneReferences();
        AddSectionHeading("Мир и прогресс", "F1 / Esc — закрыть меню");
        if (!string.IsNullOrEmpty(mvpStatus)) AddHint(mvpStatus);
        BuildMvpPlayer();
    }

    private void MvpRow(string title, string status, string actionLabel = null, bool enabled = false, UnityEngine.Events.UnityAction action = null)
    {
        RectTransform row = CreateRect(title, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        bool compact = mvpOrbitSurfaceOpen;
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
        OrbitalRewardKind.Pistol or OrbitalRewardKind.Sniper or OrbitalRewardKind.Shotgun or
        OrbitalRewardKind.NewRing or OrbitalRewardKind.AddMount or OrbitalRewardKind.RigExpansion ||
        OrbitalRewardProvider.GetRigPart(kind).HasValue || OrbitalRewardProvider.GetSegmentedLinkKind(kind).HasValue;

    private void BuildRigRewardCatalogue()
    {
        var build = MvpStation.Rig.Build;
        foreach (bool weapons in new[] { true, false })
        {
            AddSectionTitle(weapons ? "Оружие" : "Модули", "В запас конструктора");
            foreach (var card in upgradeManager.DebugRewardCatalogue)
            {
                var kind = card.RewardKind;
                if (OrbitalRewardProvider.GetRigPart(kind) is not TelekineticPart part ||
                    (part is TelekineticPart.Pistol or TelekineticPart.Sniper or TelekineticPart.Shotgun or TelekineticPart.Saw) != weapons)
                    continue;

                int stock = build.Reserve.Count(item => item.Kind == part);
                SandboxRewardRow(kind, TelekineticBuild.Label(part), stock, OrbitalRewardIconResolver.Resolve(card), MvpCanEdit);
            }
        }
        AddSectionTitle("Пространство конструкции", "");
        AddMvpBuildReward(OrbitalRewardKind.RigExpansion, $"Сетка: {build.AvailableRadius * 2 + 1}×{build.AvailableRadius * 2 + 1}", "РАСШИРИТЬ");
    }

    private void BuildMvpWeaponExperiments()
    {
        var station = MvpStation;
        if (station == null || !station.IsInitialized)
        { AddHint("Нужен живой игрок."); return; }
        var state = station.State;
        string reason = station.Rig != null || !station.UsesSegmentedOrbit ? "Профили работают в Segmented Orbit." :
            !MvpCanEdit || station.IsRunEnded ? "Нужен живой игрок и свободная очередь наград." : null;
        if (reason != null) AddHint(reason);
        Group("Пистолет", OrbitalModuleKind.Pistol, "Обычный", "Прямой одиночный снаряд.",
            ("Возврат промаха", () => state.MissReturnPistolExperiment, station.DebugSetMissReturnPistolExperiment,
                "При промахе: 4 м наружу и 4 м обратно по фиксированной мировой линии."));
        Group("Снайперка", OrbitalModuleKind.Sniper, "Обычная", "Обычный одиночный выстрел в одну цель.",
            ("Заряженная линия", () => state.ChargedLineSniperExperiment, station.DebugSetChargedLineSniperExperiment,
                "Подготовка 0,45 с, затем пробитие до трёх тел. Q/E ведут линию до выпуска."));
        Group("Пила", OrbitalModuleKind.Saw, "Обычная", "Штатная пила.",
            ("Инерционная", () => state.InertialSawExperiment, station.DebugSetInertialSawExperiment,
                "Пила набирает и теряет инерцию при смене направления."),
            ("Импульсная", () => state.PulsedSawExperiment, station.DebugSetPulsedSawExperiment,
                "Пила работает импульсами."));
        if (!string.IsNullOrEmpty(mvpStatus)) AddHint(mvpStatus);
        void Group(string title, OrbitalModuleKind kind, string normal, string normalDescription,
            params (string Label, Func<bool> Active, Func<bool, bool> Set, string Description)[] modes)
        {
            int selected = Array.FindIndex(modes, mode => mode.Active()) + 1;
            int installed = state.Modules.Count(module => module.ModuleType == kind);
            var actions = new (string Label, bool Enabled, UnityEngine.Events.UnityAction Action)[modes.Length + 1];
            for (int i = 0; i < actions.Length; i++)
            {
                int index = i;
                string label = i == 0 ? normal : modes[i - 1].Label;
                actions[i] = (label + (i == selected ? " · выбран" : ""), reason == null && i != selected, () =>
                {
                    if (!MvpCanEdit || station.IsRunEnded || station.Rig != null) { RefreshCurrentTab(); return; }
                    for (int j = 0; j < modes.Length; j++)
                    {
                        if (j + 1 == index || !modes[j].Active() || modes[j].Set(false)) continue;
                        mvpStatus = "Профиль не сменился: действие сейчас недоступно.";
                        RefreshCurrentTab();
                        return;
                    }
                    mvpStatus = index > 0 && !modes[index - 1].Set(true)
                        ? "Выбранный профиль сейчас недоступен."
                        : "Профиль обновлён.";
                    RefreshCurrentTab();
                });
            }
            SandboxRow("Вариант: " + title, actions);
            var buttons = contentRoot.GetChild(contentRoot.childCount - 1).GetComponentsInChildren<Button>();
            var selectedButton = buttons[selected];
            var colors = selectedButton.colors;
            colors.disabledColor = new Color(.12f, .57f, .35f, 1f);
            selectedButton.colors = colors;
            string description = selected == 0 ? normalDescription : modes[selected - 1].Description;
            string application = installed == 0 ? "Не установлен: профиль применится после установки. " : $"Установлено: {installed}. ";
            var hint = CreateText("Описание варианта", contentRoot, application + description, 13f,
                TextAlignmentOptions.MidlineLeft, mutedColor);
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        }
    }

    private void AddMvpBuildReward(OrbitalRewardKind kind, string status, string actionLabel = "ВЫДАТЬ")
    {
        var data = upgradeManager.DebugRewardCatalogue.FirstOrDefault(d => d.RewardKind == kind);
        if (data == null) return;
        string reason = upgradeManager.DebugRewardUnavailableReason(kind);
        MvpRow(LocalizationService.EnsureExists().Get(data.upgradeName), reason ?? status,
            actionLabel, MvpCanEdit && reason == null, () => GrantMvpBuildReward(kind));
        contentRoot.GetChild(contentRoot.childCount - 1).GetComponentInChildren<Button>().name = "Rig reward · " + kind;
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
        CloseMenu();
        CloseHub();
        ResolveSceneReferences();
        if (!MvpCanEdit || MvpStation.IsRunEnded || Time.timeScale <= 0f) return;
        hubCategory = 0;
        SelectTab(DebugTab.Sandbox, false);
        OpenMenu();
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
        mvpOrbitSaveFooter.gameObject.SetActive(false);
        ApplyMenuViewportLayout();
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
#if UNITY_EDITOR
        var destination = UnityEditor.AssetDatabase.FindAssets("t:OrbitalPresentationConfig")
            .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(asset => asset != null && config.name == asset.name + " (Runtime)");
        if (destination == null) { mvpOrbitSaveStatus.text = "Исходные настройки не найдены"; return; }
        UnityEditor.Undo.RecordObject(destination, "Save ORBITAL ring appearance");
        destination.RingSurface = JsonUtility.FromJson<OrbitalPresentationConfig.RingSurfaceSettings>(JsonUtility.ToJson(config.RingSurface));
        destination.RingLineWidth = config.RingLineWidth;
        destination.RingLineAlpha = config.RingLineAlpha;
        UnityEditor.EditorUtility.SetDirty(destination);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(destination);
#else
        PlayerPrefs.SetString("Subject42.Orbits.Appearance", JsonUtility.ToJson(new DebugRingAppearance
        { Surface = config.RingSurface, Width = config.RingLineWidth, Alpha = config.RingLineAlpha }));
        PlayerPrefs.Save();
#endif
        mvpRingSurfaceBackup = JsonUtility.ToJson(config.RingSurface);
        mvpRingWidthBackup = config.RingLineWidth;
        mvpRingAlphaBackup = config.RingLineAlpha;
        mvpOrbitSaveStatus.text = "Сохранено между запусками";
    }

#if !UNITY_EDITOR
    [Serializable] private sealed class DebugRingAppearance
    {
        public OrbitalPresentationConfig.RingSurfaceSettings Surface;
        public float Width, Alpha;
    }
#endif

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
        AddSectionHeading("Внешний вид орбит", "F9 / Esc — закрыть · Тяните за заголовок");
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

    private void BuildMvpEnemyExperiments()
    {
        bool available = enemySpawner != null;
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
                var player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
                if (player != null &&
                    enemySpawner.DebugSpawnFlankingChaserAround(flankingPrefab, player.position, out enemyDebugStatus) != null)
                    CloseMenu();
                else RefreshCurrentTab();
            });
        AddHint(enemyDebugStatus);
    }

    private void BuildMvpWorldEvents()
    {
        var sector = ProductionExplorationSectorController.ActiveInstance;
        if (sector != null)
        {
            AddSectionTitle("События текущего сектора", "Существующие игровые сервисы, без лабораторных пресетов");
            MvpRow("Контейнер награды рядом с игроком", "Настоящее содержимое и обычная выдача награды", "СОЗДАТЬ", MvpCanEdit && !WorldLootRewardReel.IsActive, () => { CloseMenu(); sector.DebugSpawnRewardChestNearPlayer(); });
            MvpRow("Парные порталы", "Безопасное размещение существующим сервисом", "СОЗДАТЬ", MvpCanEdit, () => { mvpStatus = sector.SpawnPortalPair() ? "Порталы созданы" : "Нет безопасных мест для порталов"; RefreshCurrentTab(); });
            MvpRow("Ресурсные узлы", "Создать узлы в текущем секторе", "СОЗДАТЬ", MvpCanEdit, () => { mvpStatus = $"Создано узлов: {sector.SpawnResourceNodes()}"; RefreshCurrentTab(); });
        }
    }

    private void BuildMvpPlayer()
    {
        var player = PlayerRuntimeReference.CachedPlayer; var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        var run = RunStateManager.Instance; var experience = ExperienceManager.Instance;
        AddSectionTitle("Игрок", "");
        MvpRow("Здоровье игрока", health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "Игрок не готов", "ВОССТАНОВИТЬ", health != null && !health.IsDead, () => { health.Heal(health.MaxHealth); RefreshCurrentTab(); });
        MvpRow("Опыт до следующего уровня", experience != null ? $"Уровень {experience.CurrentLevel} · {experience.CurrentExp}/{experience.ExpToNextLevel}" : "Система опыта не готова", "ДАТЬ ОПЫТ", MvpCanEdit && experience != null,
            () => { CloseMenu(); run.DebugMarkDevelopmentRun(); experience.AddExperience(Mathf.Max(1, experience.ExpToNextLevel - experience.CurrentExp)); });
        BuildMvpPickupTools();
        AddSectionTitle("Текущий забег", "");
        MvpRow("Текущий ран", $"Сектор {run?.CurrentSector?.SectorNumber} · орбит {MvpStation?.Rings.Count} · оружий {MvpStation?.Modules.Count} · угроза {run?.ThreatValue:0.0}");
        bool canExit = MvpCanEdit && runFlowController != null && runFlowController.CanDebugCompleteCurrentLevel;
        MvpRow("Завершить текущий сектор", canExit ? "Досрочно довести таймер до конца и продолжить забег через штатный переход" : "Завершение сейчас недоступно",
            "ПРОЙТИ СЕКТОР", canExit, () => { CloseMenu(); runFlowController?.TryDebugCompleteCurrentLevel(); });
        MvpRow("Вернуться в бункер", "Штатное завершение рана и очистка временного состояния", "ВЕРНУТЬСЯ", run != null && !run.IsRunEnded && RunEndService.Instance != null && !SceneTransitionOverlay.IsTransitioning,
            () => { CloseMenu(); RunEndService.Instance.ReturnToBunker(); });
        BuildMvpWorldEvents();
        AddWorldEventsSection();
    }
}
#endif
