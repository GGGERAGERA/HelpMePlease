#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Same dark/cyan IMGUI vocabulary as the orbital developer tools; no production UI assets.
public sealed class BunkerDebugView : IDisposable
{
    private readonly BunkerDebugController owner;
    private readonly Dictionary<string, string> fields = new();
    private readonly Vector2[] scroll = new Vector2[5];
    private readonly string[] tabs = { "Экономика", "Доступ", "Прогресс", "Реплики", "Сохранение" };
    private Rect window = new(876, 38, 700, 824);
    private int tab;
    private GUIStyle panel, label, heading, small, button, selected, input;
    private Texture2D background, buttonBackground;
    private string confirmation;
    private Action confirmedAction;
    private static readonly Color Cyan = new(.2f, .9f, 1f), Amber = new(1f, .72f, .25f);

    public BunkerDebugView(BunkerDebugController owner) => this.owner = owner;
    public void SelectTab(int value) { tab = Mathf.Clamp(value, 0, 4); GUIUtility.keyboardControl = 0; }
    public void Dispose()
    {
        if (background != null) UnityEngine.Object.Destroy(background);
        if (buttonBackground != null) UnityEngine.Object.Destroy(buttonBackground);
    }
    private static Texture2D Texture(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color); texture.Apply(); return texture;
    }
    private void Styles()
    {
        if (panel != null) return;
        background = Texture(new Color(.025f, .045f, .065f, .98f));
        buttonBackground = Texture(new Color(.075f, .15f, .19f));
        panel = new GUIStyle(GUI.skin.window) { padding = new RectOffset(18, 18, 12, 14) };
        panel.normal.background = background; panel.onNormal.background = background;
        label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, padding = new RectOffset(0, 0, 3, 3) };
        label.normal.textColor = new Color(.89f, .94f, .98f);
        heading = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold };
        heading.normal.textColor = Cyan;
        small = new GUIStyle(label) { fontSize = 13 };
        small.normal.textColor = new Color(.64f, .78f, .83f);
        button = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true, fixedHeight = 30,
            margin = new RectOffset(2, 2, 3, 3), padding = new RectOffset(8, 8, 3, 3) };
        button.normal.background = buttonBackground; button.normal.textColor = Color.white;
        selected = new GUIStyle(button); selected.normal.textColor = Cyan;
        input = new GUIStyle(GUI.skin.textField) { fontSize = 15, fixedHeight = 28, alignment = TextAnchor.MiddleRight };
    }
    public void Draw()
    {
        Styles();
        Matrix4x4 previous = GUI.matrix;
        Color color = GUI.color;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUI.depth = -1000;
        window = GUI.Window(owner.GetInstanceID(), window, DrawWindow, "", panel);
        GUI.matrix = previous; GUI.color = color;
    }
    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("БУНКЕР / DEBUG", heading);
        if (Button("F9 · закрыть", () => owner.SetVisible(false), width: 120)) { }
        GUILayout.EndHorizontal();
        Color previous = GUI.contentColor;
        GUI.contentColor = PlayerPrefs.IsSandboxActive ? Cyan : Amber;
        GUILayout.Label(PlayerPrefs.IsSandboxActive ? "ТЕСТОВОЕ СОСТОЯНИЕ · основной прогресс изолирован" :
            "ОБЫЧНОЕ СОХРАНЕНИЕ · только просмотр", label);
        GUI.contentColor = previous;
        GUILayout.Label("Developer only · время идёт · ввод игрока заблокирован в окне", small);
        if (!PlayerPrefs.IsSandboxActive && !owner.Busy)
            Button("Начать тестовую копию текущего сохранения", () => owner.BeginTestCopy());
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < tabs.Length; i++)
        {
            int next = i;
            if (GUILayout.Button(tabs[i], tab == i ? selected : button)) SelectTab(next);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        if (confirmation != null)
        {
            GUILayout.Space(35);
            GUILayout.Label("ПОДТВЕРЖДЕНИЕ", heading);
            GUILayout.Label(confirmation, label);
            GUILayout.BeginHorizontal();
            Button("Подтвердить", () => { var action = confirmedAction; confirmation = null; confirmedAction = null; fields.Clear(); action(); });
            Button("Отмена", () => { confirmation = null; confirmedAction = null; });
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }
        else
        {
            scroll[tab] = GUILayout.BeginScrollView(scroll[tab], GUILayout.ExpandHeight(true));
            switch (tab)
            {
                case 0: Economy(); break;
                case 1: Access(); break;
                case 2: Progress(); break;
                case 3: Barks(); break;
                case 4: Saves(); break;
            }
            GUILayout.EndScrollView();
        }
        GUILayout.Space(5);
        GUILayout.Label(owner.Busy ? "Загрузка состояния…" : owner.Notice, small, GUILayout.Height(44));
        GUI.DragWindow(new Rect(0, 0, window.width - 150, 40));
    }
    private bool Button(string text, Action action, bool allowed = true, float width = 0)
    {
        bool enabled = GUI.enabled; GUI.enabled = enabled && allowed && !owner.Busy;
        bool clicked = width > 0 ? GUILayout.Button(text, button, GUILayout.Width(width)) : GUILayout.Button(text, button);
        GUI.enabled = enabled;
        if (clicked) action();
        return clicked;
    }
    private void Confirm(string text, Action action) { confirmation = text; confirmedAction = action; GUIUtility.keyboardControl = 0; }
    private void Section(string text) { GUILayout.Space(12); GUILayout.Label(text, heading); }
    private void Number(string key, string title, int current, Action<int> apply, int step = 1)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(title, label, GUILayout.Width(232));
        GUILayout.Label(current.ToString(), label, GUILayout.Width(60));
        if (!fields.TryGetValue(key, out string value)) value = current.ToString();
        bool enabled = GUI.enabled; GUI.enabled &= owner.CanMutate;
        fields[key] = GUILayout.TextField(value, 11, input, GUILayout.Width(83));
        Button("Уст.", () =>
        {
            if (int.TryParse(fields[key], out int parsed)) apply(parsed);
            else owner.SetNotice("Введите целое число в диапазоне Int32.");
        }, width: 55);
        Button("−", () => apply((int)Math.Max(0L, (long)current - step)), width: 40);
        Button("+", () => apply((int)Math.Min(int.MaxValue, (long)current + step)), width: 40);
        GUI.enabled = enabled;
        GUILayout.EndHorizontal();
    }
    private void Economy()
    {
        var wallet = CurrencyManager.Instance;
        if (wallet == null) { GUILayout.Label("Ожидание CurrencyManager…", label); return; }
        Section("Ресурсы");
        Number("gold", "Золото · шаг 100", wallet.TotalGold, v => owner.SetGold(v), 100);
        Number("cores", "Ядра · шаг 1", wallet.TotalCores, v => owner.SetCores(v));
        GUILayout.Label("Точное значение без множителя дохода. Штатные уведомления обновляют HUD.", small);
        var meta = MetaProgressionManager.Instance;
        if (meta == null) return;
        Section("Метаулучшения · 0–10");
        foreach (MetaUpgradeType type in Enum.GetValues(typeof(MetaUpgradeType)))
            Number("meta" + type, type.ToString(), meta.GetLevel(type), v => owner.SetMetaLevel(type, v));
        Section("Станции · уровни и вложения");
        var stations = BunkerStationProgressionService.Instance;
        if (stations != null) foreach (BunkerStationId station in Enum.GetValues(typeof(BunkerStationId)))
        {
            Number("station" + station, station + " · уровень", stations.GetLevel(station), v => owner.SetStationLevel(station, v));
            Number("investment" + station, "  вложено / " + stations.GetUpgradeCost(station), stations.GetInvestedGold(station), v => owner.SetStationInvestment(station, v), 100);
        }
        if (owner.CanMutate && owner.Catalog?.modules != null)
        {
            Section("Модули оружия · метауровень");
            foreach (var module in owner.Catalog.modules)
                if (module != null) Number("module" + module.Kind, module.Kind.ToString(), meta.GetOrbitalModuleLevel(module.Kind), v => owner.SetModuleLevel(module.Kind, v));
        }
        if (owner.CanMutate && owner.Catalog?.anomalies != null)
        {
            Section("Стабилизаторы · метауровень");
            foreach (var anomaly in owner.Catalog.anomalies)
                if (anomaly != null) Number("anomaly" + anomaly.Id, anomaly.name, AnomalyMetaProgressionManager.EnsureExists().GetLevel(anomaly), v => owner.SetAnomalyLevel(anomaly, v));
        }
        if (!owner.CanMutate) GUILayout.Label("Уровни модулей и стабилизаторов доступны в тестовой копии.", small);
        Section("Разблокированные возможности");
        var unlocks = UnlockProgressService.Instance;
        if (unlocks?.Registry != null) foreach (var content in unlocks.Registry.Contents)
        {
            if (content == null) continue;
            GUILayout.Label(content.displayName + " · " + (unlocks.IsUnlocked(content) ? "доступно" : "закрыто"), label);
            GUILayout.BeginHorizontal();
            bool manual = !content.unlockedByDefault && content.condition?.type != UnlockConditionType.StationLevelRequirement;
            Button("Открыть", () => owner.SetUnlock(content, true), owner.CanMutate && manual);
            Button("Закрыть", () => Confirm("Сбросить тестовый unlock «" + content.displayName + "»?", () => owner.SetUnlock(content, false)), owner.CanMutate && manual);
            GUILayout.EndHorizontal();
            if (!manual) GUILayout.Label("Определяется default-доступом или уровнем станции.", small);
        }
    }
    private static string ShortName(Component target) => target.transform.parent == null ? target.name : target.transform.parent.name + "/" + target.name;
    private void Access()
    {
        Section("Ворота · " + owner.Gates.Length);
        GUILayout.Label("Открыть/закрыть и lock — штатные override. AUTO возвращает последнее игровое состояние.", small);
        foreach (var gate in owner.Gates)
        {
            if (gate == null) continue;
            GUILayout.Label(ShortName(gate) + " · " + (gate.IsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ") +
                (gate.IsUnlocked ? " / доступны" : " / LOCK") + (gate.DebugHasOverride ? " *" : ""), label);
            GUILayout.BeginHorizontal();
            Button("Открыть", () => owner.SetGateOpen(gate, true), owner.CanMutate);
            Button("Закрыть", () => owner.SetGateOpen(gate, false), owner.CanMutate);
            Button(gate.DebugIsLocked ? "Unlock" : "Lock", () => owner.SetGateLocked(gate, !gate.DebugIsLocked), owner.CanMutate);
            Button("AUTO", () => owner.ResetGate(gate), owner.CanMutate);
            GUILayout.EndHorizontal();
        }
        Section("Помещения");
        if (owner.Rooms.Length == 0)
            GUILayout.Label("Доступ помещений определяется соответствующими воротами RoomDoors выше. Отдельные контроллеры комнат в этой production-сцене не подключены.", small);
        foreach (var room in owner.Rooms)
        {
            if (room == null) continue;
            GUILayout.BeginHorizontal();
            GUILayout.Label(room.RoomId + " · " + (room.Unlocked ? "доступно" : "закрыто"), label, GUILayout.Width(370));
            Button("Переключить", () => owner.SetRoomAccess(room, !room.Unlocked), owner.CanMutate);
            GUILayout.EndHorizontal();
        }
        Section("Станции и взаимодействия");
        foreach (var station in owner.Stations)
        {
            if (station == null) continue;
            GUILayout.Label(ShortName(station) + " · " + (station.CanInteract ? "взаимодействие доступно" : "недоступно"), label);
            GUILayout.BeginHorizontal();
            Button(station.InteractionEnabled ? "Запретить" : "Разрешить", () => owner.SetStationInteraction(station, !station.InteractionEnabled), owner.CanMutate);
            Button("Проверить взаимодействие", () => owner.InspectStation(station), owner.CanMutate && station.CanInteract);
            GUILayout.EndHorizontal();
        }
        Section("Остальные взаимодействия");
        foreach (var source in owner.OtherInteractions)
        {
            if (source == null) continue;
            bool available = source.isActiveAndEnabled && ((IBunkerInteractable)source).CanInteract;
            GUILayout.Label(ShortName(source) + " · " + source.GetType().Name + " · " + (available ? "доступно" : "недоступно"), label);
            Button("Проверить взаимодействие", () => owner.InspectInteraction(source), owner.CanMutate && available);
        }
    }
    private void Progress()
    {
        var meta = MetaProgressionManager.Instance;
        if (meta == null) return;
        Section("Миссии · тестовые стадии");
        GUILayout.Label("Шаг: принять → имитировать выполнение целей → вернуться → штатная выплата. Назад: восстановить тестовое состояние.", small);
        var missions = meta.Missions;
        if (missions != null) foreach (var mission in missions.Catalog.Missions)
        {
            if (mission == null) continue;
            GUILayout.Label(mission.Title + " · " + missions.GetState(mission.MissionId) + " · " + mission.TargetSectorId, label);
            GUILayout.Label(string.Join(" / ", mission.Objectives.Select(o => o.Description)), small);
            Button(missions.IsAvailable(mission.MissionId) ? "Следующая тестовая стадия" : "Недоступно в production",
                () => owner.AdvanceMission(mission.MissionId), owner.CanMutate && missions.IsAvailable(mission.MissionId) && missions.GetState(mission.MissionId) != MissionState.Completed);
        }
        Section("Escape Protocol");
        GUILayout.Label(meta.HasEscapeProtocol ? meta.GetEscapeObjectiveText() : "Нет production-конфигурации", label);
        if (meta.HasEscapeProtocol)
        {
            GUILayout.Label($"Этап {meta.CurrentEscapeStage}/{meta.EscapeProtocol.Stages.Count} · funded={meta.EscapeProtocolFunded} · completed={meta.EscapeProtocolCompleted}", small);
            for (int i = 0; i < meta.EscapeProtocol.Stages.Count; i++)
            {
                var stage = meta.EscapeProtocol.Stages[i];
                GUILayout.Label($"{stage.id}: {meta.GetEscapeInvestment(i)}/{stage.cost} ядер · {(meta.IsEscapeStageCompleted(i) ? "готово" : "ожидает")}", small);
            }
            Button("Вложить ядра в текущий этап", () => owner.FundEscapeStage(), owner.CanMutate && !meta.EscapeProtocolFunded);
        }
        var map = meta.SurfaceMap;
        if (map == null) return;
        Section("Обычный забег");
        GUILayout.Label("Сектор: " + (map.SelectedSectorId ?? "стартовый доступный") + " · задание: " + (map.SelectedTask?.Title ?? "не выбрано"), label);
        Button("Запустить обычный забег через BunkerRunStarter", () => owner.StartNormalRun(), owner.CanMutate);
        Section("Карта · выбор и тестовый прогресс");
        foreach (var sector in map.Definition.Sectors)
        {
            var status = map.GetStatus(sector.Id);
            GUILayout.Label(sector.Id + " · " + status + (map.SelectedSectorId == sector.Id ? " · ВЫБРАН" : ""), label);
            GUILayout.BeginHorizontal();
            Button("Выбрать", () => owner.SelectSector(sector.Id), owner.CanMutate && status != SurfaceSectorStatus.Locked);
            Button("Тест: пройден", () => owner.CompleteMapSector(sector.Id), owner.CanMutate && status == SurfaceSectorStatus.Available && !sector.IsEscapeFinal);
            GUILayout.EndHorizontal();
        }
        Section("Задания экспедиции");
        GUILayout.Label("Доступность и выбор — реальные API карты. Выполнение и выдача ядер проверяются в запущенном забеге.", small);
        var current = map.Definition.Find(map.SelectedSectorId);
        if (current != null) foreach (var task in current.ExpeditionTasks)
        {
            if (task == null) continue;
            GUILayout.Label(task.Title + $" · {task.Condition} {task.Target} · {task.RewardCores} ядер", label);
            GUILayout.Label(task.Description, small);
            Button(map.SelectedTask == task ? "Выбрано" : "Выбрать задание", () => owner.SelectTask(task.Id), owner.CanMutate && task.IsAvailable(current.BuildRunConfig(map.Definition.Id)));
        }
        Button("Без задания", () => owner.SelectTask(null), owner.CanMutate && current != null);
        var rewardsJson = PlayerPrefs.GetString(ExpeditionTaskRun.LastRewardsKey, "");
        if (!string.IsNullOrEmpty(rewardsJson))
        {
            var reward = JsonUtility.FromJson<ExpeditionCoreRewards>(rewardsJson);
            if (reward != null) GUILayout.Label($"Последний итог: {reward.TaskTitle} · выполнено={reward.TaskCompleted} · ядра {reward.TaskCores}+{reward.SectorCores}+{reward.FirstClearCores}={reward.Total}", small);
        }
    }
    private void Barks()
    {
        var barks = owner.Barks;
        if (barks == null) { GUILayout.Label("Система реплик не найдена.", label); return; }
        Section("Состояние атмосферных реплик");
        GUILayout.Label("Активно: " + barks.IsSpeaking + " · приоритет: " + barks.DebugActiveCategory, label);
        GUILayout.Label("Текст: " + (barks.IsSpeaking ? barks.CurrentText : "—"), label);
        GUILayout.Label("Подавление: " + barks.DebugBlockReason, label);
        GUILayout.Label($"До случайной: {barks.DebugRandomRemaining:0} с · {barks.DebugLastAttempt}", small);
        Button("Завершить текущую реплику", () => owner.StopBark(), owner.CanMutate && barks.IsSpeaking);
        Section("Вызов существующих реплик");
        Button("Случайная реплика", () => owner.PlayBark(BunkerBarkCategory.Random, null), owner.CanMutate);
        Button("Возвращение из забега", () => owner.PlayBark(BunkerBarkCategory.Event, "return_from_run"), owner.CanMutate);
        GUILayout.Label("Приоритеты, кулдауны, вероятность и запрет повтора сохраняются. Причина отказа показана выше.", small);
        foreach (var group in barks.DebugLines.Where(l => l != null && l.category != BunkerBarkCategory.Random).GroupBy(l => (l.category, l.key)))
        {
            var key = group.Key;
            GUILayout.Space(7);
            GUILayout.Label(key.category + " / " + key.key, label);
            GUILayout.Label(string.Join("\n", group.Select(l => "• " + l.text)), small);
            Button("Вызвать", () => owner.PlayBark(key.category, key.key), owner.CanMutate);
        }
    }
    private void Saves()
    {
        Section("Изолированная developer-сессия");
        GUILayout.Label(PlayerPrefs.IsSandboxActive ?
            (PlayerPrefs.IsFreshSandbox ? "Чистое тестовое состояние." : "Тестовая копия текущего сохранения.") + $" Изменённых ключей: {PlayerPrefs.SandboxChangedKeys}." :
            "Обычный профиль. Любые изменяющие debug-команды заблокированы.", label);
        GUILayout.Label("Тестовые записи, удаления и новые reward receipt остаются в отдельном слое памяти. Основные PlayerPrefs не изменяются. Копия читает исходные значения основного профиля, пока ключ не изменён в тесте.", label);
        GUILayout.Label("В Editor тест переживает перекомпиляцию скриптов через SessionState. Stop Play Mode или закрытие development-сборки отбрасывает тест. Экспорт теста в основной прогресс отсутствует.", small);
        Button("Копия текущего сохранения", () => owner.BeginTestCopy(), !PlayerPrefs.IsSandboxActive);
        Button("Восстановить исходное тестовое состояние", () => Confirm("Отбросить все тестовые изменения и перезагрузить бункер?", () => owner.RestoreTestStart()), PlayerPrefs.IsSandboxActive);
        Button("Чистое тестовое состояние", () => Confirm("Создать пустой тестовый профиль и перезагрузить бункер? Основное сохранение останется нетронутым.", () => owner.BeginFreshTest()));
        Button("Вернуться к обычному сохранению", () => Confirm("Отбросить тестовую сессию и загрузить обычный профиль? Runtime-кэши будут пересозданы.", () => owner.RestoreMainSave()), PlayerPrefs.IsSandboxActive);
        GUILayout.Label("Перед переключением завершите мини-игру/интро и закройте игровые окна. Переключение в активном забеге запрещено.", small);
        Section("Объекты сцены");
        Button("Обновить список объектов", owner.RefreshTargets);
        GUILayout.Label($"Ворота: {owner.Gates.Length} · взаимодействия: {owner.Stations.Length + owner.OtherInteractions.Length}", label);
    }
}
#endif
