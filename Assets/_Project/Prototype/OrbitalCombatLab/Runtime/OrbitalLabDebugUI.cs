using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalLabDebugUI : MonoBehaviour
    {
        public bool MenuOpen { get; private set; } = true;
        public bool SuppressedByGoldenPath { get; set; }
        public int HoveredRing { get; private set; } = -1;
        public bool UpgradeSelectionActive => upgradeSelection;
        public bool PointerOverMenu
        {
            get
            {
                if (lab != null && lab.GoldenPath != null && lab.GoldenPath.PointerOverUi) return true;
                if (SuppressedByGoldenPath || !MenuOpen) return false;
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                return PanelRect.Contains(mouse);
            }
        }

        private Rect PanelRect => new(12f, 12f, Mathf.Min(520f, Screen.width - 24f), Screen.height - 24f);
        private readonly bool[] open = { true, true, true, true, true, true, false, false, false, false, true, false,
            true, true, true, false };
        private OrbitalCombatLabController lab;
        private Vector2 scroll;
        private GUIStyle title, section, hint, stat;
        private float qPressedAt = -99f;
        private float ePressedAt = -99f;
        private bool upgradeSelection;
        private bool previewUpgrade = true;
        private OrbitalRingUpgradeType pendingUpgrade = OrbitalRingUpgradeType.Amplifier;
        private float savedTimeScale = 1f;
        private bool massFillConfirmation;
        private float pendingFillFraction;
        private int pendingFillPerRing;

        public void Configure(OrbitalCombatLabController controller) => lab = controller;
        public void SetMenuOpen(bool value) => MenuOpen = value;

        private void Update()
        {
            if (SuppressedByGoldenPath) return;
            if (Input.GetKeyDown(KeyCode.F1)) MenuOpen = !MenuOpen;
            HoveredRing = -1;
            if (upgradeSelection)
            {
                HandleUpgradeSelection();
                return;
            }
            if (!MenuOpen || !lab.RingEditMode || lab.RingCount == 0 || lab.Drag.IsDragging) return;
            HandlePhaseKey(KeyCode.Q, -1f, ref qPressedAt);
            HandlePhaseKey(KeyCode.E, 1f, ref ePressedAt);
            OrbitalRing selected = SelectedRing();
            if (Input.GetKeyDown(KeyCode.R)) selected.Settings.Clockwise = !selected.Settings.Clockwise;
            if (Input.GetKeyDown(KeyCode.Space)) selected.Settings.Paused = !selected.Settings.Paused;
            if (Camera.main == null || PointerOverMenu) return;
            Vector2 world = MouseWorld();
            int nearest = FindNearestRing(world, .5f);
            HoveredRing = nearest;
            if (Input.GetMouseButtonDown(0) && nearest >= 0) lab.SelectedRing = nearest;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f && nearest >= 0)
            {
                lab.SelectedRing = nearest;
                OrbitalRingSettings settings = lab.Rings[nearest].Settings;
                settings.RotationSpeed = Mathf.Clamp(settings.RotationSpeed + wheel * 6f, 0f, 220f);
            }
        }

        private void OnGUI()
        {
            if (lab == null || SuppressedByGoldenPath) return;
            EnsureStyles();
            if (MenuOpen) DrawMenu(); else GUI.Box(new Rect(12f, 12f, 322f, 31f), "F1 → ORBITAL COMBAT LAB", hint);
            if (lab.ShowStats) DrawStats();
            if (MenuOpen && lab.RingEditMode && lab.RingCount > 0) DrawRingEditOverlay();
            if (upgradeSelection) DrawUpgradeOverlay();
            if (Time.unscaledTime < lab.UserMessageUntil)
                GUI.Box(new Rect(Screen.width * .5f - 260f, 12f, 520f, 38f), lab.UserMessage, stat);
        }

        private void DrawMenu()
        {
            Rect panel = PanelRect;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 16f));
            GUILayout.Label("ORBITAL COMBAT LAB", title);
            GUILayout.Label("F1 — меню · WASD — движение · ЛКМ — drag · Tab — полный обзор станции", hint);
            scroll = GUILayout.BeginScrollView(scroll);
            lab.PatternCombat = Toggle(lab.PatternCombat, "PATTERN COMBAT",
                "Выключено — исходный бой; включено — links, resonance и поля колец воздействуют на толпу.");
            if (Fold(0, "1. СОСТОЯНИЕ ТЕСТА")) DrawState();
            if (Fold(1, "2. КОЛЬЦА")) DrawRings();
            if (Fold(2, "3. ВЫБРАННОЕ КОЛЬЦО")) DrawSelectedRing();
            if (Fold(3, "4. ОБЪЕКТЫ")) DrawObjects();
            if (Fold(4, "5. LINK NODE")) DrawLinks();
            if (Fold(5, "6. РЕЗОНАНС")) DrawResonance();
            if (Fold(6, "7. СЛЕДЫ")) DrawTrails();
            if (Fold(7, "8. ФОРМАЦИИ")) DrawFormations();
            if (Fold(8, "9. ФОРМА ОРБИТЫ")) DrawShape();
            if (Fold(9, "10. ПОЛЕ КОЛЬЦА")) DrawField();
            if (Fold(10, "11. ПРЕСЕТЫ")) DrawPresets();
            if (Fold(11, "12. СТАТИСТИКА И ВИЗУАЛ")) DrawVisuals();
            if (Fold(12, "13. МАСШТАБ И АВТО-КОЛЬЦА")) DrawScalability();
            if (Fold(13, "14. UPGRADE TEST MODE")) DrawUpgrades();
            if (Fold(14, "15. ORBITAL CORE")) DrawCore();
            if (Fold(15, "16. БЫСТРЫЕ ТЕСТЫ")) DrawQuickTests();
            // IMGUI must emit the same control count during Layout and Repaint. GUI.tooltip
            // can differ between those events, so keep this label unconditional.
            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? " " : "ⓘ " + GUI.tooltip, hint);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawState()
        {
            Row(() => Button("START", lab.ApplyStartState, "Исходная маленькая система."),
                () => Button("MID", lab.ApplyMidState, "Три боевых эшелона."),
                () => Button("FINAL", lab.ApplyFinalState, "Исходная максимальная станция без pattern-оружия."));
            Row(() => Button("RESET", lab.ResetTest, "Полностью очищает links, trails и resonance."),
                () => Button("CLEAR OBJECTS", lab.ClearMounted, "Удалить все объекты и временную геометрию."));
            Row(() => Button("50", () => lab.SpawnEnemies(50), "Плотность 50."),
                () => Button("100", () => lab.SpawnEnemies(100), "Плотность 100."),
                () => Button("200", () => lab.SpawnEnemies(200), "Плотность 200."),
                () => Button("300", () => lab.SpawnEnemies(300), "Плотность 300."));
            lab.PlayerImmortal = Toggle(lab.PlayerImmortal, "Бессмертие игрока", "Отключите только для проверки давления толпы.");
            lab.Crowd.DamagePlayer = Toggle(lab.Crowd.DamagePlayer, "Враги наносят урон", "Работает при выключенном бессмертии.");
        }

        private void DrawRings()
        {
            Row(() => Button("+1 КОЛЬЦО", lab.AddRing, "Добавить автоматически настроенное кольцо."),
                () => Button("−1 КОЛЬЦО", lab.RemoveRing, "Удалить внешнее кольцо вместе с объектами."));
            Row(() => Button("+5 КОЛЕЦ", () => lab.AddRings(5), "Массовое добавление до Safety Ring Limit."),
                () => Button("+10 КОЛЕЦ", () => lab.AddRings(10), "Массовое добавление до Safety Ring Limit."));
            Row(() => Button("ОСТАВИТЬ ТОЛЬКО 1", lab.KeepOnlyOneRing, "Удалить внешние кольца и их оружие."),
                () => Button("ОЧИСТИТЬ СТАНЦИЮ", lab.ClearStation, "Одно пустое кольцо и сброс улучшений."));
            lab.ShowRings = Toggle(lab.ShowRings, "Показывать кольца", "Линии всех орбит.");
            lab.ShowMounts = Toggle(lab.ShowMounts, "Показывать точки", "Свободные и занятые позиции.");
            lab.RingEditMode = Toggle(lab.RingEditMode, "РЕДАКТИРОВАНИЕ КОЛЕЦ", "Клик линии, колесо скорости, Q/E постоянного смещения фазы, R направления, Space паузы.");
            lab.PauseSelectedRingWhileEditing = Toggle(lab.PauseSelectedRingWhileEditing,
                "PAUSE SELECTED RING WHILE EDITING", "Временно останавливает выбранное кольцо, не меняя его скорость. После выхода вращение продолжается.");
            if (lab.RingEditMode) GUILayout.Label("Наведи на линию → клик → Q/E. Shift — точно, Ctrl — 45°. Колесо — скорость, R — reverse, Space — pause.", hint);
        }

        private void DrawSelectedRing()
        {
            if (lab.RingCount == 0) return;
            for (int i = 0; i < lab.RingCount; i++)
            {
                if (i % 12 == 0) GUILayout.BeginHorizontal();
                GUI.backgroundColor = i == lab.SelectedRing ? new Color(.2f, .9f, 1f) : Color.white;
                if (GUILayout.Button((i + 1).ToString(), GUILayout.Height(25f))) lab.SelectedRing = i;
                if (i % 12 == 11 || i == lab.RingCount - 1) GUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            OrbitalRing ring = SelectedRing();
            OrbitalRingSettings s = ring.Settings;
            s.Radius = Slider("Radius · м", s.Radius, .8f, 60f, "Базовый размер орбиты.");
            s.RotationSpeed = Slider("Скорость вращения · °/с · ОБА", s.RotationSpeed, 0f, 220f, "Угловая скорость рисунка и всего оружия на этом кольце.");
            s.Clockwise = Toggle(s.Clockwise, "Вращение по часовой стрелке", "Разворачивает поток объектов.");
            s.Paused = Toggle(s.Paused, "Пауза выбранного кольца", "Замораживает только это кольцо, сохраняя фазу.");
            GUILayout.Label($"Текущий угол вращения: {ring.RotationAngle:0.0}°", hint);
            ring.PhaseOffset = Slider("Смещение формации · ° · ВИЗУАЛ", ring.PhaseOffset, 0f, 360f,
                "Постоянное пользовательское смещение формации. Вращающийся угол продолжает жить отдельно.");
            int mounts = Mathf.RoundToInt(Slider("Крепления · шт · ОБА", s.MaxMounts, 1f, OrbitalRing.AbsoluteMaxMounts, "Количество видимых позиций для оружия на этом кольце."));
            if (mounts != s.MaxMounts) lab.SetSelectedRingMaxMounts(mounts);
            Row(() => Button("SYNC PREV", () => lab.SynchronizeSelectedWithPrevious(true), "Копирует скорость, направление и фазу предыдущего."),
                () => Button("COPY SPEED", lab.CopyPreviousSpeed, "Копирует только угловую скорость предыдущего кольца."));
            Row(() => Button("×0.5", () => lab.MultiplySelectedSpeed(.5f), "Замедлить вдвое."),
                () => Button("×1", () => lab.MultiplySelectedSpeed(1f), "Сохранить."),
                () => Button("×1.5", () => lab.MultiplySelectedSpeed(1.5f), "Ускорить."),
                () => Button("×2", () => lab.MultiplySelectedSpeed(2f), "Ускорить вдвое."));
            Row(() => Button("−15°", () => lab.NudgeSelectedPhase(-15f), "Уменьшить постоянный Phase Offset."),
                () => Button("+15°", () => lab.NudgeSelectedPhase(15f), "Увеличить постоянный Phase Offset."));
            Button("СБРОСИТЬ ФАЗУ", lab.ResetSelectedPhase, "Вернуть Ring Phase Offset к 0°, не сбрасывая текущий вращающийся угол.");
            Button("ВЫРОВНЯТЬ ПО НАПРАВЛЕНИЮ ИГРОКА", lab.AlignMountZeroWithForward, "Сместить формацию так, чтобы Mount 0 смотрел по последнему направлению движения.");
        }

        private void DrawObjects()
        {
            Row(() => Button("+ GUN", () => lab.AddMounted(OrbitalMountType.Gun), "Голубая дальняя атака."),
                () => Button("+ BLADE", () => lab.AddMounted(OrbitalMountType.Blade), "Красный контактный урон."));
            Row(() => Button("+ PUSHER", () => lab.AddMounted(OrbitalMountType.Pusher), "Жёлтый crowd control."),
                () => Button("+ LINK NODE", () => lab.AddMounted(OrbitalMountType.LinkNode), "Пурпурный узел геометрии."));
            Row(() => Button("+ MINE LAYER", () => lab.AddMounted(OrbitalMountType.MineLayer), "Оставляет зелёно-оранжевые мины в мировой позиции."),
                () => Button("+ ARC EMITTER", () => lab.AddMounted(OrbitalMountType.ArcEmitter), "Короткий цепной бело-фиолетовый разряд."));
            Button("FILL ALL RINGS", lab.FillAllRings, "Заполнить свободные точки базовыми объектами.");
            lab.ShowAttackRanges = Toggle(lab.ShowAttackRanges, "Радиусы атак", "Рабочие зоны Gun и Pusher.");
            lab.Gun.Damage = Slider("Gun Damage", lab.Gun.Damage, 1f, 60f, "Урон projectile.");
            lab.Blade.Damage = Slider("Blade Damage", lab.Blade.Damage, 1f, 90f, "Контактный урон.");
            lab.Pusher.PushForce = Slider("Pusher Force", lab.Pusher.PushForce, 1f, 35f, "Сила раздвигания толпы.");
            GUILayout.Label("MINE LAYER · БОЙ", section);
            lab.Mines.Damage = Slider("Урон мины · ед.", lab.Mines.Damage, 1f, 90f, "ВЛИЯЕТ НА: урон всем врагам внутри взрыва.");
            lab.Mines.DropInterval = Slider("Интервал установки · сек", lab.Mines.DropInterval, .2f, 4f, "ВЛИЯЕТ НА: плотность минного рисунка вдоль орбиты.");
            lab.Mines.TriggerRadius = Slider("Радиус срабатывания · м", lab.Mines.TriggerRadius, .2f, 2f, "ВЛИЯЕТ НА: дистанцию обнаружения врага.");
            lab.Mines.ExplosionRadius = Slider("Радиус взрыва · м", lab.Mines.ExplosionRadius, .4f, 4f, "ВЛИЯЕТ НА: площадь AoE и видимый импульс.");
            lab.Mines.Lifetime = Slider("Время жизни мины · сек", lab.Mines.Lifetime, 1f, 30f, "ВЛИЯЕТ НА: длину минного следа; старая мина возвращается в pool.");
            lab.Mines.MaximumActivePerLayer = Mathf.RoundToInt(Slider("Мин на один модуль · шт", lab.Mines.MaximumActivePerLayer, 1f, 16f, "Ограничивает активные мины одного Mine Layer."));
            lab.Mines.PushForce = Slider("Отталкивание взрыва · импульс", lab.Mines.PushForce, 0f, 20f, "ВЛИЯЕТ НА: crowd control внутри Explosion Radius.");
            Button("RESET MINE PARAMETERS", ResetMineParameters, "Вернуть понятные исходные значения Mine Layer.");
            GUILayout.Label("ARC EMITTER · БОЙ", section);
            lab.Arc.Damage = Slider("Урон дуги · ед.", lab.Arc.Damage, 1f, 60f, "ВЛИЯЕТ НА: первый и затухающие chain hits.");
            lab.Arc.Cooldown = Slider("Перезарядка дуги · сек", lab.Arc.Cooldown, .15f, 3f, "Меньше — короткие вспышки происходят чаще.");
            lab.Arc.Range = Slider("Дальность дуги · м", lab.Arc.Range, 1f, 14f, "ВЛИЯЕТ НА: ранний перехват на внешних кольцах.");
            lab.Arc.ChainCount = Mathf.RoundToInt(Slider("Целей в цепи · шт", lab.Arc.ChainCount, 1f, 10f, "Количество коротких последовательных разрядов."));
            lab.Arc.ChainRange = Slider("Дальность следующего перехода · м", lab.Arc.ChainRange, .5f, 8f, "ВЛИЯЕТ НА: может ли молния найти следующую цель после первого попадания.");
            lab.Arc.PulseBonus = Slider("Бонус Core Pulse · ×", lab.Arc.PulseBonus, 1f, 4f, "ВЛИЯЕТ НА: урон следующего discharge после прохождения волны ядра.");
            lab.Arc.LinkConduction = Toggle(lab.Arc.LinkConduction, "LINK CONDUCTION · ОБА", "Link Node на том же кольце добавляет один переход цепи.");
            Button("RESET ARC PARAMETERS", ResetArcParameters, "Вернуть понятные исходные значения Arc Emitter.");
            GUILayout.Label("ВИЗУАЛ ОРУЖИЯ", section);
            Row(() => Choice("PRIMITIVES", lab.WeaponVisuals.Mode == OrbitalWeaponVisualMode.Primitives,
                    () => lab.ApplyWeaponVisualMode(OrbitalWeaponVisualMode.Primitives), "Контрольная геометрическая версия без prefab-визуалов."),
                () => Choice("MINI WEAPONS", lab.WeaponVisuals.Mode == OrbitalWeaponVisualMode.MiniWeapons,
                    () => lab.ApplyWeaponVisualMode(OrbitalWeaponVisualMode.MiniWeapons), "Production miniWeapons только как дочерние визуалы; Lab сохраняет весь бой."));
            Row(() => Choice("TANGENTIAL", lab.WeaponVisuals.BladeOrientation == OrbitalBladeOrientation.Tangential,
                    () => lab.WeaponVisuals.BladeOrientation = OrbitalBladeOrientation.Tangential, "LaserSward режет по касательной движения."),
                () => Choice("RADIAL", lab.WeaponVisuals.BladeOrientation == OrbitalBladeOrientation.Radial,
                    () => lab.WeaponVisuals.BladeOrientation = OrbitalBladeOrientation.Radial, "LaserSward смотрит лезвием наружу."));
            lab.WeaponVisuals.PistolScale = Slider("Pistol Visual Scale", lab.WeaponVisuals.PistolScale, .4f, 3f, "Масштабирует только prefab visual, не projectile или drag root.");
            lab.WeaponVisuals.LaserSwardScale = Slider("LaserSward Visual Scale", lab.WeaponVisuals.LaserSwardScale, .4f, 3f, "Масштабирует только корпус и свет меча.");
            lab.WeaponVisuals.ImpulsGunScale = Slider("ImpulsGun Visual Scale", lab.WeaponVisuals.ImpulsGunScale, .4f, 3f, "Масштабирует только prefab visual, не Push Radius.");
            lab.WeaponVisuals.LinkNodeScale = Slider("Link Node Visual Scale", lab.WeaponVisuals.LinkNodeScale, .5f, 2f, "Размер пурпурного core без изменения link damage.");
            lab.WeaponVisuals.PistolRotationOffset = Slider("Pistol Rotation Offset", lab.WeaponVisuals.PistolRotationOffset, -180f, 180f, "Поправка +X forward Pistol относительно выстрела.");
            lab.WeaponVisuals.LaserSwardRotationOffset = Slider("LaserSward Rotation Offset", lab.WeaponVisuals.LaserSwardRotationOffset, -180f, 180f, "Поправка локального +Y лезвия.");
            lab.WeaponVisuals.ImpulsGunRotationOffset = Slider("ImpulsGun Rotation Offset", lab.WeaponVisuals.ImpulsGunRotationOffset, -180f, 180f, "Поправка направления корпуса ImpulsGun.");
            lab.WeaponVisuals.SortingOffset = Mathf.RoundToInt(Slider("Weapon Sorting Offset", lab.WeaponVisuals.SortingOffset, 1f, 30f, "Порядок prefab sprites поверх колец и толпы."));
            lab.WeaponVisuals.EffectsEnabled = Toggle(lab.WeaponVisuals.EffectsEnabled, "MINI WEAPON EFFECTS", "Recoil/muzzle/pulse запускаются только синхронно с Lab-действием.");
            lab.WeaponVisuals.EffectIntensity = Slider("Effect Intensity", lab.WeaponVisuals.EffectIntensity, 0f, 1f, "Уменьшает размер и длительность готовых particles, если они создают шум.");
            lab.WeaponVisuals.ShowPrototypeColliders = Toggle(lab.WeaponVisuals.ShowPrototypeColliders, "Show Prototype Colliders", "Оранжевый круг показывает область Lab-логики, независимую от prefab collider.");
            lab.WeaponVisuals.ShowMuzzlePoints = Toggle(lab.WeaponVisuals.ShowMuzzlePoints, "Show Muzzle Points", "Жёлтая точка показывает фактический origin projectile.");
            lab.WeaponVisuals.ShowVisualForward = Toggle(lab.WeaponVisuals.ShowVisualForward, "Show Visual Forward", "Зелёный луч показывает визуальный forward.");
            lab.WeaponVisuals.ShowMountRoots = Toggle(lab.WeaponVisuals.ShowMountRoots, "Show Mount Roots", "Показывает нейтральный корень orbital/drag/combat.");
        }

        private void DrawLinks()
        {
            Row(() => Choice("PAIRS", lab.Links.Mode == OrbitalLinkMode.Pairs, () => lab.Links.Mode = OrbitalLinkMode.Pairs, "Попарные хорды."),
                () => Choice("CHAIN", lab.Links.Mode == OrbitalLinkMode.Chain, () => lab.Links.Mode = OrbitalLinkMode.Chain, "Последовательная ломаная."),
                () => Choice("ALL NEARBY", lab.Links.Mode == OrbitalLinkMode.AllNearby, () => lab.Links.Mode = OrbitalLinkMode.AllNearby, "Ближайшие связи в пределах дистанции."));
            lab.Links.ShowLinks = Toggle(lab.Links.ShowLinks, "Показывать связи · ВИЗУАЛ", "Показывает постоянные энергетические сегменты Link Node.");
            lab.Links.DealDamage = Toggle(lab.Links.DealDamage, "Урон связей · БОЙ", "Пересечение становится оружием при PATTERN COMBAT ON.");
            lab.Links.Damage = Slider("Урон связи · ед./срабатывание · БОЙ", lab.Links.Damage, 0f, 40f, "Сколько урона получает враг, пересёкший фиолетовую линию. Для двух колец используется средний Damage Multiplier.");
            lab.Links.HitCooldown = Slider("Повторный урон · сек · БОЙ", lab.Links.HitCooldown, .08f, 1.5f, "Минимальное время до следующего урона той же цели. Меньше — чаще.");
            GUILayout.Label($"Максимум ≈ {1f / Mathf.Max(.01f, lab.Links.HitCooldown):0.##} попадания/сек по одной цели", hint);
            lab.Links.LineWidth = Slider("Толщина связи · world units · ОБА", lab.Links.LineWidth, .015f, .22f, "Меняет видимую толщину и ширину hit-зоны.");
            lab.Links.MaxDistance = Slider("Дальность связи · м · ОБА", lab.Links.MaxDistance, 2f, 24f, "Максимальное расстояние, на котором два Link Node соединяются в ALL NEARBY.");
            lab.Links.PulseSpeed = Slider("Скорость пульсации · Гц · ВИЗУАЛ", lab.Links.PulseSpeed, 0f, 10f, "Скорость изменения яркости. На урон не влияет.");
            Button("ТЕСТОВЫЙ ВРАГ НА ЛИНИЮ", lab.Pattern.PlaceTestEnemyOnLink, "Перемещает первого активного врага в центр первой видимой связи.");
            Color linkColor = lab.Links.LineColor;
            linkColor.r = Slider("Link Color R", linkColor.r, 0f, 1f, "Красный канал цвета энергетической геометрии.");
            linkColor.g = Slider("Link Color G", linkColor.g, 0f, 1f, "Зелёный канал: поднимите для cyan/white рисунка.");
            linkColor.b = Slider("Link Color B", linkColor.b, 0f, 1f, "Синий канал: вместе с красным создаёт magenta.");
            linkColor.a = 1f;
            lab.Links.LineColor = linkColor;
            Button("RESET LINK PARAMETERS", ResetLinkParameters, "Вернуть исходные урон, частоту, толщину, дальность и цвет связей.");
        }

        private void DrawResonance()
        {
            lab.Resonance.Enabled = Toggle(lab.Resonance.Enabled, "Resonance ON", "Ищет выстраивания объектов разных колец.");
            lab.Resonance.VisualOnly = Toggle(lab.Resonance.VisualOnly, "VISUAL ONLY", "Показывает событие без влияния на баланс.");
            Row(() => Choice("VOLLEY", lab.Resonance.Mode == OrbitalResonanceMode.RadialVolley, () => lab.Resonance.Mode = OrbitalResonanceMode.RadialVolley, "Усиленный радиальный залп."),
                () => Choice("BEAM", lab.Resonance.Mode == OrbitalResonanceMode.Beam, () => lab.Resonance.Mode = OrbitalResonanceMode.Beam, "Короткий луч."),
                () => Choice("SHOCK", lab.Resonance.Mode == OrbitalResonanceMode.Shockwave, () => lab.Resonance.Mode = OrbitalResonanceMode.Shockwave, "Ударная волна."),
                () => Choice("CYCLE", lab.Resonance.Mode == OrbitalResonanceMode.Cycle, () => lab.Resonance.Mode = OrbitalResonanceMode.Cycle, "Чередование режимов."));
            lab.Resonance.AlignmentTolerance = Slider("Допуск резонанса · ±° · ОБА", lab.Resonance.AlignmentTolerance, 2f, 35f,
                "Насколько близко по углу должны оказаться объекты разных колец. Больше угол — чаще резонанс.");
            lab.Resonance.MinimumObjects = Mathf.RoundToInt(Slider("Минимум объектов · шт · БОЙ", lab.Resonance.MinimumObjects, 2f, 6f, "Количество объектов именно с разных колец в одной радиальной линии."));
            lab.Resonance.Cooldown = Slider("Cooldown резонанса · сек · БОЙ", lab.Resonance.Cooldown, .25f, 4f, "Пауза между событиями резонанса.");
            GUILayout.Label($"Максимум ≈ {1f / Mathf.Max(.01f, lab.Resonance.Cooldown):0.##} резонанса/сек", hint);
            lab.Resonance.Damage = Slider("Урон резонанса · ед. · БОЙ", lab.Resonance.Damage, 0f, 60f, "Используется BEAM/SHOCKWAVE и fallback VOLLEY.");
            lab.Resonance.Range = Slider("Дальность резонанса · м · ОБА", lab.Resonance.Range, 3f, 24f, "Длина beam/volley и масштаб shockwave.");
            Button("RESET RESONANCE PARAMETERS", ResetResonanceParameters, "Вернуть исходный допуск, частоту, урон и дальность резонанса.");
        }

        private void DrawTrails()
        {
            Row(() => Choice("OFF", lab.Trails.Mode == OrbitalTrailMode.Off, () => lab.Trails.Mode = OrbitalTrailMode.Off, "Без следов."),
                () => Choice("SHORT", lab.Trails.Mode == OrbitalTrailMode.Short, () => lab.Trails.Mode = OrbitalTrailMode.Short, "Короткая дуга."),
                () => Choice("MEDIUM", lab.Trails.Mode == OrbitalTrailMode.Medium, () => lab.Trails.Mode = OrbitalTrailMode.Medium, "Заметный рисунок."),
                () => Choice("HYPNOTIC", lab.Trails.Mode == OrbitalTrailMode.Hypnotic, () => lab.Trails.Mode = OrbitalTrailMode.Hypnotic, "Длинная кинетическая скульптура."));
            lab.Trails.Length = Slider("Длина следа · сек · ВИЗУАЛ", lab.Trails.Length, .15f, 2.5f, "Сколько времени остаётся цветная дуга за объектом.");
            lab.Trails.Width = Slider("Толщина следа · м · ВИЗУАЛ", lab.Trails.Width, .015f, .24f, "Видимая толщина дуги в мире.");
            lab.Trails.Alpha = Slider("Яркость следа · ВИЗУАЛ", lab.Trails.Alpha, .05f, 1f, "Прозрачность до применения визуального профиля.");
            lab.Trails.FollowVisualProfile = Toggle(lab.Trails.FollowVisualProfile, "TRAILS FOLLOW VISUAL PROFILE",
                "CLEAN/COMBAT выключают следы, HYPNOTIC/MAXIMUM включают длинные. Отключите для ручной настройки.");
        }

        private void DrawFormations()
        {
            lab.FreeMountPhase = Toggle(lab.FreeMountPhase, "FREE MOUNT PHASE", "Drag вдоль текущего кольца меняет собственный угол; красный preview означает наложение.");
            lab.MinimumMountSpacing = Slider("Минимальный угол между объектами · ° · ОБА", lab.MinimumMountSpacing, 4f, 35f, "Не даёт двум объектам занять почти одинаковую точку кольца.");
            Row(() => Button("DISTRIBUTE", lab.DistributeSelectedEvenly, "Вернуть равномерные углы."),
                () => Button("CLUSTER", lab.ClusterSelected, "Собрать плотный сектор."));
            Row(() => Button("FRONT ARC", lab.FrontArcSelected, "Собрать по направлению движения."),
                () => Button("ALTERNATE", lab.AlternateSelected, "Два сектора с пустотами."));
        }

        private void DrawShape()
        {
            OrbitalRingSettings s = SelectedRing().Settings;
            Row(() => Choice("CIRCLE", s.Shape == OrbitalShape.Circle, () => s.Shape = OrbitalShape.Circle, "Стабильная окружность."),
                () => Choice("ELLIPSE", s.Shape == OrbitalShape.Ellipse, () => s.Shape = OrbitalShape.Ellipse, "Вытянутая траектория."),
                () => Choice("BREATH", s.Shape == OrbitalShape.Breathing, () => s.Shape = OrbitalShape.Breathing, "Плавное расширение."),
                () => Choice("WOBBLE", s.Shape == OrbitalShape.Wobble, () => s.Shape = OrbitalShape.Wobble, "Лепестковое искажение."));
            if (s.Shape == OrbitalShape.Ellipse)
            {
                s.AspectRatio = Slider("Вытянутость · отношение · ВИЗУАЛ", s.AspectRatio, .55f, 2.3f, "1 — круг; дальше от 1 — сильнее эллипс.");
                s.ShapeRotation = Slider("Поворот эллипса · ° · ВИЗУАЛ", s.ShapeRotation, 0f, 360f, "Ориентация длинной оси.");
            }
            else if (s.Shape == OrbitalShape.Breathing)
            {
                s.BreathingAmplitude = Slider("Амплитуда дыхания · м · ОБА", s.BreathingAmplitude, 0f, 1.5f, "Насколько радиус кольца расширяется и сжимается.");
                s.BreathingFrequency = Slider("Частота дыхания · Гц · ОБА", s.BreathingFrequency, .05f, 2f, "Сколько циклов расширения происходит в секунду.");
                s.BreathingPhase = Slider("Фаза дыхания · ° · ВИЗУАЛ", s.BreathingPhase, 0f, 360f, "Сдвиг волны относительно соседних колец.");
            }
            else if (s.Shape == OrbitalShape.Wobble)
            {
                s.WobbleLobes = Mathf.RoundToInt(Slider("Лепестки · шт · ВИЗУАЛ", s.WobbleLobes, 2f, 10f, "Количество волн по окружности."));
                s.WobbleAmplitude = Slider("Глубина лепестков · м · ОБА", s.WobbleAmplitude, 0f, .9f, "Насколько траектория отклоняется от базового радиуса." );
                s.WobbleSpeed = Slider("Скорость деформации · рад/с · ВИЗУАЛ", s.WobbleSpeed, 0f, 8f, "Скорость движения волн по орбите.");
            }
        }

        private void DrawField()
        {
            OrbitalRingSettings s = SelectedRing().Settings;
            Row(() => Choice("GHOST", s.FieldMode == OrbitalRingFieldMode.Ghost, () => s.FieldMode = OrbitalRingFieldMode.Ghost, "Только удерживает объекты."),
                () => Choice("SLOW", s.FieldMode == OrbitalRingFieldMode.Slow, () => s.FieldMode = OrbitalRingFieldMode.Slow, "Замедляет у линии."),
                () => Choice("PULSE", s.FieldMode == OrbitalRingFieldMode.Pulse, () => s.FieldMode = OrbitalRingFieldMode.Pulse, "Периодический push."));
            Row(() => Choice("CUT", s.FieldMode == OrbitalRingFieldMode.Cut, () => s.FieldMode = OrbitalRingFieldMode.Cut, "Контактный урон линии."),
                () => Choice("CONDUCTOR", s.FieldMode == OrbitalRingFieldMode.Conductor, () => s.FieldMode = OrbitalRingFieldMode.Conductor, "Урон при resonance Link Node."));
            s.FieldWidth = Slider("Толщина поля · м · ОБА", s.FieldWidth, .05f, .8f, "Ширина активной полосы вокруг линии; апгрейд области визуально и физически расширяет её.");
            s.FieldDamage = Slider("Урон поля · ед. · БОЙ", s.FieldDamage, 0f, 30f, "Урон режимов CUT и CONDUCTOR за одно срабатывание.");
            s.SlowMultiplier = Slider("Остаток скорости врага · × · БОЙ", s.SlowMultiplier, .1f, 1f, "0.3 оставляет врагу 30% скорости; меньше — сильнее замедление.");
            s.FieldPushForce = Slider("Сила отталкивания · импульс · БОЙ", s.FieldPushForce, 0f, 20f, "Насколько сильно режим PULSE отбрасывает толпу.");
            s.PulseInterval = Slider("Интервал поля PULSE · сек · ОБА", s.PulseInterval, .2f, 5f, "Пауза между видимыми и игровыми импульсами кольца.");
            s.FieldTargetCooldown = Slider("Повторный урон одной цели · сек · БОЙ", s.FieldTargetCooldown, .08f, 1.5f, "Защита от нанесения урона каждый кадр.");
        }

        private void DrawPresets()
        {
            GUILayout.Label("ДВИЖЕНИЕ", section);
            Row(() => Button("GEAR", () => lab.ApplyMovementPreset(OrbitalMovementPreset.Gear), "Механическая передача."),
                () => Button("FLOWER", () => lab.ApplyMovementPreset(OrbitalMovementPreset.Flower), "Повторяющиеся лепестки."),
                () => Button("WAVE", () => lab.ApplyMovementPreset(OrbitalMovementPreset.Wave), "Фазовая волна."));
            Row(() => Button("SYNC", () => lab.ApplyMovementPreset(OrbitalMovementPreset.Sync), "Жёсткая конструкция."),
                () => Button("CHAOS", () => lab.ApplyMovementPreset(OrbitalMovementPreset.Chaos), "Несоизмеримые скорости."),
                () => Button("FREEZE", lab.ToggleFreeze, "Сохранить/восстановить скорости."));
            Button("RESTORE DEFAULT", () => lab.ApplyMovementPreset(OrbitalMovementPreset.Default), "Исходные скорости, направления, фазы и радиусы.");
            GUILayout.Label("СРАВНИТЕЛЬНЫЕ СОСТОЯНИЯ", section);
            Row(() => Button("PATTERN FLOWER", lab.ApplyPatternFlower, "Красивый повторяющийся механизм."),
                () => Button("COMBAT WEB", lab.ApplyCombatWeb, "Повреждающая сеть и BEAM."));
            Row(() => Button("ORBITAL FORTRESS", lab.ApplyOrbitalFortress, "Все роли и поля колец."),
                () => Button("HYPNOSIS", lab.ApplyHypnosis, "Кинетическая скульптура без шума."));
            Button("DIRECTED FORTRESS", lab.ApplyDirectedFortress, "Направленная боевая формация.");
            GUILayout.Label("MINI WEAPONS / LINKS", section);
            Row(() => Button("MINI WEAPONS START", lab.ApplyMiniWeaponsStart, "Один Pistol, одно кольцо, 50 врагов, trails OFF."),
                () => Button("MINI WEAPONS FLOWER", lab.ApplyMiniWeaponsFlower, "Три prefab-визуала и фиолетовые связи без trail-шума."));
            Row(() => Button("MINI WEAPONS FORTRESS", lab.ApplyMiniWeaponsFortress, "28 объектов, шесть колец и 300 врагов."),
                () => Button("LINK HYPNOSIS", lab.ApplyLinkHypnosis, "Только 16 Link Node и линии, без врагов и trails."));
        }

        private void DrawVisuals()
        {
            lab.ShowStats = Toggle(lab.ShowStats, "Показывать статистику", "Runtime-счётчики справа.");
            Row(() => Button("CLEAN", () => lab.ApplyVisualProfile(OrbitalVisualProfile.Clean), "Минимум линий."),
                () => Button("COMBAT", () => lab.ApplyVisualProfile(OrbitalVisualProfile.Combat), "Приоритет бою."),
                () => Button("HYPNOTIC", () => lab.ApplyVisualProfile(OrbitalVisualProfile.Hypnotic), "Приоритет узорам."),
                () => Button("MAXIMUM", () => lab.ApplyVisualProfile(OrbitalVisualProfile.Maximum), "Перегруженный предел."));
            lab.RingAlpha = Slider("Ring Alpha", lab.RingAlpha, .1f, 1.5f, "Интенсивность орбит.");
            lab.TrailAlpha = Slider("Trail Alpha", lab.TrailAlpha, .1f, 1.6f, "Интенсивность следов.");
            lab.LinkAlpha = Slider("Link Alpha", lab.LinkAlpha, .1f, 1.6f, "Интенсивность сети.");
            lab.ResonanceFlash = Slider("Resonance Flash", lab.ResonanceFlash, .1f, 1.6f, "Яркость alignment.");
            lab.EnemyAlpha = Slider("Enemy Alpha", lab.EnemyAlpha, .1f, 1f, "Прозрачность толпы.");
            lab.ProjectileAlpha = Slider("Projectile Alpha", lab.ProjectileAlpha, .1f, 1.5f, "Яркость projectile.");
            lab.CameraRig.AutoCamera = Toggle(lab.CameraRig.AutoCamera, "Auto Camera", "Максимальный базовый радиус без дёрганья от форм.");
            lab.CameraImpulse = Toggle(lab.CameraImpulse, "Camera impulse", "Слабая реакция на крупные события.");
            lab.SlowDuringDrag = Toggle(lab.SlowDuringDrag, "Замедлять drag", "TimeScale 0.2 с восстановлением.");
        }

        private void DrawScalability()
        {
            lab.SafetyRingLimit = Mathf.RoundToInt(Slider("Safety Ring Limit · колец", lab.SafetyRingLimit, 1f, 64f,
                "Мягкий предел Play Mode. Попытка превысить показывает предупреждение."));
            lab.SafetyObjectLimit = Mathf.RoundToInt(Slider("Safety Object Limit · объектов", lab.SafetyObjectLimit, 32f, 512f,
                "Выше этого числа массовое заполнение требует второго подтверждения."));
            GUILayout.Label("STRESS-ПРЕСЕТЫ", section);
            Row(() => Button("1 RING", () => lab.SetRingCount(1), "Точка отсчёта."),
                () => Button("3 RINGS", () => lab.SetRingCount(3), "Ранний рост."),
                () => Button("6 RINGS", () => lab.SetRingCount(6), "Старый максимум."),
                () => Button("8 RINGS", () => lab.SetRingCount(8), "Первый большой силуэт."));
            Row(() => Button("12 RINGS", () => lab.SetRingCount(12), "Гипнотическая середина."),
                () => Button("16 RINGS", () => lab.SetRingCount(16), "Предел читаемого роста."),
                () => Button("24 RINGS", () => lab.SetRingCount(24), "Экстремальный тест."),
                () => Button("32 RINGS", () => lab.SetRingCount(32), "Safety limit по умолчанию."));
            GUILayout.Label("РАСПРЕДЕЛЕНИЕ РАДИУСОВ", section);
            Row(() => Choice("CONSTANT GAP", lab.RingGeneration.SpacingMode == OrbitalRingSpacingMode.ConstantGap,
                    () => SetSpacing(OrbitalRingSpacingMode.ConstantGap), "Одинаковый интервал."),
                () => Choice("GROWING GAP", lab.RingGeneration.SpacingMode == OrbitalRingSpacingMode.GrowingGap,
                    () => SetSpacing(OrbitalRingSpacingMode.GrowingGap), "Внешние кольца дальше."),
                () => Choice("COMPRESSED", lab.RingGeneration.SpacingMode == OrbitalRingSpacingMode.Compressed,
                    () => SetSpacing(OrbitalRingSpacingMode.Compressed), "После порога интервал плавно сжимается."));
            lab.RingGeneration.FirstRingRadius = Slider("Первый радиус · м", lab.RingGeneration.FirstRingRadius, .8f, 3f, "Радиус внутреннего кольца.");
            lab.RingGeneration.BaseRingGap = Slider("Базовый интервал · м", lab.RingGeneration.BaseRingGap, .4f, 2f, "Расстояние между ранними кольцами.");
            lab.RingGeneration.GapGrowth = Slider("Рост/сжатие интервала", lab.RingGeneration.GapGrowth, .01f, .2f, "GROWING: прибавка; COMPRESSED: сила сжатия.");
            lab.RingGeneration.MinimumGap = Slider("Минимальный интервал · м", lab.RingGeneration.MinimumGap, .3f, 1.2f, "Не даёт внешним кольцам слиться.");
            lab.RingGeneration.CompressionStartRing = Mathf.RoundToInt(Slider("Начало сжатия · кольцо", lab.RingGeneration.CompressionStartRing, 4f, 24f, "С какого эшелона COMPRESSED уменьшает gap."));
            GUILayout.Label("СКОРОСТЬ НОВЫХ КОЛЕЦ", section);
            Row(() => Choice("ALTERNATING", lab.RingGeneration.SpeedMode == OrbitalRingSpeedMode.Alternating, () => SetSpeed(OrbitalRingSpeedMode.Alternating), "Чередование направления."),
                () => Choice("OUTER SLOWER", lab.RingGeneration.SpeedMode == OrbitalRingSpeedMode.OuterSlower, () => SetSpeed(OrbitalRingSpeedMode.OuterSlower), "Внешние медленнее."));
            Row(() => Choice("CONSTANT", lab.RingGeneration.SpeedMode == OrbitalRingSpeedMode.Constant, () => SetSpeed(OrbitalRingSpeedMode.Constant), "Одинаковая скорость."),
                () => Choice("GOLDEN RATIO", lab.RingGeneration.SpeedMode == OrbitalRingSpeedMode.GoldenRatio, () => SetSpeed(OrbitalRingSpeedMode.GoldenRatio), "Долго не повторяющиеся фигуры."),
                () => Choice("CONTROLLED CHAOS", lab.RingGeneration.SpeedMode == OrbitalRingSpeedMode.ControlledChaos, () => SetSpeed(OrbitalRingSpeedMode.ControlledChaos), "Воспроизводимо по seed."));
            lab.RingGeneration.ChaosSeed = Mathf.RoundToInt(Slider("Chaos seed", lab.RingGeneration.ChaosSeed, 1f, 9999f, "Один seed всегда даёт одинаковые скорости/фазы."));
            Button("ПЕРЕСЧИТАТЬ ВСЕ КОЛЬЦА", lab.RegenerateRingLayout, "Применить генератор к существующим кольцам.");
        }

        private void DrawUpgrades()
        {
            GUILayout.Label($"УСЛОВНЫЙ УРОВЕНЬ: {lab.LabLevel}", section);
            previewUpgrade = Toggle(previewUpgrade, "PREVIEW UPGRADE EFFECT", "Hover ярко показывает выбранное кольцо и старое → новое значение до применения.");
            Row(() => Button("ПОКАЗАТЬ УЛУЧШЕНИЕ", () => BeginUpgrade(OrbitalRingUpgradeType.Amplifier), "Запускает главный UX-тест выбора прямо на арене."),
                () => Button("СЛУЧАЙНОЕ УЛУЧШЕНИЕ", () => BeginUpgrade((OrbitalRingUpgradeType)Random.Range(0, 7)), "Случайная карточка кольца."),
                () => Button("+ УРОВЕНЬ", () => lab.LabLevel++, "Только тестовый счётчик."));
            Row(() => Button("СКОРОСТЬ +25%", () => BeginUpgrade(OrbitalRingUpgradeType.Overdrive), "Покажет °/с до и после."),
                () => Button("УРОН +25%", () => BeginUpgrade(OrbitalRingUpgradeType.Amplifier), "Все оружия кольца; Link использует среднее двух колец."));
            Row(() => Button("COOLDOWN −15%", () => BeginUpgrade(OrbitalRingUpgradeType.SystemsAcceleration), "Gun, Pusher, Mine и Arc. Контактный cooldown Blade не меняется."),
                () => Button("+1 КРЕПЛЕНИЕ", () => BeginUpgrade(OrbitalRingUpgradeType.ExtraMount), "Добавляет новую видимую точку."));
            Row(() => Button("ОБЛАСТЬ +20%", () => BeginUpgrade(OrbitalRingUpgradeType.EffectField), "AoE, ranges, Arc и толщина эффектов."),
                () => Button("LINK +25%", () => BeginUpgrade(OrbitalRingUpgradeType.ResonantRing), "Точный вклад в Link/Resonance."),
                () => Button("PUSH +30%", () => BeginUpgrade(OrbitalRingUpgradeType.Stabilizer), "Crowd control специализация."));
            lab.RingUpgradeVisuals = Toggle(lab.RingUpgradeVisuals, "RING UPGRADE VISUALS ON/OFF", "Уровень виден через толщину, яркость и короткую вспышку без trails.");
            Row(() => Button("RESET UPGRADES", lab.ResetAllUpgrades, "Сброс Core и всех колец."),
                () => Button("MAX CORE", lab.MaxCore, "Пять ступеней глобальной мощности."));
            Row(() => Button("MAX SELECTED RING", lab.MaxSelectedRing, "По одному уровню каждой специализации."),
                () => Button("MAX STATION", lab.MaxStation, "Усиливает Core и все существующие кольца."));
        }

        private void DrawCore()
        {
            GUILayout.Label($"CORE LEVEL {lab.Core.Level} · импульсов {lab.Stats.CorePulses}", section);
            Row(() => Choice("VISUAL", lab.Core.PulseMode == OrbitalCorePulseMode.Visual, () => lab.Core.PulseMode = OrbitalCorePulseMode.Visual, "Только гипнотическая волна."),
                () => Choice("VOLLEY", lab.Core.PulseMode == OrbitalCorePulseMode.Volley, () => lab.Core.PulseMode = OrbitalCorePulseMode.Volley, "Синхронный залп Pistol."));
            Row(() => Choice("RESONANCE", lab.Core.PulseMode == OrbitalCorePulseMode.Resonance, () => lab.Core.PulseMode = OrbitalCorePulseMode.Resonance, "Кратко усиливает Link."),
                () => Choice("CASCADE", lab.Core.PulseMode == OrbitalCorePulseMode.Cascade, () => lab.Core.PulseMode = OrbitalCorePulseMode.Cascade, "Кольца и оружие срабатывают эшелонами."));
            lab.Core.PulseGameplayEffect = Toggle(lab.Core.PulseGameplayEffect, "PULSE GAMEPLAY EFFECT · БОЙ", "OFF оставляет только волну и charge flash.");
            lab.Core.PulseInterval = Slider("Интервал импульса · сек · ОБА", lab.Core.PulseInterval, .75f, 12f, "Пауза после завершения волны.");
            lab.Core.PulseTravelSpeed = Slider("Скорость волны · м/с · ВИЗУАЛ", lab.Core.PulseTravelSpeed, 2f, 30f, "Определяет задержку между эшелонами.");
            lab.Core.PulseWidth = Slider("Ширина волны · м · ВИЗУАЛ", lab.Core.PulseWidth, .1f, 2f, "Толщина cyan-кольца.");
            lab.Core.PulseBrightness = Slider("Яркость волны · ВИЗУАЛ", lab.Core.PulseBrightness, .1f, 2f, "На бой не влияет.");
            Button("FORCE CORE PULSE", lab.CoreSystem.ForcePulse, "Немедленно начать волну из центра.");
            GUILayout.Label("CORE UPGRADES", section);
            Row(() => Button("НОВОЕ КОЛЬЦО", () => lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.NewRing), "Главный апгрейд видимого роста."),
                () => Button("МОЩНОСТЬ +10%", () => lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.CorePower), "Global Damage всех систем."));
            Row(() => Button("ЧАСТОТА −15%", () => lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.PulseFrequency), "Ускоряет темп каскада."),
                () => Button("МАСШТАБ +10%", () => lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.FieldScale), "Только AoE/beam/link effects."));
            Row(() => Button("СВЯЗУЮЩАЯ МАТРИЦА", () => lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.LinkMatrix), "+2 links, +10% дальность и resonance."),
                () => Button("СТАБИЛИЗАЦИЯ", () => lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.Stabilization), "+4 к Safety Ring Limit."));
        }

        private void DrawQuickTests()
        {
            GUILayout.Label("ЗАПОЛНЕНИЕ", section);
            Row(() => Button("1 OBJECT / RING", () => RequestFill(0f, 1), $"Будет создано {lab.EstimateFill(0f, 1)} объектов."),
                () => Button("2 OBJECTS / RING", () => RequestFill(0f, 2), $"Будет создано {lab.EstimateFill(0f, 2)} объектов."));
            Row(() => Button("FILL 25%", () => RequestFill(.25f, 0), $"Будет создано {lab.EstimateFill(.25f)} объектов."),
                () => Button("FILL 50%", () => RequestFill(.5f, 0), $"Будет создано {lab.EstimateFill(.5f)} объектов."),
                () => Button("FILL 100%", () => RequestFill(1f, 0), $"Будет создано {lab.EstimateFill(1f)} объектов."));
            if (massFillConfirmation)
                Button("ПОДТВЕРДИТЬ ПРЕВЫШЕНИЕ LIMIT", () =>
                {
                    lab.FillStation(pendingFillFraction, pendingFillPerRing, true);
                    massFillConfirmation = false;
                }, "Создать именно запрошенное заполнение сверх Safety Object Limit.");
            Row(() => Button("LINK NETWORK", () => lab.FillTheme(OrbitalMountType.LinkNode, OrbitalMountType.Gun, 2), "Link + редкий Pistol."),
                () => Button("MINE FORTRESS", () => lab.FillTheme(OrbitalMountType.MineLayer, OrbitalMountType.Pusher, 3), "Mine + Pusher."),
                () => Button("ARC CASCADE", () => lab.FillTheme(OrbitalMountType.ArcEmitter, OrbitalMountType.LinkNode, 3), "Arc + проводники."));
            Row(() => Button("RANDOM BALANCED", lab.FillRandomBalanced, "По два объекта на кольцо; все роли распределяются равномерно."),
                () => Button("HYPNOTIC STATION", lab.ApplyHypnoticStation, "16 колец, Golden Ratio, Link + редкие Arc, trails OFF."));
            GUILayout.Label("НОВЫЕ ПРЕСЕТЫ", section);
            Row(() => Button("CORE CASCADE", lab.ApplyCoreCascade, "12 колец, четыре типа оружия, 200 врагов."),
                () => Button("LINK CATHEDRAL", lab.ApplyLinkCathedral, "16 колец, чистая сеть, Full Station."));
            Row(() => Button("MINE PERIMETER", lab.ApplyMinePerimeter, "Мины снаружи, push внутри."),
                () => Button("ARC REACTOR", lab.ApplyArcReactor, "Arc + Link + Core resonance."),
                () => Button("ABSURD STATION", lab.ApplyAbsurdStation, "24 кольца и 300 врагов."));
            GUILayout.Label("SOLO TEST", section);
            Row(() => Button("SOLO LINK", lab.ApplySoloLink, "Только Link и несколько тестовых врагов."),
                () => Button("SOLO RESONANCE", lab.ApplySoloResonance, "Синхронные кольца для понятного alignment."),
                () => Button("SOLO CORE PULSE", lab.ApplySoloCorePulse, "Восемь эшелонов одного каскада."));
            Row(() => Button("SOLO MINE", lab.ApplySoloMine, "Четыре радиуса минного рисунка."),
                () => Button("SOLO ARC", lab.ApplySoloArc, "Короткие вспышки и один проводник."),
                () => Button("ВОССТАНОВИТЬ СТАНЦИЮ", lab.ApplyCoreCascade, "Вернуть демонстрационную конфигурацию Core Cascade."));
            GUILayout.Label("GROWTH TIMELINE", section);
            Row(() => Button("MIN 1", () => lab.ApplyGrowthStage(0), "1 ring"),
                () => Button("MIN 3", () => lab.ApplyGrowthStage(1), "3 rings"),
                () => Button("MIN 6", () => lab.ApplyGrowthStage(2), "6 rings"),
                () => Button("MIN 10", () => lab.ApplyGrowthStage(3), "10 rings"),
                () => Button("MIN 15", () => lab.ApplyGrowthStage(4), "16 rings"),
                () => Button("EXTREME", () => lab.ApplyGrowthStage(5), "24 rings"));
            GUILayout.Label("КАМЕРА", section);
            Row(() => Choice("FULL STATION", lab.CameraRig.Mode == OrbitalCameraMode.FullStation, () => lab.CameraRig.Mode = OrbitalCameraMode.FullStation, "Вмещает весь силуэт."),
                () => Choice("COMBAT FOCUS", lab.CameraRig.Mode == OrbitalCameraMode.CombatFocus, () => lab.CameraRig.Mode = OrbitalCameraMode.CombatFocus, "Ограничивает отдаление; Tab временно показывает всё."));
            lab.CameraRig.MaximumAutoCameraSize = Slider("Максимальное отдаление Combat Focus · м", lab.CameraRig.MaximumAutoCameraSize, 5f, 40f, "Предел размера камеры в боевом режиме; Tab временно игнорирует его.");
            lab.CameraRig.MinimumPlayerScreenSize = Slider("Минимальный размер игрока · доля экрана", lab.CameraRig.MinimumPlayerScreenSize, .01f, .08f, "Нижний предел читаемости игрока при автоматическом отдалении.");
            lab.CameraRig.OuterRingMargin = Slider("Outer Ring Margin · м", lab.CameraRig.OuterRingMargin, .2f, 4f, "Отступ Full Station от внешнего кольца.");
            lab.CameraRig.SmoothTime = Slider("Плавность камеры · сек", lab.CameraRig.SmoothTime, .05f, 1.5f, "Время сглаживания перехода Full/Combat и удержания Tab.");
        }

        private void SetSpacing(OrbitalRingSpacingMode mode) { lab.RingGeneration.SpacingMode = mode; lab.RegenerateRingLayout(); }
        private void SetSpeed(OrbitalRingSpeedMode mode) { lab.RingGeneration.SpeedMode = mode; lab.RegenerateRingLayout(); }

        private void RequestFill(float fraction, int fixedPerRing)
        {
            pendingFillFraction = fraction;
            pendingFillPerRing = fixedPerRing;
            massFillConfirmation = !lab.FillStation(fraction, fixedPerRing, false);
        }

        private void ResetMineParameters()
        {
            lab.Mines.Damage = 24f;
            lab.Mines.DropInterval = 1.25f;
            lab.Mines.TriggerRadius = .72f;
            lab.Mines.ExplosionRadius = 1.55f;
            lab.Mines.Lifetime = 10f;
            lab.Mines.MaximumActivePerLayer = 6;
            lab.Mines.PushForce = 5f;
        }

        private void ResetArcParameters()
        {
            lab.Arc.Damage = 13f;
            lab.Arc.Cooldown = .9f;
            lab.Arc.Range = 5.5f;
            lab.Arc.ChainCount = 3;
            lab.Arc.ChainRange = 2.4f;
            lab.Arc.LinkConduction = true;
            lab.Arc.PulseBonus = 1.75f;
        }

        private void ResetLinkParameters()
        {
            lab.Links.Damage = 8f;
            lab.Links.HitCooldown = .35f;
            lab.Links.LineWidth = .055f;
            lab.Links.MaxDistance = 9f;
            lab.Links.PulseSpeed = 3f;
            lab.Links.LineColor = new Color(1f, .06f, .84f, 1f);
        }

        private void ResetResonanceParameters()
        {
            lab.Resonance.AlignmentTolerance = 10f;
            lab.Resonance.MinimumObjects = 2;
            lab.Resonance.Cooldown = 1.15f;
            lab.Resonance.Damage = 16f;
            lab.Resonance.Range = 9f;
        }

        private void BeginUpgrade(OrbitalRingUpgradeType type)
        {
            if (lab.RingCount == 0) return;
            pendingUpgrade = type;
            upgradeSelection = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = .06f;
            lab.RingEditMode = false;
            lab.Notify("Выберите кольцо прямо на арене. ЛКМ применить · ПКМ/Esc отменить · ←/→ выбрать · колесо zoom", 8f);
        }

        private void HandleUpgradeSelection()
        {
            for (int i = 0; i < lab.RingCount; i++) lab.Rings[i].PreviewRotationMultiplier = 1f;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                FinishUpgradeSelection();
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) lab.SelectedRing = (lab.SelectedRing - 1 + lab.RingCount) % lab.RingCount;
            if (Input.GetKeyDown(KeyCode.RightArrow)) lab.SelectedRing = (lab.SelectedRing + 1) % lab.RingCount;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f) lab.CameraRig.Zoom(wheel * 1.2f);
            if (Camera.main != null && !PointerOverMenu)
            {
                HoveredRing = FindNearestRing(MouseWorld(), Mathf.Max(.35f, lab.OuterRingRadius * .018f));
                if (HoveredRing >= 0)
                {
                    lab.SelectedRing = HoveredRing;
                    if (previewUpgrade)
                    {
                        lab.Rings[HoveredRing].FlashUpgrade(.06f);
                        if (pendingUpgrade == OrbitalRingUpgradeType.Overdrive)
                            lab.Rings[HoveredRing].PreviewRotationMultiplier = 1.25f;
                    }
                    if (Input.GetMouseButtonDown(0))
                    {
                        lab.ApplyRingUpgrade(HoveredRing, pendingUpgrade);
                        FinishUpgradeSelection();
                    }
                }
            }
        }

        private void FinishUpgradeSelection()
        {
            upgradeSelection = false;
            HoveredRing = -1;
            for (int i = 0; i < lab.RingCount; i++) lab.Rings[i].PreviewRotationMultiplier = 1f;
            Time.timeScale = savedTimeScale;
        }

        private void DrawUpgradeOverlay()
        {
            int index = Mathf.Clamp(HoveredRing >= 0 ? HoveredRing : lab.SelectedRing, 0, lab.RingCount - 1);
            OrbitalRing ring = lab.Rings[index];
            int objects = 0;
            string roles = "";
            for (int i = 0; i < ring.Mounts.Length; i++)
            {
                if (ring.Mounts[i] == null) continue;
                if (objects < 4) roles += (objects == 0 ? "" : ", ") + ring.Mounts[i].Type;
                objects++;
            }
            string text = $"{OrbitalCombatLabController.RingUpgradeName(pendingUpgrade)}\n" +
                $"КОЛЬЦО {index + 1} · уровень {RomanLevel(ring.Upgrades.Level)} · объектов {objects}: {(objects == 0 ? "пусто" : roles)}\n" +
                $"{lab.DescribeRingUpgrade(index, pendingUpgrade)}\nЛКМ — применить · ПКМ/Esc — отменить · ←/→ — кольцо · колесо — zoom";
            GUI.Box(new Rect(Screen.width * .5f - 310f, Screen.height - 118f, 620f, 104f), text, stat);
        }

        private void DrawStats()
        {
            OrbitalRing selected = lab.RingCount > 0 ? SelectedRing() : null;
            string ring = selected == null ? "—" : $"#{lab.SelectedRing + 1} · {selected.EffectiveRotationSpeed:0.#}°/с · " +
                $"LV {selected.Upgrades.Level} · DMG ×{selected.Upgrades.DamageMultiplier:0.##} · CD ×{selected.Upgrades.CooldownMultiplier:0.##}";
            string text = $"FPS ~ {lab.Stats.SmoothedFps:0}\nEnemies {lab.Stats.ActiveEnemies}/{lab.Crowd.DesiredCount}\n" +
                $"Rings {lab.RingCount} · Mounts {lab.MountedCount} · Links {lab.Stats.ActiveLinks}\n" +
                $"Mines {lab.Stats.ActiveMines} · Arc {lab.Stats.ArcDischarges}/{lab.Stats.ArcHits} hits · Core {lab.Stats.CorePulses}\n" +
                $"Resonances {lab.Stats.Resonances} ({lab.Stats.LastResonance})\n" +
                $"Camera {lab.CameraRig.CurrentSize:0.0} · object ~{lab.CameraRig.ApproximateObjectScreenSize * 100f:0.0}% screen\n" +
                $"Movement {lab.CurrentMovementPreset} · {lab.CameraRig.Mode}\n{ring}";
            GUI.Box(new Rect(Screen.width - 380f, 12f, 368f, 174f), text, stat);
        }

        private void DrawRingEditOverlay()
        {
            OrbitalRing ring = SelectedRing();
            Vector2 anchorWorld = ring.GetPositionForAngle(lab.PlayerPosition, ring.FormationAngle + 35f);
            Vector3 screen = Camera.main != null ? Camera.main.WorldToScreenPoint(anchorWorld) : Vector3.zero;
            if (screen.z < 0f) return;
            float x = Mathf.Clamp(screen.x - 116f, PanelRect.xMax + 8f, Screen.width - 244f);
            float y = Mathf.Clamp(Screen.height - screen.y - 19f, 174f, Screen.height - 46f);
            string label = $"КОЛЬЦО {lab.SelectedRing + 1} · LV {RomanLevel(ring.Upgrades.Level)} | ФАЗА {ring.PhaseOffset:0}° | Q/E";
            GUI.Box(new Rect(x, y, 236f, 32f), label, stat);
        }

        private void HandlePhaseKey(KeyCode key, float direction, ref float pressedAt)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(key))
            {
                pressedAt = Time.unscaledTime;
                float step = control ? 45f : shift ? 3f : 15f;
                lab.NudgeSelectedPhase(direction * step);
            }
            if (!Input.GetKey(key)) return;
            if (Time.unscaledTime - pressedAt < .3f) return;
            float speed = control ? 90f : shift ? 16f : 54f;
            lab.NudgeSelectedPhase(direction * speed * Time.unscaledDeltaTime);
        }

        private OrbitalRing SelectedRing() => lab.Rings[Mathf.Clamp(lab.SelectedRing, 0, lab.RingCount - 1)];
        private int FindNearestRing(Vector2 world, float threshold)
        {
            int best = -1; float distance = threshold;
            for (int i = 0; i < lab.RingCount; i++)
            {
                float value = lab.Rings[i].DistanceToPath(lab.PlayerPosition, world);
                if (value >= distance) continue;
                distance = value; best = i;
            }
            return best;
        }
        private static Vector2 MouseWorld()
        {
            Vector3 screen = Input.mousePosition; screen.z = -Camera.main.transform.position.z;
            return Camera.main.ScreenToWorldPoint(screen);
        }
        private bool Fold(int index, string label)
        {
            GUILayout.Space(5f);
            GUI.backgroundColor = open[index] ? new Color(.16f, .55f, .62f) : new Color(.22f, .25f, .28f);
            if (GUILayout.Button((open[index] ? "▼ " : "▶ ") + label, GUILayout.Height(25f))) open[index] = !open[index];
            GUI.backgroundColor = Color.white;
            return open[index];
        }
        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(.3f, .95f, 1f);
            section = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            section.normal.textColor = new Color(.3f, .95f, 1f);
            hint = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            hint.normal.textColor = new Color(.72f, .78f, .82f);
            stat = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 8, 8, 8) };
            stat.normal.textColor = new Color(.82f, 1f, 1f);
        }
        private static bool Toggle(bool value, string label, string tooltip) => GUILayout.Toggle(value, new GUIContent(label, tooltip));
        private static float Slider(string label, float value, float min, float max, string tooltip)
        {
            GUILayout.Label(new GUIContent($"{label}: {value:0.##}", tooltip));
            return GUILayout.HorizontalSlider(value, min, max);
        }
        private static void Button(string label, System.Action action, string tooltip)
        {
            if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(27f))) action?.Invoke();
        }
        private static void Button(string label, System.Func<bool> action, string tooltip)
        {
            if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(27f))) action?.Invoke();
        }
        private static void Choice(string label, bool active, System.Action action, string tooltip)
        {
            GUI.backgroundColor = active ? new Color(.25f, .9f, 1f) : Color.white;
            Button(label, action, tooltip); GUI.backgroundColor = Color.white;
        }
        private static void Row(params System.Action[] cells)
        {
            GUILayout.BeginHorizontal(); for (int i = 0; i < cells.Length; i++) cells[i](); GUILayout.EndHorizontal();
        }
        private static string RomanLevel(int level)
        {
            if (level <= 0) return "0";
            if (level == 1) return "I";
            if (level == 2) return "II";
            if (level == 3) return "III";
            if (level == 4) return "IV";
            return "V+";
        }
    }
}
