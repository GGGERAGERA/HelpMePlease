#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Presentation of the existing lab, using its IMGUI and runtime objects only.
public sealed class EnemyOrbitalLabSandboxView : IDisposable
{
    readonly EnemyOrbitalLabController lab;
    readonly List<Texture2D> textures = new();
    readonly Dictionary<LineRenderer, (Gradient gradient, float width)> rings = new();
    GUIStyle title, heading, text, small, button, active, banner, badge;
    Texture2D background;
    Vector2 scroll;
    Camera camera;
    Rect originalViewport;
    float glow;
    bool wasSlowing;
    float transition;
    static readonly Color Cyan = new(.2f, .94f, 1f);
    static readonly string[] Tabs = { "ВРАГИ", "ОРБИТЫ", "ОРУЖИЕ", "МОДИФИКАЦИИ" };
    public EnemyOrbitalLabSandboxView(EnemyOrbitalLabController owner) => lab = owner;
    Texture2D Texture(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color); texture.Apply(); textures.Add(texture); return texture;
    }
    void Styles()
    {
        if (title != null) return;
        background = Texture(new Color(.045f, .059f, .078f));
        text = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = false, padding = new RectOffset(0, 0, 3, 3) };
        text.normal.textColor = new Color(.88f, .92f, .96f);
        small = new GUIStyle(text) { fontSize = 13 };
        title = new GUIStyle(text) { fontSize = 23, fontStyle = FontStyle.Bold };
        heading = new GUIStyle(text) { fontSize = 17, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 12, 8) };
        heading.normal.textColor = Cyan;
        button = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true, fixedHeight = 42, padding = new RectOffset(8, 8, 4, 4), margin = new RectOffset(0, 6, 3, 3) };
        button.normal.background = Texture(new Color(.12f, .16f, .21f)); button.normal.textColor = text.normal.textColor;
        button.hover.background = Texture(new Color(.2f, .3f, .37f)); button.hover.textColor = Color.white;
        button.active.background = button.hover.background;
        active = new GUIStyle(button); active.normal.background = Texture(new Color(.1f, .38f, .45f)); active.normal.textColor = Color.white;
        banner = new GUIStyle(active) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fixedHeight = 0 };
        badge = new GUIStyle(banner) { fontSize = 14, wordWrap = false };
    }
    public void Tick(bool visible, Rect panel)
    {
        var next = lab.CameraRig != null ? lab.CameraRig.ControlledCamera : null;
        if (next != camera)
        {
            if (camera != null) camera.rect = originalViewport;
            camera = next;
            if (camera != null) originalViewport = camera.rect;
        }
        if (camera != null) camera.rect = visible ? new Rect((panel.xMax + 16f) / Screen.width, 0, 1f - (panel.xMax + 16f) / Screen.width, 1) : originalViewport;
        bool slowing = lab.CombatV2?.IsSlowingOrbits == true;
        if (slowing != wasSlowing) { transition = 1f; wasSlowing = slowing; }
        transition = Mathf.MoveTowards(transition, 0f, Time.unscaledDeltaTime * 5f);
        glow = Mathf.MoveTowards(glow, slowing ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (lab.Ready)
            foreach (var ring in lab.Station.Rings)
            {
                var view = ring.Mounts.FirstOrDefault()?.Transform.GetComponentInParent<OrbitalRingView>();
                if (view?.FrontLine == null) continue;
                var line = view.FrontLine;
                if (!rings.TryGetValue(line, out var baseline))
                { baseline = (line.colorGradient, line.widthMultiplier); rings.Add(line, baseline); }
                if (glow > 0f)
                {
                    var gradient = new Gradient();
                    gradient.SetKeys(new[] { new GradientColorKey(Color.Lerp(baseline.gradient.Evaluate(0f), Cyan, glow), 0), new GradientColorKey(Color.Lerp(baseline.gradient.Evaluate(1f), Cyan, glow), 1) },
                        new[] { new GradientAlphaKey(Mathf.Lerp(baseline.gradient.Evaluate(0).a, 1f, glow), 0), new GradientAlphaKey(Mathf.Lerp(baseline.gradient.Evaluate(1).a, 1f, glow), 1) });
                    line.colorGradient = gradient;
                    line.widthMultiplier = baseline.width * (1f + glow * .55f + transition * .7f);
                }
                else { line.colorGradient = baseline.gradient; line.widthMultiplier = baseline.width; }
            }
    }
    public void Draw(bool visible, Rect panel)
    {
        Styles();
        if (visible)
        {
            GUI.DrawTexture(panel, background);
            GUILayout.BeginArea(new Rect(18, 16, panel.width - 30, panel.height - 26));
            GUILayout.Label("ПЕСОЧНИЦА ОРБИТ", title);
            if (GUILayout.Button("SEGMENTED ORBIT →", button)) lab.SetSegmentedOrbit(true);
            GUILayout.Label("Стрельба наружу · Shift: вращение ×0,4", small);
            GUILayout.Space(8);
            Row(() => Btn(lab.Paused ? "Продолжить · P" : "Пауза · P", () => lab.SetPaused(!lab.Paused), lab.Paused),
                () => Btn("Повторить бой", lab.RepeatSandboxEncounter));
            Row(() => Tab(0), () => Tab(1)); Row(() => Tab(2), () => Tab(3));
            GUILayout.Space(4);
            if (!lab.Ready) GUILayout.Label("Подготавливаем арену…", text);
            else
            {
                scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(Mathf.Max(200, panel.height - 385)));
                scroll.x = 0;
                switch (lab.SandboxTab) { case 0: Enemies(); break; case 1: Orbits(); break; case 2: Weapons(); break; default: Modules(); break; }
                GUILayout.EndScrollView();
                GUILayout.Space(8);
                GUILayout.Label(lab.SandboxMessage, small, GUILayout.Height(40));
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("ЛКМ: выбор · На паузе: расстановка · Esc: отмена", small);
            GUILayout.Label("ПКМ: центр · Shift: орбиты · F1: панель", small);
            GUILayout.Label("Тренировка: герой не погибает", small);
            GUILayout.EndArea();
        }
        else GUI.Label(new Rect(20, 16, 280, 32), "F1 — открыть песочницу", text);
        if (lab.CombatV2?.IsSlowingOrbits == true || glow > .01f)
        {
            float left = visible ? panel.xMax + 16f : 0;
            GUI.Label(new Rect(left + (Screen.width - left - 360) * .5f, 20, 360, 48), "ОРБИТЫ ЗАМЕДЛЕНЫ", banner);
            if (camera != null && lab.Player != null)
            {
                Vector3 point = camera.WorldToScreenPoint(lab.Player.transform.position);
                GUI.Label(new Rect(point.x - 67, Screen.height - point.y + 42, 134, 30), "SHIFT  ×0,4", badge);
            }
        }
    }
    void Tab(int index)
    {
        Btn(Tabs[index], () => { lab.SetSandboxTab(index); scroll = Vector2.zero; }, lab.SandboxTab == index);
    }
    void Btn(string name, Action action, bool selected = false)
    { if (GUILayout.Button(name, selected ? active : button, GUILayout.ExpandWidth(true))) action(); }
    void Row(Action left, Action right) { GUILayout.BeginHorizontal(); left(); right(); GUILayout.EndHorizontal(); }
    void Label(string value) => GUILayout.Label(value, text);
    void Heading(string value) => GUILayout.Label(value, heading);
    void Enemies()
    {
        Heading("ВЫБЕРИТЕ БОЙ");
        Label($"На арене: {lab.AliveEnemies} / {EnemyOrbitalLabController.SandboxEnemyLimit}");
        Row(() => Btn("Толпа", () => lab.StartSandboxEncounter(1)), () => Btn("Толпа + стрелки", () => lab.StartSandboxEncounter(2)));
        Row(() => Btn("Щитовые", () => lab.StartSandboxEncounter(3)), () => Btn("Очистить арену", lab.ClearSandboxArena));
        Btn("ПЛОТНЫЙ БОЙ", () => lab.StartSandboxEncounter(4), true);
        GUILayout.Space(8);
        Btn(lab.SandboxSpawning ? "Остановить спавн" : "Возобновить спавн", () => lab.SetSandboxSpawning(!lab.SandboxSpawning));
        Label(lab.SandboxSpawning ? "Спавн включён · потери пополняются" : "Спавн остановлен");
        Heading("РЕЗУЛЬТАТ БОЯ");
        var report = lab.Telemetry.Snapshot();
        bool recording = lab.Telemetry.Recording;
        bool completed = !recording && report.Weapons.Length > 0;
        Label(recording ? (lab.Paused ? "Запись на паузе" : "Запись идёт")
            : completed ? "Замер завершён" : "Замер не начат");
        if (completed)
            GUILayout.Label($"Показатели зафиксированы. В полёте на момент остановки: {report.Weapons.Sum(w => w.Pending)} снарядов без попадания.", small);
        if (recording || completed)
        {
            Btn("Копировать JSON замера", () => GUIUtility.systemCopyBuffer = lab.CombatTelemetryJson());
            GUILayout.Label((recording ? "JSON: текущий снимок записи." : "JSON: завершённый замер.")
                + " Не восстанавливает сборку.", small);
        }
        Label($"{report.Elapsed:0.0} с · убийств {report.Weapons.Sum(w => w.Kills)}");
        GUILayout.Label(report.SurvivalApplicable ? $"Получено урона: {report.DamageReceived:0.#}" : $"Выживаемость: н/д · контактов {report.PlayerImpacts}", small);
        GUILayout.Label($"Путь: {report.DistanceTravelled:0.0} м\nМакс. смещение центра: {report.MaximumCenterOffset:0.0} м\nЗамедление орбит (Shift): {report.OrbitSlowSeconds:0.0} с", small);
        if (report.ConfigurationChanged)
            GUILayout.Label("Результат объединяет несколько конфигураций. Модули в строках — на момент регистрации пушки. Для отдельного сравнения повторите бой.", small);
        foreach (var row in report.Weapons)
            GUILayout.Label($"{row.Name} · ID {row.WeaponId} · {row.Modules}\nСнарядов {row.Fired} · попад. {row.HitShots} · мимо {row.Misses}\nУрон {row.Damage:0.#} · убийств {row.Kills} · щит {row.ShieldBlocks} · в полёте {row.Pending}", small);
        if (recording) Btn("Завершить замер", lab.StopCombatTelemetry);
    }
    void RingPicker()
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < lab.Station.Rings.Count; i++) { int index = i; Btn("Кольцо " + (i + 1), () => lab.SelectSandboxRing(index), lab.SandboxRingIndex == i); }
        GUILayout.EndHorizontal();
    }
    void Orbits()
    {
        Heading("КОЛЬЦА"); RingPicker();
        Row(() => Btn("+ Кольцо", () => lab.ChangeSandboxRings(true)), () => Btn("− Кольцо", () => lab.ChangeSandboxRings(false)));
        GUILayout.Label("От 1 до 3 колец · до 4 пушек на кольце", small);
        var ring = lab.SandboxRing;
        if (ring == null) return;
        Heading("ВРАЩЕНИЕ"); Label($"Скорость: {ring.RotationSpeed:0}°/с");
        float speed = GUILayout.HorizontalSlider(ring.RotationSpeed, 10f, 180f, GUILayout.Height(26));
        if (Mathf.Abs(speed - ring.RotationSpeed) > .1f) lab.SetSandboxRingSpeed(speed);
        Row(() => Btn("↺ Против часовой", () => lab.SetSandboxRingDirection(1), ring.State.Direction > 0),
            () => Btn("↻ По часовой", () => lab.SetSandboxRingDirection(-1), ring.State.Direction < 0));
        Heading("РАСПОЛОЖЕНИЕ НА КОЛЬЦЕ");
        Row(() => Btn("Равномерно", () => lab.ApplySandboxLayout("EVEN")), () => Btn("Группа", () => lab.ApplySandboxLayout("GROUPED")));
        Btn("Своя сборка", () => lab.ApplySandboxLayout("CUSTOM"));
        Label("Меняются только углы выбранного кольца. Оружие и модификации сохраняются.");
    }
    static string Name(OrbitalModuleRuntime weapon) => weapon.Kind switch
    { OrbitalModuleKind.Shotgun => "Дробовик", OrbitalModuleKind.Sniper => "Снайперка", OrbitalModuleKind.Saw => "Пила", _ => "Пистолет" };
    public static string ModuleName(string value) => value switch
    { "RAPID" => "Скорострельность", "PIERCE" => "Пробивание", "MULTISHOT" => "Доп. снаряд", _ => "Пусто" };
    void WeaponPicker()
    {
        var weapons = lab.Station.Modules.ToArray();
        for (int i = 0; i < weapons.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, weapons.Length); j++)
            {
                var weapon = weapons[j]; int ordinal = j + 1;
                Btn($"{ordinal}. {Name(weapon)} · к{weapon.CurrentMount.Ring.State.Order + 1}", () => { lab.SetPaused(true); lab.SelectSandboxWeapon(weapon.StableModuleId); }, weapon.StableModuleId == lab.SandboxSelectedWeaponId);
            }
            GUILayout.EndHorizontal();
        }
        if (weapons.Length == 0) Label("Оружия пока нет. Установите пушку ниже.");
    }
    void Selected()
    {
        var weapon = lab.SandboxWeapon;
        if (weapon == null) { Label("Выберите пушку на арене или в списке."); return; }
        Heading($"{Name(weapon)} · кольцо {weapon.CurrentMount.Ring.State.Order + 1}");
        int id = weapon.StableModuleId;
        if (lab.WeaponModules.SlotCount(id) == 0) { GUILayout.Label("Контактное оружие · без модификаций", small); return; }
        GUILayout.Label(string.Join(" + ", Enumerable.Range(0, lab.WeaponModules.SlotCount(id)).Select(i => ModuleName(lab.WeaponModules.GetModule(id, i)))), small);
    }
    void Weapons()
    {
        Heading("СРАВНЕНИЕ ТАКТИКИ");
        Row(() => Btn("Скопление", () => lab.ApplyTacticalPreset("GROUPED")), () => Btn("Равномерно", () => lab.ApplyTacticalPreset("EVEN")));
        Btn("Смешанная сборка", () => lab.ApplyTacticalPreset("MIXED"));
        Heading("УСТАНОВЛЕННОЕ ОРУЖИЕ"); WeaponPicker(); Selected();
        GUI.enabled = lab.SandboxWeapon != null;
        Row(() => Btn("Переместить", lab.MoveSandboxWeapon), () => Btn("Удалить", () => lab.RemoveSandboxWeapon()));
        GUI.enabled = true;
        if (lab.Placement.HasSelection) Label(lab.Placement.PreviewValid ? "Зелёное дуло: ЛКМ — поставить" : "Красное дуло: недостаточно места");
        Heading("УСТАНОВИТЬ НА КОЛЬЦО"); RingPicker();
        Row(() => Btn("+ Пистолет", () => lab.InstallSandboxWeapon(0)), () => Btn("+ Дробовик", () => lab.InstallSandboxWeapon(1)));
        Row(() => Btn("+ Снайперка", () => lab.InstallSandboxWeapon(2)), () => Btn("+ Пила", () => lab.InstallSandboxWeapon(3)));
        Heading("ГОТОВЫЕ СБОРКИ");
        Btn("Четыре обычных", () => lab.ApplySandboxBuild("A"));
        Row(() => Btn("Одна усиленная", () => lab.ApplySandboxBuild("B")), () => Btn("Две специализированные", () => lab.ApplySandboxBuild("C")));
    }
    void Modules()
    {
        Heading("ВЫБЕРИТЕ ПУШКУ"); WeaponPicker(); Selected();
        int id = lab.SandboxSelectedWeaponId, count = lab.WeaponModules.SlotCount(id);
        if (count == 0) return;
        for (int i = 0; i < count; i++)
        {
            int slot = i;
            Heading($"СЛОТ {i + 1} · {ModuleName(lab.WeaponModules.GetModule(id, i))}");
            Row(() => Btn("Скорострельность", () => lab.SetSandboxModule(slot, "RAPID")), () => Btn("Пробивание", () => lab.SetSandboxModule(slot, "PIERCE")));
            Row(() => Btn("Доп. снаряд", () => lab.SetSandboxModule(slot, "MULTISHOT")), () => Btn("Удалить модуль", () => lab.SetSandboxModule(slot, "EMPTY")));
        }
        if (count == 1) Btn("Открыть второй слот", lab.UnlockSandboxSlot);
        GUILayout.Space(6); GUILayout.Label("Один тип модификации — один раз на пушку.", small);
    }
    public void RestoreVisuals()
    {
        if (camera != null) camera.rect = originalViewport;
        foreach (var entry in rings) if (entry.Key != null) { entry.Key.colorGradient = entry.Value.gradient; entry.Key.widthMultiplier = entry.Value.width; }
        rings.Clear(); glow = transition = 0; wasSlowing = false;
    }
    public void Dispose()
    { RestoreVisuals(); foreach (var texture in textures) if (texture != null) UnityEngine.Object.Destroy(texture); textures.Clear(); }
}
#endif
