#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using Object = UnityEngine.Object;
using Link = SegmentedOrbitConfiguration.Link;
using Motion = SegmentedOrbitConfiguration.Motion;

// Scene-local drawing and direct manipulation. Combat remains in the station and its modules.
public sealed class SegmentedOrbitView : IDisposable
{
    readonly EnemyOrbitalLabController lab;
    SegmentedOrbitConfiguration Build => lab.Segmented;
    GameObject root;
    readonly LineRenderer[] arcs = new LineRenderer[36], arrows = new LineRenderer[36], guides = new LineRenderer[3];
    readonly LineRenderer[] connections;
    Camera camera;
    Rect previousViewport;
    GUIStyle title, text, small, button, selected, icon, linkBadge;
    Texture2D background;
    Vector2 scroll;
    int tab;
    bool showIndividual;
    static readonly Color Cyan = new(.2f,.9f,1f), Amber = new(1f,.72f,.25f);
    static readonly Color ConnectionColor = new(.5f,.72f,.76f);
    static string Marker(Link kind) => kind switch { Link.Damage => "DMG", Link.FireRate => "FR", Link.ExtraShot => "2X", Link.Ricochet => "RIC", _ => "" };
    public SegmentedOrbitView(EnemyOrbitalLabController lab) { this.lab = lab; connections = new LineRenderer[Build.Links.Length]; }
    void Styles()
    {
        if (title != null) return;
        background = new Texture2D(1,1) { hideFlags = HideFlags.HideAndDontSave };
        background.SetPixel(0,0,new Color(.035f,.05f,.07f)); background.Apply();
        text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, padding = new RectOffset(0,0,3,3) };
        text.normal.textColor = new Color(.9f,.94f,.98f);
        title = new GUIStyle(text) { fontSize = 22, fontStyle = FontStyle.Bold };
        title.normal.textColor = Cyan;
        small = new GUIStyle(text) { fontSize = 13 };
        button = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true, fixedHeight = 34, margin = new RectOffset(2,2,3,3) };
        selected = new GUIStyle(button); selected.normal.textColor = Cyan;
        icon = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0,0,0,0) };
        linkBadge = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold,
            wordWrap = false, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(2,2,0,0) };
        linkBadge.normal.background = background; linkBadge.normal.textColor = new Color(.9f,.96f,.97f);
    }
    LineRenderer Line(string name, int count, float width)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false;
        line.sharedMaterial = lab.Station.VisualMaterial; line.positionCount = count; line.widthMultiplier = width;
        var core = lab.Station.GetComponent<OrbitalStationView>().Core;
        line.sortingLayerID = core.sortingLayerID; line.sortingOrder = core.sortingOrder + 2;
        return line;
    }
    void EnsureGeometry()
    {
        if (root != null) return;
        root = new GameObject("Segmented Orbit geometry"); root.transform.SetParent(lab.Station.transform, false);
        for (int i = 0; i < 36; i++) { arcs[i] = Line("Segment", 17, .1f); arrows[i] = Line("Outward muzzle", 5, .025f); }
        for (int level = 0; level < 3; level++) guides[level] = Line("Potential level", 145, .012f);
        for (int i = 0; i < connections.Length; i++) connections[i] = Line("Connection",9,.025f);
    }
    static Vector3 Point(float degrees, float radius) => new(Mathf.Cos(degrees * Mathf.Deg2Rad) * radius, Mathf.Sin(degrees * Mathf.Deg2Rad) * radius, 0f);
    public void Tick(bool visible, Rect panel)
    {
        if (!lab.Ready || !Build.Bound) return;
        if (camera == null) { camera = lab.CameraRig.ControlledCamera; previousViewport = camera.rect; }
        camera.rect = visible ? new Rect(panel.xMax / Screen.width, 0, 1f - panel.xMax / Screen.width, 1) : previousViewport;
        EnsureGeometry();
        for (int level = 0; level < 3; level++)
        {
            var ring = lab.Station.Rings[level];
            var stock = ring.Mounts[0].Transform.GetComponentInParent<OrbitalRingView>();
            stock.FrontLine.enabled = stock.BackLine.enabled = false;
            guides[level].enabled = lab.Paused;
            guides[level].startColor = guides[level].endColor = new Color(.3f,.6f,.7f,.16f);
            for (int j = 0; j < 145; j++) guides[level].SetPosition(j, Point(j * 2.5f, ring.Radius));
            for (int position = 0; position < 12; position++)
            {
                int index = level * 12 + position;
                var mount = ring.Mounts[position]; var cell = Build.Cells[index];
                var stockMount = mount.Transform.GetComponent<OrbitalMountView>(); stockMount.Marker.enabled = stockMount.Halo.enabled = false;
                bool related = lab.Paused && (Build.SelectionTouches(index) || Build.Hovered >= 0 &&
                    (Build.HoveredLink ? Build.Links[Build.Hovered].Touches(index) : Build.Hovered == index));
                bool target = lab.Paused && Build.IsConnectionTarget(index);
                Color color = related ? Amber : target ? new Color(.3f,1f,.6f) : new Color(.35f,.72f,.85f,.85f);
                arcs[index].enabled = cell.Segment; arcs[index].widthMultiplier = related ? .15f : .09f;
                arcs[index].startColor = arcs[index].endColor = color;
                float angle = ring.Phase + mount.LocalPhase;
                for (int j = 0; j < 17; j++) arcs[index].SetPosition(j, Point(angle - 15f + j * 30f / 16f, ring.Radius));
                var gun = mount.Module;
                if (gun != null) gun.SetHighlighted(related || target);
                arrows[index].enabled = lab.Paused && gun != null && cell.Weapon != 3;
                arrows[index].startColor = arrows[index].endColor = related ? Amber : Cyan;
                Vector3 start = Point(angle, ring.Radius + .36f), end = Point(angle, ring.Radius + .95f);
                Vector3 axis = (end - start).normalized, side = new(-axis.y,axis.x,0f);
                arrows[index].SetPositions(new[] { start, end, end - axis * .18f + side * .12f, end, end - axis * .18f - side * .12f });
            }
        }
        Build.ResolveConnections();
        foreach (var edge in Build.Links)
        {
            bool preview = lab.Paused && edge.Module == Link.None && Build.ConnectingFrom >= 0 &&
                edge.Touches(Build.ConnectingFrom) && Build.CanConnect(edge.A,edge.B,out _);
            var line = connections[edge.Id];
            line.enabled = preview || edge.Module != Link.None;
            if (!line.enabled) continue;
            bool chosen = lab.Paused && Build.SelectedLink && Build.Selected == edge.Id;
            Color color = chosen ? Amber : edge.IsActive || preview ? ConnectionColor : Color.gray;
            color.a = chosen ? 1f : preview ? .3f : edge.IsActive ? .65f : .22f;
            line.startColor = line.endColor = color;
            line.widthMultiplier = chosen ? .055f : lab.Paused ? .025f : .02f;
            for (int j = 0; j < 9; j++) line.SetPosition(j,ConnectionPoint(edge,j / 8f));
        }
    }
    Vector3 ConnectionPoint(SegmentedOrbitConfiguration.Connection edge, float t)
    {
        var a = Build.Mount(edge.A); var b = Build.Mount(edge.B);
        float start = a.Ring.Phase + a.LocalPhase;
        float end = b.Ring.Phase + b.LocalPhase;
        return Point(start + Mathf.DeltaAngle(start,end) * t,a.Ring.Radius - .45f * Mathf.Sin(Mathf.PI*t));
    }
    Rect WorldRect(float angle, float radius, int size)
    {
        Vector3 world = lab.Station.transform.position + Point(angle,radius);
        Vector3 point = camera.WorldToScreenPoint(world);
        return new Rect(point.x - size * .5f, Screen.height - point.y - size * .5f, size, size);
    }
    void DrawConstruction()
    {
        if (!lab.Paused || camera == null || !Build.Bound) return;
        Build.Hovered = -1; Build.HoveredLink = false;
        DrawLinkMarkers(true);
        for (int i = 0; i < 36; i++)
        {
            var ring = lab.Station.Rings[i / 12]; float angle = ring.Phase + 90f + i % 12 * 30f;
            var cell = Build.Cells[i]; var rect = WorldRect(angle, ring.Radius, 26);
            var saved = GUI.backgroundColor;
            GUI.backgroundColor = Build.SelectionTouches(i) ? Amber : Build.IsConnectionTarget(i) ? new Color(.3f,1f,.6f) : cell.Segment ? Cyan : new Color(.4f,.55f,.65f,.4f);
            string label = cell.Weapon >= 0 ? new[] { "P", "S", "N", "O" }[cell.Weapon] : cell.Segment ? "■" : "+";
            // Weapon bodies remain unobstructed; their small selection handle sits inside the segment.
            if (cell.Weapon >= 0) rect = WorldRect(angle, ring.Radius - .25f, 24);
            if (!Build.HoveredLink && rect.Contains(Event.current.mousePosition)) Build.Hovered = i;
            if (GUI.Button(rect,label,icon))
            {
                bool moving = Build.MovingFrom >= 0;
                bool connecting = Build.ConnectingFrom >= 0;
                Build.Select(i,false);
                if (!moving && !connecting && !cell.Segment) Build.AddSegment(i);
                tab = 0;
            }
            GUI.backgroundColor = saved;
        }
        var pointer = Event.current;
        if (pointer.type == EventType.MouseDown && pointer.button == 0 &&
            camera.pixelRect.Contains(new Vector2(pointer.mousePosition.x,Screen.height - pointer.mousePosition.y)))
        {
            Vector2 world = camera.ScreenToWorldPoint(new Vector3(pointer.mousePosition.x,
                Screen.height - pointer.mousePosition.y,-camera.transform.position.z));
            if (SelectWeaponAt(world)) pointer.Use();
        }
        string hint = Build.ConnectingFrom >= 0 ? "ЛКМ по подсвеченной пушке — соединить · Esc — отмена" : "+ построить · Пушка → модуль → подсвеченная цель";
        if (!Build.HoveredLink && Build.Hovered >= 0 && Build.IsConnectionTarget(Build.Hovered))
        {
            int source = Build.ConnectingFrom >= 0 ? Build.ConnectingFrom : Build.SelectedCell;
            var edge = Build.FindConnection(source,Build.Hovered);
            hint = $"{Build.ConnectionName(edge)} · {Build.Strength(edge)*100:0}% · " +
                (Build.ConnectingFrom >= 0 ? "ЛКМ — соединить" : "Выберите модуль в панели");
        }
        GUI.Label(new Rect(camera.pixelRect.x + 18,65,650,28),hint,small);
    }
    void DrawLinkMarkers(bool interactive)
    {
        if (camera == null) return;
        Build.ResolveConnections();
        foreach (var edge in Build.Links)
        {
            if (edge.Module == Link.None) continue;
            Vector3 point = camera.WorldToScreenPoint(lab.Station.transform.TransformPoint(ConnectionPoint(edge,.5f)));
            string label = Marker(edge.Module);
            float width = Mathf.Max(28f,Mathf.Ceil(linkBadge.CalcSize(new GUIContent(label)).x) + 8f);
            var rect = new Rect(point.x - width*.5f,Screen.height - point.y - 10, width,20);
            var saved = GUI.color;
            GUI.color = interactive && Build.SelectedLink && Build.Selected == edge.Id ? Amber :
                edge.IsActive ? Color.white : new Color(.55f,.58f,.6f,.7f);
            if (interactive)
            {
                if (rect.Contains(Event.current.mousePosition)) { Build.Hovered = edge.Id; Build.HoveredLink = true; }
                if (GUI.Button(rect,label,linkBadge)) { Build.Select(edge.Id,true); tab = 0; }
            }
            else GUI.Label(rect,label,linkBadge);
            GUI.color = saved;
        }
    }
    bool SelectWeaponAt(Vector2 world)
    {
        var gun = lab.Station.Modules.Where(m => m.HitTest(world,.08f))
            .OrderBy(m => (m.WorldPosition - world).sqrMagnitude).FirstOrDefault();
        if (gun == null) return false;
        Build.Select(gun.CurrentMount.Ring.State.Order * 12 + gun.CurrentMount.MountIndex,false);
        tab = 0; return true;
    }
    void Btn(string label, Action action, bool active = false)
    { if (GUILayout.Button(label,active ? selected : button)) action(); }
    void Row(Action a, Action b) { GUILayout.BeginHorizontal(); a(); b(); GUILayout.EndHorizontal(); }
    void Label(string value) => GUILayout.Label(value,text);
    void Heading(string value) { GUILayout.Space(8); var color = GUI.contentColor; GUI.contentColor = Cyan; Label(value); GUI.contentColor = color; }
    public void Draw(bool visible, Rect panel)
    {
        Styles();
        if (!lab.Ready || !Build.Bound)
        { GUI.Label(new Rect(20,20,380,50),"Подготавливаем Segmented Orbit…",title); return; }
        DrawConstruction();
        if (lab.Paused)
        {
            GUI.DrawTexture(panel,background);
            GUILayout.BeginArea(new Rect(14,12,panel.width - 28,panel.height - 22));
            GUILayout.Label("SEGMENTED ORBIT",title);
            GUILayout.Label("Связи рядом и через пустой слот",small);
            Row(() => Btn("▶ В БОЙ · Tab / P",lab.StartSegmentedCombat,true), () => Btn("Свободный режим",() => lab.SetSegmentedOrbit(false)));
            GUILayout.BeginHorizontal();
            Btn("V1 Rotating",() => Build.SetMotion(Motion.Rotating),Build.Movement == Motion.Rotating);
            Btn("V2 Fixed",() => Build.SetMotion(Motion.Fixed),Build.Movement == Motion.Fixed);
            Btn("V3 Manual",() => Build.SetMotion(Motion.Manual),Build.Movement == Motion.Manual);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Btn("СБОРКА",() => tab = 0,tab == 0); Btn("БОЙ / ПРЕСЕТЫ",() => tab = 1,tab == 1); Btn("НАСТРОЙКИ",() => tab = 2,tab == 2);
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll,false,false);
            if (tab == 0) Selection(); else if (tab == 1) Encounter(); else MotionSettings();
            GUILayout.EndScrollView();
            GUILayout.Label(Build.Message,small,GUILayout.MinHeight(38));
            GUILayout.Label("ЛКМ: строить / соединить · Esc: отмена действия",small);
            GUILayout.Label("WASD · Space: рывок · ПКМ: центр · Shift: Bullet Time",small);
            GUILayout.EndArea();
        }
        else
        {
            DrawLinkMarkers(false);
            if (GUI.Button(new Rect(16,14,240,36),"КОНСТРУКТОР · Tab / P",button)) lab.SetPaused(true);
            GUI.Label(new Rect(275,16,560,30),Build.Preset + "  ·  " + Build.Movement + "  ·  Shift: Bullet Time",small);
        }
        if (Build.Movement == Motion.Manual) EnergyHud();
        if (lab.Station.BulletTime.IsActive)
        {
            var saved = GUI.color; GUI.color = Cyan;
            GUI.DrawTexture(new Rect(lab.Paused ? panel.xMax : 0,0,Screen.width,5),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width-5,0,5,Screen.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(lab.Paused ? panel.xMax : 0,Screen.height-5,Screen.width,5),Texture2D.whiteTexture);
            GUI.Label(new Rect(Screen.width*.5f-110,58,260,36),"BULLET TIME",title); GUI.color = saved;
        }
    }
    void Selection()
    {
        Build.ResolveConnections();
        Label($"Сегменты {Build.SegmentCount}/{lab.SegmentedSegmentBudget}  ·  Пушки {Build.WeaponCount}  ·  Связи {Build.Links.Count(e => e.IsActive)}/{Build.LinkCount}");
        GUILayout.Label($"Соседняя связь — 100%\nСвязь через слот — {lab.SegmentedSkipStrength*100:0}%",small);
        LevelPicker();
        if (!Build.SelectedLink) Label($"Позиция {Build.Selected % 12 + 1}");
        if (Build.SelectedLink) LinkSelection(); else SegmentSelection();
    }
    void LevelPicker()
    {
        int level = Build.SelectedCell / 12;
        Heading("УРОВЕНЬ");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 3; i++) { int chosen = i; Btn((i+1).ToString(),() => Build.Select(chosen*12,false),level == i); }
        GUILayout.EndHorizontal();
    }
    void EnergyHud()
    {
        var area = new Rect(Screen.width - 260,14,242,52);
        GUI.DrawTexture(area,background);
        GUI.Label(new Rect(area.x + 8,area.y + 2,area.width - 16,24),$"Q / E · ПОВОРОТ   {Build.RotationEnergy:0}/{lab.ManualRotationCapacity:0}",small);
        var bar = new Rect(area.x + 9,area.y + 31,area.width - 18,10);
        var saved = GUI.color; GUI.color = new Color(.15f,.22f,.28f); GUI.DrawTexture(bar,Texture2D.whiteTexture);
        GUI.color = Build.RotationEnergy > lab.ManualRotationCapacity * .25f ? Cyan : Amber;
        bar.width *= Mathf.Clamp01(Build.RotationEnergy / lab.ManualRotationCapacity);
        GUI.DrawTexture(bar,Texture2D.whiteTexture); GUI.color = saved;
    }
    void MotionSettings()
    {
        Heading("СИЛА СВЯЗИ ЧЕРЕЗ СЛОТ");
        Label($"{lab.SegmentedSkipStrength*100:0}% · рядом всегда 100%");
        lab.SegmentedSkipStrength = Mathf.Round(GUILayout.HorizontalSlider(lab.SegmentedSkipStrength,.1f,1f)*100)/100;
        if (Build.Movement == Motion.Manual)
        {
            Heading("РУЧНОЙ ПОВОРОТ · Q / E");
            GUILayout.Label("Q — против часовой, E — по часовой. Отпустите обе кнопки для восстановления. На нуле стрельба продолжается.",small);
            Label($"Запас: {lab.ManualRotationCapacity:0}"); lab.ManualRotationCapacity = Mathf.Round(GUILayout.HorizontalSlider(lab.ManualRotationCapacity,25,200));
            Label($"Поворот: {lab.ManualRotationSpeed:0}°/с"); lab.ManualRotationSpeed = Mathf.Round(GUILayout.HorizontalSlider(lab.ManualRotationSpeed,15,180));
            Label($"Расход: {lab.ManualRotationDrain:0}/с"); lab.ManualRotationDrain = Mathf.Round(GUILayout.HorizontalSlider(lab.ManualRotationDrain,0,100));
            Label($"Восстановление: {lab.ManualRotationRecovery:0}/с"); lab.ManualRotationRecovery = Mathf.Round(GUILayout.HorizontalSlider(lab.ManualRotationRecovery,0,100));
            Build.RefreshRotationCapacity();
        }
        LevelPicker(); int level = Build.SelectedCell / 12;
        var ring = lab.Station.Rings[level];
        GUI.enabled = Build.Movement == Motion.Rotating;
        Label($"Скорость уровня: {ring.State.BaseRotationSpeed:0}°/с");
        float speed = GUILayout.HorizontalSlider(ring.State.BaseRotationSpeed,0,180);
        if (!Mathf.Approximately(speed,ring.State.BaseRotationSpeed)) { ring.State.BaseRotationSpeed = speed; lab.SegmentedSpeeds[level] = speed; }
        Row(() => Btn("↶ Против часовой",() => ring.State.Direction = 1,ring.State.Direction == 1), () => Btn("↷ По часовой",() => ring.State.Direction = -1,ring.State.Direction == -1));
        GUI.enabled = true;
        Label($"Радиус: {ring.State.Radius:0.0}");
        float radius = GUILayout.HorizontalSlider(ring.State.Radius,1.2f,6f);
        if (!Mathf.Approximately(radius,ring.State.Radius))
        { ring.State.Radius = radius; lab.SegmentedRadii[level] = radius; ring.Tick(0f); }
    }
    void SegmentSelection()
    {
        int index = Build.Selected; var cell = Build.Cells[index];
        if (!cell.Segment) { Btn("+ Добавить сегмент",() => Build.AddSegment(index)); return; }
        if (cell.Weapon < 0)
        {
            Heading("ОРУЖИЕ ИЗ ЗАПАСА");
            for (int i = 0; i < 4; i++)
            { int kind = i; GUI.enabled = Build.WeaponRemaining(i) > 0; Btn(SegmentedOrbitConfiguration.WeaponNames[i] + " · " + Build.WeaponRemaining(i),() => Build.InstallWeapon(index,kind)); }
            GUI.enabled = true;
        }
        else
        {
            Label(SegmentedOrbitConfiguration.WeaponNames[cell.Weapon]);
            Row(() => Btn("Переместить",Build.BeginMove,Build.MovingFrom >= 0),() => Btn("Убрать оружие",() => Build.RemoveWeapon(index)));
            var gun = Build.Gun(index);
            Label($"От связей: урон +{(Build.DamageFactor(gun.StableModuleId)-1)*100:0}% · темп +{(Build.RateFactor(gun.StableModuleId)-1)*100:0}% · рикошеты {Build.Ricochets(gun.StableModuleId)}");
            Heading($"СОЕДИНИТЬ · {Build.ActiveLinkCount(index)}/2 активны");
            GUILayout.Label("Выберите модуль, затем ЛКМ по зелёной пушке. Значок существующей связи — заменить или снять модуль.",small);
            ConnectionButtons(kind => Build.BeginConnect(kind),kind => Build.LinkRemaining(kind) > 0,Build.ConnectingModule);
            if (Build.ConnectingFrom >= 0) Btn("Отменить соединение",Build.CancelInteraction);
            if (gun is EnemyOrbitalLabWeapon)
            {
                Btn(showIndividual ? "Личные модификации ▾" : "Личные модификации ▸",() => showIndividual = !showIndividual);
                if (!showIndividual) { Btn("Удалить сегмент",() => Build.RemoveSegment(index)); return; }
                int id = gun.StableModuleId;
                string[] names = { "EMPTY", "RAPID", "PIERCE", "MULTISHOT" };
                for (int slot = 0; slot < lab.WeaponModules.SlotCount(id); slot++)
                {
                    int chosen = slot;
                    int current = Array.IndexOf(names,lab.WeaponModules.GetModule(id,slot));
                    int next = GUILayout.SelectionGrid(current,new[] { "Нет", "Темп", "Пробой", "+луч" },4);
                    if (next != current) lab.WeaponModules.SetModule(id,chosen,names[next]);
                }
                if (lab.WeaponModules.SlotCount(id) == 1) Btn("Открыть второй слот",() => lab.WeaponModules.UnlockSecondSlot(id));
            }
        }
        Btn("Удалить сегмент",() => Build.RemoveSegment(index));
    }
    void LinkSelection()
    {
        var edge = Build.Links[Build.Selected];
        string Name(int index) => Build.Cells[index].Weapon < 0 ? "Нет оружия" : SegmentedOrbitConfiguration.WeaponNames[Build.Cells[index].Weapon];
        Label($"{Build.ConnectionName(edge)} · сила {Build.Strength(edge)*100:0}%");
        GUILayout.Label($"Уровень {edge.A / 12 + 1} · позиции {edge.A % 12 + 1} ↔ {edge.B % 12 + 1}",small);
        Label(Name(edge.A) + " ↔ " + Name(edge.B));
        Heading(SegmentedOrbitConfiguration.LinkNames[(int)edge.Module]);
        Label(Build.Effect(edge));
        Label(Build.Status(Build.Selected));
        foreach (int index in new[] { edge.A, edge.B })
            if (Build.Gun(index) is EnemyOrbitalLabWeapon gun)
                GUILayout.Label($"{Name(index)}: урон +{(Build.DamageFactor(gun.StableModuleId)-1)*100:0}% · темп +{(Build.RateFactor(gun.StableModuleId)-1)*100:0}% · рикошеты {Build.Ricochets(gun.StableModuleId)}",small);
        ConnectionButtons(kind => Build.SetLink(Build.Selected,kind),kind => Build.Eligible(Build.Selected) &&
            (edge.Module == kind || Build.LinkRemaining(kind) > 0),edge.Module);
        Btn("Снять модуль",() => Build.SetLink(Build.Selected,Link.None));
    }
    void ConnectionButtons(Action<Link> choose, Func<Link,bool> available, Link current)
    {
        for (int i = 1; i <= 4; i++)
        {
            Link kind = (Link)i;
            GUI.enabled = available(kind);
            Btn(Marker(kind) + "  " + SegmentedOrbitConfiguration.LinkNames[i] + " · " + Build.LinkRemaining(kind),() => choose(kind),current == kind);
        }
        GUI.enabled = true;
    }
    void Encounter()
    {
        Heading("КОНСТРУКЦИИ · ОДИНАКОВЫЙ ЗАПАС");
        foreach (string name in SegmentedOrbitConfiguration.Presets) Btn(name,() => Build.LoadPreset(name),Build.Preset == name);
        Btn("Reset Preset",() => Build.LoadPreset(Build.Preset));
        GUILayout.Label("«Шашечка» — шесть пушек через пустые слоты. Тот же запас оружия и модулей. Multi-Layer — связи на трёх уровнях.",small);
        Heading("СОСТАВ ВРАГОВ");
        Row(() => Btn("Обычные",() => lab.StartSandboxEncounter(1)), () => Btn("Со стрелками",() => lab.StartSandboxEncounter(2)));
        Row(() => Btn("Щитоносцы",() => lab.StartSandboxEncounter(3)), () => Btn("Плотная волна",() => lab.StartSandboxEncounter(4)));
        Label($"Плотность: {lab.SegmentedEnemyDensity} · на арене {lab.AliveEnemies}");
        lab.SegmentedEnemyDensity = Mathf.RoundToInt(GUILayout.HorizontalSlider(lab.SegmentedEnemyDensity,12,64));
        Row(() => Btn(lab.SandboxSpawning ? "Стоп спавн" : "Продолжить спавн",() => lab.SetSandboxSpawning(!lab.SandboxSpawning)), () => Btn("Очистить арену",lab.ClearSandboxArena));
        Btn("Повторить бой",lab.RepeatSandboxEncounter);
        Heading("ЗАПАС ДЛЯ СРАВНЕНИЯ");
        GUILayout.Label($"Сегменты: {lab.SegmentedSegmentBudget}. Пистолеты: {lab.SegmentedPistols}, дробовики: {lab.SegmentedShotguns}, снайперки: {lab.SegmentedSnipers}, пилы: {lab.SegmentedSaws}. По {lab.SegmentedLinksPerType} связи каждого типа. Лимиты настраиваются на Lab Controller в Inspector.",small);
    }
    public void RestoreVisuals()
    {
        if (camera != null) camera.rect = previousViewport;
        camera = null;
        if (root != null) { root.SetActive(false); Object.Destroy(root); } root = null;
    }
    public void Dispose() { RestoreVisuals(); if (background != null) Object.Destroy(background); }
}
#endif
