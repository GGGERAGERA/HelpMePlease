using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalLabDebugUI : MonoBehaviour
    {
        public bool MenuOpen { get; private set; } = true;
        public int HoveredRing { get; private set; } = -1;
        public bool PointerOverMenu
        {
            get
            {
                if (!MenuOpen) return false;
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                return PanelRect.Contains(mouse);
            }
        }

        private Rect PanelRect => new(12f, 12f, Mathf.Min(470f, Screen.width - 24f), Screen.height - 24f);
        private readonly bool[] open = { true, true, true, true, true, true, false, false, false, false, true, false };
        private OrbitalCombatLabController lab;
        private Vector2 scroll;
        private GUIStyle title, section, hint, stat;
        private float qPressedAt = -99f;
        private float ePressedAt = -99f;

        public void Configure(OrbitalCombatLabController controller) => lab = controller;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) MenuOpen = !MenuOpen;
            HoveredRing = -1;
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
            if (lab == null) return;
            EnsureStyles();
            if (MenuOpen) DrawMenu(); else GUI.Box(new Rect(12f, 12f, 322f, 31f), "F1 → ORBITAL COMBAT LAB", hint);
            if (lab.ShowStats) DrawStats();
            if (MenuOpen && lab.RingEditMode && lab.RingCount > 0) DrawRingEditOverlay();
        }

        private void DrawMenu()
        {
            Rect panel = PanelRect;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 16f));
            GUILayout.Label("ORBITAL COMBAT LAB", title);
            GUILayout.Label("F1 — меню · WASD — движение · ЛКМ — drag · Ring Edit: Q/E, R, Space, колесо", hint);
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
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Label("ⓘ " + GUI.tooltip, hint);
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
            Row(() => Button("+ RING", lab.AddRing, "Добавить кольцо, максимум шесть."),
                () => Button("- RING", lab.RemoveRing, "Удалить внешнее кольцо вместе с объектами."));
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
            GUILayout.BeginHorizontal();
            for (int i = 0; i < lab.RingCount; i++)
            {
                GUI.backgroundColor = i == lab.SelectedRing ? new Color(.2f, .9f, 1f) : Color.white;
                if (GUILayout.Button((i + 1).ToString(), GUILayout.Height(25f))) lab.SelectedRing = i;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            OrbitalRing ring = SelectedRing();
            OrbitalRingSettings s = ring.Settings;
            s.Radius = Slider("Radius", s.Radius, .8f, 11f, "Базовый размер орбиты.");
            s.RotationSpeed = Slider("Rotation Speed", s.RotationSpeed, 0f, 220f, "Угловая скорость рисунка.");
            s.Clockwise = Toggle(s.Clockwise, "Reverse Direction / Clockwise", "Разворачивает поток объектов.");
            s.Paused = Toggle(s.Paused, "Pause Ring", "Замораживает только это кольцо, сохраняя фазу.");
            GUILayout.Label($"Ring Rotation Angle: {ring.RotationAngle:0.0}°", hint);
            ring.PhaseOffset = Slider("Ring Phase Offset", ring.PhaseOffset, 0f, 360f,
                "Постоянное пользовательское смещение формации. Вращающийся угол продолжает жить отдельно.");
            int mounts = Mathf.RoundToInt(Slider("Max Mounts", s.MaxMounts, 1f, 8f, "Количество креплений."));
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
            Button("FILL ALL RINGS", lab.FillAllRings, "Заполнить свободные точки базовыми объектами.");
            lab.ShowAttackRanges = Toggle(lab.ShowAttackRanges, "Радиусы атак", "Рабочие зоны Gun и Pusher.");
            lab.Gun.Damage = Slider("Gun Damage", lab.Gun.Damage, 1f, 60f, "Урон projectile.");
            lab.Blade.Damage = Slider("Blade Damage", lab.Blade.Damage, 1f, 90f, "Контактный урон.");
            lab.Pusher.PushForce = Slider("Pusher Force", lab.Pusher.PushForce, 1f, 35f, "Сила раздвигания толпы.");
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
            lab.Links.ShowLinks = Toggle(lab.Links.ShowLinks, "Show Links", "Показывать энергетические сегменты.");
            lab.Links.DealDamage = Toggle(lab.Links.DealDamage, "Links Deal Damage", "Пересечение становится оружием при PATTERN COMBAT ON.");
            lab.Links.Damage = Slider("Link Damage", lab.Links.Damage, 0f, 40f, "Урон пересечения с cooldown.");
            lab.Links.HitCooldown = Slider("Hit Cooldown", lab.Links.HitCooldown, .08f, 1.5f, "Исключает урон каждый кадр.");
            lab.Links.LineWidth = Slider("Line Width", lab.Links.LineWidth, .015f, .22f, "Толщина хорд.");
            lab.Links.MaxDistance = Slider("Max Link Distance", lab.Links.MaxDistance, 2f, 18f, "Ограничивает ALL NEARBY и визуальную кашу.");
            lab.Links.PulseSpeed = Slider("Pulse Speed", lab.Links.PulseSpeed, 0f, 10f, "Скорость дыхания линий.");
            Color linkColor = lab.Links.LineColor;
            linkColor.r = Slider("Link Color R", linkColor.r, 0f, 1f, "Красный канал цвета энергетической геометрии.");
            linkColor.g = Slider("Link Color G", linkColor.g, 0f, 1f, "Зелёный канал: поднимите для cyan/white рисунка.");
            linkColor.b = Slider("Link Color B", linkColor.b, 0f, 1f, "Синий канал: вместе с красным создаёт magenta.");
            linkColor.a = 1f;
            lab.Links.LineColor = linkColor;
        }

        private void DrawResonance()
        {
            lab.Resonance.Enabled = Toggle(lab.Resonance.Enabled, "Resonance ON", "Ищет выстраивания объектов разных колец.");
            lab.Resonance.VisualOnly = Toggle(lab.Resonance.VisualOnly, "VISUAL ONLY", "Показывает событие без влияния на баланс.");
            Row(() => Choice("VOLLEY", lab.Resonance.Mode == OrbitalResonanceMode.RadialVolley, () => lab.Resonance.Mode = OrbitalResonanceMode.RadialVolley, "Усиленный радиальный залп."),
                () => Choice("BEAM", lab.Resonance.Mode == OrbitalResonanceMode.Beam, () => lab.Resonance.Mode = OrbitalResonanceMode.Beam, "Короткий луч."),
                () => Choice("SHOCK", lab.Resonance.Mode == OrbitalResonanceMode.Shockwave, () => lab.Resonance.Mode = OrbitalResonanceMode.Shockwave, "Ударная волна."),
                () => Choice("CYCLE", lab.Resonance.Mode == OrbitalResonanceMode.Cycle, () => lab.Resonance.Mode = OrbitalResonanceMode.Cycle, "Чередование режимов."));
            lab.Resonance.AlignmentTolerance = Slider("Допуск резонанса", lab.Resonance.AlignmentTolerance, 2f, 35f,
                "Насколько точно объекты должны выстроиться. Чем больше значение, тем чаще резонансы.");
            lab.Resonance.MinimumObjects = Mathf.RoundToInt(Slider("Minimum Objects", lab.Resonance.MinimumObjects, 2f, 4f, "Сколько разных колец должны совпасть."));
            lab.Resonance.Cooldown = Slider("Cooldown", lab.Resonance.Cooldown, .25f, 4f, "Пауза между отдельными событиями.");
            lab.Resonance.Damage = Slider("Damage", lab.Resonance.Damage, 0f, 60f, "Сила геометрической атаки.");
            lab.Resonance.Range = Slider("Range", lab.Resonance.Range, 3f, 18f, "Длина beam/volley и масштаб shockwave.");
        }

        private void DrawTrails()
        {
            Row(() => Choice("OFF", lab.Trails.Mode == OrbitalTrailMode.Off, () => lab.Trails.Mode = OrbitalTrailMode.Off, "Без следов."),
                () => Choice("SHORT", lab.Trails.Mode == OrbitalTrailMode.Short, () => lab.Trails.Mode = OrbitalTrailMode.Short, "Короткая дуга."),
                () => Choice("MEDIUM", lab.Trails.Mode == OrbitalTrailMode.Medium, () => lab.Trails.Mode = OrbitalTrailMode.Medium, "Заметный рисунок."),
                () => Choice("HYPNOTIC", lab.Trails.Mode == OrbitalTrailMode.Hypnotic, () => lab.Trails.Mode = OrbitalTrailMode.Hypnotic, "Длинная кинетическая скульптура."));
            lab.Trails.Length = Slider("Length", lab.Trails.Length, .15f, 2.5f, "Длина цветной дуги.");
            lab.Trails.Width = Slider("Width", lab.Trails.Width, .015f, .24f, "Толщина следа.");
            lab.Trails.Alpha = Slider("Alpha", lab.Trails.Alpha, .05f, 1f, "Прозрачность до visual profile.");
            lab.Trails.FollowVisualProfile = Toggle(lab.Trails.FollowVisualProfile, "TRAILS FOLLOW VISUAL PROFILE",
                "CLEAN/COMBAT выключают следы, HYPNOTIC/MAXIMUM включают длинные. Отключите для ручной настройки.");
        }

        private void DrawFormations()
        {
            lab.FreeMountPhase = Toggle(lab.FreeMountPhase, "FREE MOUNT PHASE", "Drag вдоль текущего кольца меняет собственный угол; красный preview означает наложение.");
            lab.MinimumMountSpacing = Slider("Minimum Angular Spacing", lab.MinimumMountSpacing, 4f, 35f, "Минимальная угловая дистанция.");
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
                s.AspectRatio = Slider("Aspect Ratio", s.AspectRatio, .55f, 2.3f, "Сила вытяжки.");
                s.ShapeRotation = Slider("Rotation Angle", s.ShapeRotation, 0f, 360f, "Ориентация длинной оси.");
            }
            else if (s.Shape == OrbitalShape.Breathing)
            {
                s.BreathingAmplitude = Slider("Amplitude", s.BreathingAmplitude, 0f, 1.5f, "Амплитуда дыхания.");
                s.BreathingFrequency = Slider("Frequency", s.BreathingFrequency, .05f, 2f, "Частота дыхания.");
                s.BreathingPhase = Slider("Phase Offset", s.BreathingPhase, 0f, 360f, "Сдвиг относительно соседей.");
            }
            else if (s.Shape == OrbitalShape.Wobble)
            {
                s.WobbleLobes = Mathf.RoundToInt(Slider("Lobes", s.WobbleLobes, 2f, 10f, "Количество лепестков."));
                s.WobbleAmplitude = Slider("Amplitude", s.WobbleAmplitude, 0f, .9f, "Глубина волн." );
                s.WobbleSpeed = Slider("Speed", s.WobbleSpeed, 0f, 8f, "Скорость искажения.");
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
            s.FieldWidth = Slider("Field Width", s.FieldWidth, .05f, .8f, "Толщина зоны.");
            s.FieldDamage = Slider("Damage", s.FieldDamage, 0f, 30f, "Урон CUT/CONDUCTOR.");
            s.SlowMultiplier = Slider("Slow Multiplier", s.SlowMultiplier, .1f, 1f, "Меньше — сильнее slow.");
            s.FieldPushForce = Slider("Push Force", s.FieldPushForce, 0f, 20f, "Сила PULSE.");
            s.PulseInterval = Slider("Pulse Interval", s.PulseInterval, .2f, 5f, "Интервал импульсов.");
            s.FieldTargetCooldown = Slider("Target Cooldown", s.FieldTargetCooldown, .08f, 1.5f, "Защита от урона каждый кадр.");
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

        private void DrawStats()
        {
            OrbitalRing selected = lab.RingCount > 0 ? SelectedRing() : null;
            string ring = selected == null ? "—" : $"#{lab.SelectedRing + 1} · {selected.Settings.RotationSpeed:0.#}°/с · " +
                $"{(selected.Settings.Clockwise ? "CW" : "CCW")} · rot {selected.RotationAngle:0}° · phase {selected.PhaseOffset:0}° · {selected.Settings.Shape}";
            string text = $"FPS ~ {lab.Stats.SmoothedFps:0}\nEnemies {lab.Stats.ActiveEnemies}/{lab.Crowd.DesiredCount}\n" +
                $"Rings {lab.RingCount} · Mounts {lab.MountedCount} · Links {lab.Stats.ActiveLinks}\n" +
                $"Resonances {lab.Stats.Resonances} ({lab.Stats.LastResonance})\n" +
                $"Link Hits {lab.Stats.LinkHits} · Field Hits {lab.Stats.RingFieldHits}\n" +
                $"Movement {lab.CurrentMovementPreset} · Visual {lab.WeaponVisuals.Mode}\n{ring}";
            GUI.Box(new Rect(Screen.width - 360f, 12f, 348f, 153f), text, stat);
        }

        private void DrawRingEditOverlay()
        {
            OrbitalRing ring = SelectedRing();
            Vector2 anchorWorld = ring.GetPositionForAngle(lab.PlayerPosition, ring.FormationAngle + 35f);
            Vector3 screen = Camera.main != null ? Camera.main.WorldToScreenPoint(anchorWorld) : Vector3.zero;
            if (screen.z < 0f) return;
            float x = Mathf.Clamp(screen.x - 116f, PanelRect.xMax + 8f, Screen.width - 244f);
            float y = Mathf.Clamp(Screen.height - screen.y - 19f, 174f, Screen.height - 46f);
            string label = $"КОЛЬЦО {lab.SelectedRing + 1} | ФАЗА {ring.PhaseOffset:0}° | Q/E";
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
    }
}
