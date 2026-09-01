using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalLabDebugUI : MonoBehaviour
    {
        public bool MenuOpen { get; private set; } = true;
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

        private Rect PanelRect => new(12f, 12f, Mathf.Min(450f, Screen.width - 24f), Screen.height - 24f);
        private OrbitalCombatLabController lab;
        private Vector2 scroll;
        private GUIStyle title;
        private GUIStyle section;
        private GUIStyle hint;
        private GUIStyle stat;

        public void Configure(OrbitalCombatLabController controller) => lab = controller;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) MenuOpen = !MenuOpen;
        }

        private void OnGUI()
        {
            if (lab == null) return;
            EnsureStyles();
            if (MenuOpen) DrawMenu();
            else DrawClosedHint();
            if (lab.ShowStats) DrawStats();
        }

        private void DrawMenu()
        {
            Rect panel = PanelRect;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 16f));
            GUILayout.Label("ORBITAL COMBAT LAB", title);
            GUILayout.Label("F1 — скрыть меню · WASD — движение · ЛКМ — перетаскивание", hint);
            scroll = GUILayout.BeginScrollView(scroll);

            DrawSection("БЫСТРЫЙ ТЕСТ");
            Row(() => Button("START TEST", lab.ApplyStartState, "Запустить начальное состояние."),
                () => Button("RESET TEST", lab.ResetTest, "Полностью вернуть стенд к START STATE."));
            Row(() => Button("START STATE", lab.ApplyStartState, "1 кольцо, 1 Gun, 50 врагов."),
                () => Button("MID STATE", lab.ApplyMidState, "3 кольца, все три роли, 120 врагов."),
                () => Button("FINAL STATION", lab.ApplyFinalState, "6 колец, 35 объектов, 300 врагов."));

            DrawSection("КОЛЬЦА И ОБЪЕКТЫ");
            Row(() => Button("+ RING", lab.AddRing, "Добавить кольцо, максимум 6."),
                () => Button("- RING", lab.RemoveRing, "Удалить внешнее кольцо и его объекты."));
            Row(() => Button("+ GUN", () => lab.AddMounted(OrbitalMountType.Gun), "Добавить пушку на активное кольцо."),
                () => Button("+ BLADE", () => lab.AddMounted(OrbitalMountType.Blade), "Добавить лезвие на активное кольцо."),
                () => Button("+ PUSHER", () => lab.AddMounted(OrbitalMountType.Pusher), "Добавить толкатель на активное кольцо."));
            Row(() => Button("CLEAR OBJECTS", lab.ClearMounted, "Удалить все боевые объекты."),
                () => Button("FILL ALL RINGS", lab.FillAllRings, "Заполнить свободные крепления всеми тремя ролями."));

            DrawSection("ПЛОТНОСТЬ ТОЛПЫ");
            Row(() => Button("SPAWN 50", () => lab.SpawnEnemies(50), "Установить 50 врагов."),
                () => Button("SPAWN 100", () => lab.SpawnEnemies(100), "Установить 100 врагов."),
                () => Button("SPAWN 200", () => lab.SpawnEnemies(200), "Установить 200 врагов."),
                () => Button("SPAWN 300", () => lab.SpawnEnemies(300), "Установить 300 врагов."));

            DrawSection("ПЕРЕКЛЮЧАТЕЛИ");
            lab.ShowRings = Toggle(lab.ShowRings, "Показать кольца", "Показать линии активных орбит.");
            lab.ShowMounts = Toggle(lab.ShowMounts, "Показать монтажные точки", "Серые — свободны, красные — заняты.");
            lab.PlayerImmortal = Toggle(lab.PlayerImmortal, "Бессмертие игрока", "По умолчанию включено для исследования fantasy.");
            lab.Crowd.DamagePlayer = Toggle(lab.Crowd.DamagePlayer, "Враги наносят урон игроку", "Работает только при выключенном бессмертии.");
            lab.RingContactDamage = Toggle(lab.RingContactDamage, "Урон от самих колец", "Кольца наносят настроенный контактный урон.");
            lab.RingContactPush = Toggle(lab.RingContactPush, "Отталкивание от колец", "Кольца слегка отталкивают пересекающих их врагов.");
            lab.CameraRig.AutoCamera = Toggle(lab.CameraRig.AutoCamera, "Auto Camera", "Масштаб зависит от радиуса внешнего кольца.");
            lab.SlowDuringDrag = Toggle(lab.SlowDuringDrag, "Замедлять время при перетаскивании", "Во время drag Time.timeScale = 0.2 и гарантированно восстанавливается.");
            lab.ShowAttackRanges = Toggle(lab.ShowAttackRanges, "Показывать радиусы атак", "Показать Range у Gun и Push Radius у Pusher.");
            lab.ShowStats = Toggle(lab.ShowStats, "Показывать простую статистику", "FPS, враги, убийства и срабатывания.");
            lab.CameraImpulse = Toggle(lab.CameraImpulse, "Camera impulse от массового push", "Короткий мягкий толчок камеры при сильном Pusher.");

            DrawRingEditor();
            DrawWeaponEditor();
            DrawCameraEditor();

            GUILayout.Space(8f);
            GUILayout.Label("Подсказка: зажмите ЛКМ на цветном объекте, наведите его на зелёную орбиту и отпустите. Если свободной точки нет, объект вернётся назад. Esc/ПКМ отменяют drag.", hint);
            if (!string.IsNullOrEmpty(GUI.tooltip))
                GUILayout.Label("ⓘ " + GUI.tooltip, hint);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRingEditor()
        {
            DrawSection("АКТИВНОЕ КОЛЬЦО");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < lab.RingCount; i++)
            {
                GUI.backgroundColor = i == lab.SelectedRing ? new Color(.2f, .9f, 1f) : Color.white;
                if (GUILayout.Button((i + 1).ToString(), GUILayout.Height(25f))) lab.SelectedRing = i;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            if (lab.RingCount == 0) return;
            OrbitalRingSettings ring = lab.Rings[Mathf.Clamp(lab.SelectedRing, 0, lab.RingCount - 1)].Settings;
            ring.Radius = Slider("Radius", ring.Radius, .8f, 10.5f, "Радиус применяется сразу.");
            ring.RotationSpeed = Slider("Rotation Speed", ring.RotationSpeed, 0f, 180f, "Градусов в секунду.");
            ring.Clockwise = Toggle(ring.Clockwise, "Direction: Clockwise", "Выключено — CounterClockwise.");
            int mounts = Mathf.RoundToInt(Slider("Max Mounts", ring.MaxMounts, 1f, 8f, "Число доступных монтажных точек."));
            if (mounts != ring.MaxMounts) lab.SetSelectedRingMaxMounts(mounts);
            ring.ContactDamage = Slider("Ring Contact Damage", ring.ContactDamage, 0f, 30f, "Урон при пересечении линии кольца.");
            ring.ContactPush = Slider("Ring Push Force", ring.ContactPush, 0f, 15f, "Отталкивание от линии кольца.");
            ring.LineWidth = Slider("Толщина линии", ring.LineWidth, .015f, .16f, "Только визуальная толщина.");
            ring.Visible = Toggle(ring.Visible, "Отображение кольца", "Локальная видимость выбранного кольца.");
        }

        private void DrawWeaponEditor()
        {
            DrawSection("GUN — ГОЛУБОЙ");
            lab.Gun.Damage = Slider("Damage", lab.Gun.Damage, 1f, 50f, "Урон одного projectile.");
            lab.Gun.FireRate = Slider("Fire Rate", lab.Gun.FireRate, .2f, 10f, "Выстрелов в секунду.");
            lab.Gun.Range = Slider("Range", lab.Gun.Range, 2f, 16f, "Радиус поиска ближайшего врага.");
            lab.Gun.ProjectileSpeed = Slider("Projectile Speed", lab.Gun.ProjectileSpeed, 5f, 35f, "Скорость голубой точки.");

            DrawSection("BLADE — КРАСНЫЙ");
            lab.Blade.Damage = Slider("Damage", lab.Blade.Damage, 1f, 80f, "Контактный урон.");
            lab.Blade.HitCooldown = Slider("Hit Cooldown", lab.Blade.HitCooldown, .05f, 1.2f, "Cooldown отдельно для каждой цели.");
            lab.Blade.Size = Slider("Size", lab.Blade.Size, .55f, 2.4f, "Длина и зона контакта лезвия.");

            DrawSection("PUSHER — ЖЁЛТЫЙ");
            lab.Pusher.PushForce = Slider("Push Force", lab.Pusher.PushForce, 1f, 35f, "Сила резкого отталкивания.");
            lab.Pusher.PushRadius = Slider("Push Radius", lab.Pusher.PushRadius, .5f, 3.2f, "Радиус импульса.");
            lab.Pusher.Cooldown = Slider("Cooldown", lab.Pusher.Cooldown, .1f, 2f, "Задержка между импульсами.");
        }

        private void DrawCameraEditor()
        {
            DrawSection("КАМЕРА");
            lab.CameraRig.ManualSize = Slider("Ручной масштаб", lab.CameraRig.ManualSize, 4f, 18f, "Используется при Auto Camera OFF.");
            lab.CameraRig.EdgePadding = Slider("Запас по краям", lab.CameraRig.EdgePadding, 0f, 4f, "Дополнительное поле вокруг внешнего кольца.");
            lab.CameraRig.RadiusMultiplier = Slider("Множитель радиуса", lab.CameraRig.RadiusMultiplier, .45f, 1.25f, "Влияние внешнего радиуса на масштаб.");
        }

        private void DrawStats()
        {
            string text = $"FPS ~ {lab.Stats.SmoothedFps:0}\n" +
                $"Враги {lab.Stats.ActiveEnemies}/{lab.Crowd.DesiredCount}\n" +
                $"Кольца {lab.RingCount} · Объекты {lab.MountedCount}\n" +
                $"Kills {lab.Stats.Kills} · Shots {lab.Stats.Shots}\n" +
                $"Blade {lab.Stats.BladeHits} · Push {lab.Stats.PushHits}\n" +
                $"HP игрока {lab.PlayerHp:0}";
            float x = Screen.width - 210f;
            GUI.Box(new Rect(x, 12f, 198f, 126f), text, stat);
        }

        private void DrawClosedHint()
        {
            GUI.Box(new Rect(12f, 12f, 322f, 31f), "F1 → ORBITAL COMBAT LAB", hint);
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(.3f, .95f, 1f);
            section = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            section.normal.textColor = new Color(.3f, .95f, 1f);
            hint = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            hint.normal.textColor = new Color(.72f, .78f, .82f);
            stat = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 8, 8, 8) };
            stat.normal.textColor = new Color(.82f, 1f, 1f);
        }

        private void DrawSection(string label)
        {
            GUILayout.Space(8f);
            GUILayout.Label(label, section);
        }

        private static bool Toggle(bool value, string label, string tooltip) =>
            GUILayout.Toggle(value, new GUIContent(label, tooltip));

        private static float Slider(string label, float value, float min, float max, string tooltip)
        {
            GUILayout.Label(new GUIContent($"{label}: {value:0.##}", tooltip));
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private static void Button(string label, System.Action action, string tooltip)
        {
            if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(28f))) action?.Invoke();
        }

        private static void Button(string label, System.Func<bool> action, string tooltip)
        {
            if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(28f))) action?.Invoke();
        }

        private static void Row(params System.Action[] cells)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < cells.Length; i++) cells[i]();
            GUILayout.EndHorizontal();
        }
    }
}
