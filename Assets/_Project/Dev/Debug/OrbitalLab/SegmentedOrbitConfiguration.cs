#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Geometry is the source of truth; station mounts and weapons are its existing combat projection.
public sealed class SegmentedOrbitConfiguration
{
    public const int Levels = 3, Positions = 12;
    public enum Link { None, Damage, FireRate, ExtraShot, Ricochet }
    public enum Motion { Rotating, Fixed, Manual }
    public enum Geometry { Near, Skip }
    public sealed class Cell
    {
        public bool Segment;
        public int Weapon = -1;
        public int IndividualSlots = 1;
        public string First = "EMPTY", Second = "EMPTY";
    }
    public sealed class Connection
    {
        public int Id { get; }
        public int A { get; }
        public int B { get; }
        public Geometry Type => Wrap(B - A) == 1 || Wrap(A - B) == 1 ? Geometry.Near : Geometry.Skip;
        public int Middle => A / Positions * Positions + Wrap(A % Positions + (Wrap(B - A) == 2 ? 1 : -1));
        public Link Module { get; internal set; }
        public bool IsActive { get; internal set; }
        internal float NextExtraShot;
        internal string InactiveReason;
        internal Connection(int id, int a, int b) { Id = id; A = Mathf.Min(a,b); B = Mathf.Max(a,b); }
        public bool Touches(int index) => A == index || B == index;
        public int Other(int index) => index == A ? B : A;
    }
    public readonly Cell[] Cells = Enumerable.Range(0, 36).Select(_ => new Cell()).ToArray();
    public readonly Connection[] Links;
    readonly int[] activeDegrees = new int[Levels * Positions];
    readonly EnemyOrbitalLabController lab;
    OrbitalStationRuntime station;
    public Motion Movement { get; private set; }
    public string Preset { get; private set; } = "Full Circle";
    public string Message { get; private set; } = "ЛКМ: позиция или связь. Выберите действие в панели.";
    public int Selected { get; private set; }
    public bool SelectedLink { get; private set; }
    public int MovingFrom { get; private set; } = -1;
    public int ConnectingFrom { get; private set; } = -1;
    public Link ConnectingModule { get; private set; }
    public float RotationEnergy => station != null ? station.RotationEnergy : lab.ManualRotationCapacity;
    public int Hovered { get; set; } = -1;
    public bool HoveredLink { get; set; }
    public bool Bound => station != null;
    public bool Editing => lab.SegmentedOrbit && lab.Paused;
    public int SegmentCount => Cells.Count(c => c.Segment);
    public int WeaponCount => Cells.Count(c => c.Weapon >= 0);
    public int LinkCount => Links.Count(c => c.Module != Link.None);
    public int SelectedCell => SelectedLink ? Links[Selected].A : Selected;
    public static readonly string[] Presets = { "Full Circle", "Front Arc", "Split Defense", "Multi-Layer", "Шашечка" };
    public static readonly string[] WeaponNames = { "Пистолет", "Дробовик", "Снайперка", "Пила" };
    public static readonly string[] LinkNames = { "Нет связи", "Damage Link", "Fire Rate Link", "Extra Shot Link", "Ricochet Link" };
    public SegmentedOrbitConfiguration(EnemyOrbitalLabController lab)
    {
        this.lab = lab;
        var connections = new List<Connection>();
        for (int i = 0; i < Cells.Length; i++) connections.Add(new Connection(connections.Count,i,Next(i)));
        for (int i = 0; i < Cells.Length; i++) connections.Add(new Connection(connections.Count,i,Next(Next(i))));
        Links = connections.ToArray();
        LoadPresetData(Preset);
    }
    public Cell At(int level, int position) => Cells[level * Positions + Wrap(position)];
    public static int Wrap(int position) => (position % Positions + Positions) % Positions;
    public static int Next(int index) => index / Positions * Positions + Wrap(index + 1);
    public static int Previous(int index) => index / Positions * Positions + Wrap(index - 1);
    public int WeaponLimit(int kind) => kind switch { 0 => lab.SegmentedPistols, 1 => lab.SegmentedShotguns, 2 => lab.SegmentedSnipers, _ => lab.SegmentedSaws };
    public int WeaponRemaining(int kind) => Mathf.Max(0, WeaponLimit(kind) - Cells.Count(c => c.Weapon == kind));
    public int LinkRemaining(Link kind) => Mathf.Max(0, lab.SegmentedLinksPerType - Links.Count(c => c.Module == kind));
    public OrbitalMountRuntime Mount(int index) => station.Rings[index / Positions].Mounts[index % Positions];
    public OrbitalModuleRuntime Gun(int index) => Bound ? Mount(index).Module : null;
    public Connection FindConnection(int a, int b) => Links.FirstOrDefault(c => c.A == Mathf.Min(a,b) && c.B == Mathf.Max(a,b));
    public bool Eligible(int index) => Eligible(Links[index]);
    bool Eligible(Connection edge) => Cells[edge.A].Segment && Cells[edge.B].Segment && Cells[edge.A].Weapon >= 0 && Cells[edge.B].Weapon >= 0 &&
        (edge.Type != Geometry.Skip || !Cells[edge.Middle].Segment && Cells[edge.Middle].Weapon < 0);
    public float Strength(Connection edge) => edge.Type == Geometry.Near ? 1f : Mathf.Clamp(lab.SegmentedSkipStrength,.1f,1f);
    public string ConnectionName(Connection edge) => edge.Type == Geometry.Near ? "Соседняя связь" : "Связь через слот";
    public float ExtraShotInterval(Connection edge) => lab.SegmentedExtraShotCooldown / Strength(edge);
    public bool Active(int index) { ResolveConnections(); return Links[index].IsActive; }
    public int ActiveLinkCount(int cell) { ResolveConnections(); return activeDegrees[cell]; }
    public void ResolveConnections()
    {
        Array.Clear(activeDegrees,0,activeDegrees.Length);
        // Stable geometric order is also the activation policy when dormant links become eligible.
        foreach (var edge in Links)
        {
            edge.IsActive = false; edge.InactiveReason = null;
            if (edge.Module == Link.None) continue;
            if (!Cells[edge.A].Segment || !Cells[edge.B].Segment) edge.InactiveReason = "Неактивна: отсутствует сегмент.";
            else if (!Bound || Cells[edge.A].Weapon < 0 || Cells[edge.B].Weapon < 0 || Gun(edge.A) == null || Gun(edge.B) == null)
                edge.InactiveReason = "Неактивна: нужны две пушки.";
            else if (edge.Type == Geometry.Skip && (Cells[edge.Middle].Segment || Cells[edge.Middle].Weapon >= 0))
                edge.InactiveReason = "Неактивна: промежуточный слот занят. Уберите средний сегмент.";
            else if (activeDegrees[edge.A] >= 2 || activeDegrees[edge.B] >= 2)
                edge.InactiveReason = "Неактивна: предел двух активных связей на пушку.";
            else { edge.IsActive = true; activeDegrees[edge.A]++; activeDegrees[edge.B]++; }
        }
    }
    public string Status(int index)
    {
        var edge = Links[index];
        ResolveConnections();
        if (edge.Module == Link.None) return "Свободная ячейка: выберите модуль.";
        if (!edge.IsActive) return edge.InactiveReason;
        if (edge.Module == Link.Ricochet && edge.Type == Geometry.Skip)
            return Cells[edge.A].Weapon == 0 || Cells[edge.B].Weapon == 0 ?
                "Активна: рикошет через слот получает только пистолет." : "Рикошет через слот поддерживает только пистолет.";
        if (edge.Module == Link.Ricochet && (Cells[edge.A].Weapon == 3 || Cells[edge.B].Weapon == 3))
            return "Рикошет у projectile-пушек; пила не поддерживает рикошет.";
        if (edge.Module == Link.ExtraShot && (Cells[edge.A].Weapon == 3 || Cells[edge.B].Weapon == 3))
            return "Пила не стреляет: дополнительные выстрелы только у пушек.";
        if ((edge.Module == Link.Damage || edge.Module == Link.FireRate) &&
            (Cells[edge.A].Weapon == 3 || Cells[edge.B].Weapon == 3)) return "Бонус стрельбы действует на пушку; контактная пила не поддерживается.";
        return "Активна";
    }
    public string Effect(Connection edge) => edge.Module switch
    {
        Link.Damage => $"+{20f * Strength(edge):0.#}% урона обеим пушкам. Бонусы складываются.",
        Link.FireRate => $"+{20f * Strength(edge):0.#}% скорострельности обеим пушкам. Бонусы складываются.",
        Link.ExtraShot => $"Выстрел соседа без цепочек и сброса его таймера. Интервал связи {ExtraShotInterval(edge):0.00} с.",
        Link.Ricochet => edge.Type == Geometry.Skip ? "+1 рикошет только пистолету. Другие типы оружия через слот не поддерживаются." :
            "+1 рикошет к другой цели в радиусе 5 м. Пистолет, дробовик, снайперка.",
        _ => "Связь между двумя вооружёнными сегментами. Не более двух активных связей на пушку."
    };
    public void Select(int index, bool link)
    {
        if (!Editing || index < 0 || index >= (link ? Links.Length : Cells.Length)) return;
        if (!link && ConnectingFrom >= 0)
        {
            if (index != ConnectingFrom) Connect(ConnectingFrom,index,ConnectingModule);
            else CancelInteraction();
            return;
        }
        if (link) { ConnectingFrom = MovingFrom = -1; ConnectingModule = Link.None; }
        Selected = index; SelectedLink = link;
        if (MovingFrom >= 0 && !link && MovingFrom != index) MoveWeapon(index);
    }
    public void CancelInteraction() { MovingFrom = ConnectingFrom = -1; ConnectingModule = Link.None; Message = "Выбор действия отменён."; }
    public void BeginMove()
    {
        if (!Editing || SelectedLink || Cells[Selected].Weapon < 0) return;
        ConnectingFrom = -1;
        MovingFrom = Selected; Message = "ЛКМ по свободному сегменту любого уровня. Esc — отмена.";
    }
    public void BeginConnect(Link kind)
    {
        if (!Editing || SelectedLink || Cells[Selected].Weapon < 0 || kind == Link.None || LinkRemaining(kind) <= 0) return;
        MovingFrom = -1; ConnectingFrom = Selected; ConnectingModule = kind;
        Message = LinkNames[(int)kind] + ": ЛКМ по подсвеченной пушке. Esc — отмена.";
    }
    public bool CanConnect(int a, int b, out string reason)
    {
        var edge = FindConnection(a,b);
        if (edge == null) { reason = "Выберите пушку на этом уровне: рядом или через один пустой слот."; return false; }
        if (!Cells[edge.A].Segment || !Cells[edge.B].Segment || Cells[edge.A].Weapon < 0 || Cells[edge.B].Weapon < 0)
        { reason = "На обоих крайних сегментах должно быть оружие."; return false; }
        if (edge.Type == Geometry.Skip && !Eligible(edge)) { reason = "Для связи через слот уберите промежуточный сегмент."; return false; }
        if (edge.Module != Link.None) { reason = "Эта пара уже соединена. Выберите её значок для замены модуля."; return false; }
        ResolveConnections();
        if (activeDegrees[a] >= 2 || activeDegrees[b] >= 2)
        { reason = "Предел двух активных связей на пушку."; return false; }
        reason = null; return true;
    }
    public bool Connect(int a, int b, Link kind)
    {
        if (!Editing || kind == Link.None) return false;
        if (!CanConnect(a,b,out string reason)) { Message = reason; return false; }
        var edge = FindConnection(a,b);
        if (!SetLink(edge.Id,kind)) return false;
        ConnectingFrom = -1; ConnectingModule = Link.None; Selected = edge.Id; SelectedLink = true;
        return true;
    }
    public bool IsConnectionTarget(int index)
    {
        int source = ConnectingFrom >= 0 ? ConnectingFrom : SelectedLink ? -1 : Selected;
        return Editing && source >= 0 && source != index && Cells[source].Weapon >= 0 && CanConnect(source,index,out _);
    }
    public bool SelectionTouches(int index) => SelectedLink ? Links[Selected].Touches(index) : Selected == index;
    public bool AddSegment(int index)
    {
        if (!Editing || Cells[index].Segment) return false;
        if (SegmentCount >= lab.SegmentedSegmentBudget) { Message = "Закончился запас сегментов. Удалите другой сегмент."; return false; }
        Cells[index].Segment = true; Message = "Сегмент построен. Теперь можно установить оружие."; return true;
    }
    public bool RemoveSegment(int index)
    {
        if (!Editing || !Cells[index].Segment) return false;
        RemoveWeapon(index); Cells[index].Segment = false;
        Message = "Сегмент убран. Геометрические модули сохранены и не действуют."; return true;
    }
    public bool InstallWeapon(int index, int kind)
    {
        if (!Editing || !Cells[index].Segment || Cells[index].Weapon >= 0 || kind < 0 || kind > 3) return false;
        if (WeaponRemaining(kind) <= 0) { Message = "Такого оружия больше нет в запасе."; return false; }
        if (!lab.InstallSegmentedWeapon(index / Positions, index % Positions, kind)) return false;
        Cells[index].Weapon = kind; Message = "Оружие установлено. Ствол направлен наружу."; return true;
    }
    public bool RemoveWeapon(int index)
    {
        if (!Editing || Cells[index].Weapon < 0) return false;
        if (Gun(index) != null && !station.RemoveModule(Gun(index).StableModuleId)) return false;
        Cells[index].Weapon = -1; Cells[index].IndividualSlots = 1; Cells[index].First = Cells[index].Second = "EMPTY";
        MovingFrom = ConnectingFrom = -1; Message = "Оружие возвращено в запас. Связи остались в своих ячейках."; return true;
    }
    public bool MoveWeapon(int target)
    {
        int source = MovingFrom;
        if (!Editing || source < 0 || !Cells[target].Segment || Cells[target].Weapon >= 0) { Message = "Нужен свободный построенный сегмент."; return false; }
        if (!station.MoveModule(Gun(source).StableModuleId, Mount(target).Ring.RingId, target % Positions, out string error))
        { Message = error; return false; }
        Cells[target].Weapon = Cells[source].Weapon; Cells[source].Weapon = -1;
        Mount(target).UpdatePosition(Mount(target).Ring.Phase, Mount(target).Ring.Radius);
        MovingFrom = -1; Selected = target; SelectedLink = false;
        Message = "Пушка перемещена со своими модификациями. Связи остались на месте."; return true;
    }
    public bool SetLink(int index, Link kind)
    {
        if (!Editing || !Enum.IsDefined(typeof(Link), kind)) return false;
        var edge = Links[index];
        if (kind != Link.None && !Eligible(index)) { Message = "Нужны две пушки и свободный промежуток для связи через слот."; return false; }
        if (kind != Link.None && edge.Module == Link.None && !CanConnect(edge.A,edge.B,out string reason)) { Message = reason; return false; }
        if (kind != Link.None && edge.Module != kind && LinkRemaining(kind) <= 0)
        { Message = "Такого модуля нет в запасе. Снимите его с другой связи."; return false; }
        edge.Module = kind; edge.NextExtraShot = 0f;
        ResolveConnections();
        Message = kind == Link.None ? "Модуль возвращён в запас." : edge.IsActive ? "Модуль установлен. Предыдущий возвращён в запас." : Status(index);
        return true;
    }
    int IndexOf(int weaponId)
    {
        if (!Bound || !lab.SegmentedOrbit) return -1;
        var gun = station.Modules.FirstOrDefault(g => g.StableModuleId == weaponId);
        return gun?.CurrentMount == null ? -1 : gun.CurrentMount.Ring.State.Order * Positions + gun.CurrentMount.MountIndex;
    }
    float TotalStrength(int weaponId, Link kind)
    {
        int index = IndexOf(weaponId);
        if (index < 0) return 0;
        ResolveConnections();
        float total = 0f;
        foreach (var edge in Links) if (edge.IsActive && edge.Module == kind && edge.Touches(index)) total += Strength(edge);
        return total;
    }
    public float DamageFactor(int weaponId) => 1f + .2f * TotalStrength(weaponId, Link.Damage);
    public float RateFactor(int weaponId) => 1f + .2f * TotalStrength(weaponId, Link.FireRate);
    public int Ricochets(int weaponId)
    {
        int index = IndexOf(weaponId); if (index < 0) return 0;
        ResolveConnections(); int count = 0;
        foreach (var edge in Links)
            if (edge.IsActive && edge.Module == Link.Ricochet && edge.Touches(index) &&
                (edge.Type == Geometry.Near || Cells[index].Weapon == 0)) count++;
        return count;
    }
    public void OnOrdinaryShot(int weaponId)
    {
        int index = IndexOf(weaponId);
        if (index < 0 || lab.Paused) return;
        ResolveConnections();
        foreach (var edge in Links)
            if (edge.IsActive && edge.Module == Link.ExtraShot && edge.Touches(index)) TriggerExtra(edge,edge.Other(index));
    }
    void TriggerExtra(Connection edge, int other)
    {
        if (Time.time < edge.NextExtraShot || Gun(other) is not EnemyOrbitalLabWeapon gun) return;
        // Reserve before calling the other weapon. Extra shots never publish an ordinary-shot event.
        edge.NextExtraShot = Time.time + ExtraShotInterval(edge);
        gun.FireLabExtraShot();
    }
    public void SetMotion(Motion motion)
    {
        if (!Editing) return;
        if (Bound && Movement == Motion.Rotating && motion != Motion.Rotating)
            foreach (var ring in station.Rings) { ring.State.CurrentPhase = station.Rings[0].Phase; ring.Tick(0f); }
        Movement = motion; ApplyMotion();
        Message = motion == Motion.Manual ? "Manual: Q — против часовой, E — по часовой. Отпустите для восстановления." :
            motion == Motion.Fixed ? "Fixed: направления закреплены в мире." : "Rotating: каждый уровень вращается как целое.";
    }
    public void ApplyMotion()
    {
        if (!Bound) return;
        station.DebugRotationPaused = Movement != Motion.Rotating;
        foreach (var ring in station.Rings)
        {
            ring.RuntimeDirectionMultiplier = Movement != Motion.Rotating ? 0 : 1;
            ring.LabRotationMultiplier = 1f;
        }
    }
    public void TickManualRotation(float deltaTime, bool counterClockwise, bool clockwise, bool allowed)
    {
        if (!Bound || Movement != Motion.Manual) return;
        station.TickManualRotation(deltaTime, counterClockwise, clockwise, allowed && !lab.Paused,
            lab.ManualRotationCapacity, lab.ManualRotationSpeed, lab.ManualRotationDrain, lab.ManualRotationRecovery);
    }
    public void RefreshRotationCapacity()
    { if (Bound) station.State.RotationEnergy = Mathf.Clamp(RotationEnergy,0f,lab.ManualRotationCapacity); }
    public void CaptureIndividualModules()
    {
        if (!Bound) return;
        for (int i = 0; i < Cells.Length; i++)
        {
            var gun = Gun(i); if (gun == null) continue;
            Cells[i].IndividualSlots = lab.WeaponModules.SlotCount(gun.StableModuleId);
            Cells[i].First = lab.WeaponModules.GetModule(gun.StableModuleId, 0);
            Cells[i].Second = lab.WeaponModules.GetModule(gun.StableModuleId, 1);
        }
    }
    public void Unbind() { CaptureIndividualModules(); station = null; MovingFrom = ConnectingFrom = -1; Hovered = -1; }
    public void Bind(OrbitalStationRuntime value)
    {
        station = value; station.State.LabMountLimit = Positions;
        while (station.Rings.Count < Levels) station.AddRing();
        while (station.Rings.Count > Levels) station.RemoveRing(station.Rings.Last().RingId, out _);
        foreach (var gun in station.Modules.ToArray()) station.RemoveModule(gun.StableModuleId);
        for (int level = 0; level < Levels; level++)
        {
            var ring = station.Rings[level];
            while (ring.MountCount < Positions)
            {
                if (ring.MountCount == ring.MountCapacity && !station.UpgradeRingCapacity(ring.RingId)) throw new InvalidOperationException("Lab capacity failed.");
                if (!station.AddMount(ring.RingId, out string error)) throw new InvalidOperationException(error);
            }
            ring.State.Radius = lab.SegmentedRadii[level]; ring.State.CurrentPhase = 0f;
            ring.State.BaseRotationSpeed = lab.SegmentedSpeeds[level]; ring.State.Direction = level == 1 ? -1 : 1;
            ring.State.SpeedUpgradeLevel = ring.State.VisualUpgradeLevel = ring.State.MountUpgradeLevel = 0;
            ring.LabMountPhase = (index, _) => 90f + index * 30f;
            for (int position = 0; position < Positions; position++) ring.Mounts[position].SetLocalPhase(90f + position * 30f);
            ring.Tick(.36f); ring.State.CurrentPhase = 0f; ring.Tick(0f);
        }
        ApplyMotion();
        station.State.RotationEnergy = lab.ManualRotationCapacity;
        foreach (var edge in Links) edge.NextExtraShot = 0f;
        for (int i = 0; i < Cells.Length; i++)
        {
            if (Cells[i].Weapon < 0) continue;
            if (!lab.InstallSegmentedWeapon(i / Positions, i % Positions, Cells[i].Weapon)) throw new InvalidOperationException("Lab weapon installation failed.");
            int id = Gun(i).StableModuleId;
            if (Cells[i].IndividualSlots > 1) lab.WeaponModules.UnlockSecondSlot(id);
            lab.WeaponModules.SetModule(id, 0, Cells[i].First);
            if (Cells[i].IndividualSlots > 1) lab.WeaponModules.SetModule(id, 1, Cells[i].Second);
        }
    }
    public bool LoadPreset(string name)
    {
        if (!Editing || !Presets.Contains(name)) return false;
        LoadPresetData(name);
        lab.RebuildSegmentedPreset(); Message = name + ": тот же запас оружия и связей, другая геометрия."; return true;
    }
    void LoadPresetData(string name)
    {
        Preset = name; MovingFrom = ConnectingFrom = -1; Selected = 0; SelectedLink = false;
        foreach (var edge in Links) { edge.Module = Link.None; edge.NextExtraShot = 0f; }
        foreach (var c in Cells) { c.Segment = false; c.Weapon = -1; c.IndividualSlots = 1; c.First = c.Second = "EMPTY"; }
        int[] segments, guns, edges;
        switch (name)
        {
            case "Front Arc": segments = new[] { 9,10,11,0,1,2,21,22,23,12,13,14 }; guns = new[] { 9,10,11,0,1,2 }; edges = new[] { 9,10,11,0 }; break;
            case "Split Defense": segments = new[] { 11,0,1,5,6,7,35,24,25,29,30,31 }; guns = new[] { 11,0,1,5,6,7 }; edges = new[] { 11,0,5,6 }; break;
            case "Multi-Layer": segments = new[] { 11,0,1,2,12,14,15,16,26,27,28,29 }; guns = new[] { 11,0,12,14,26,27 }; edges = Array.Empty<int>(); break;
            case "Шашечка": segments = new[] { 11,1,3,5,7,9,12,14,16,18,20,22 }; guns = new[] { 11,1,3,5,7,9 }; edges = Array.Empty<int>(); break;
            default: segments = Enumerable.Range(0,12).ToArray(); guns = new[] { 0,1,4,5,10,11 }; edges = new[] { 0,4,10,11 }; break;
        }
        foreach (int i in segments.Take(Mathf.Max(0, lab.SegmentedSegmentBudget))) Cells[i].Segment = true;
        int[] kinds = { 0,0,0,1,0,2 };
        for (int i = 0; i < guns.Length; i++) if (Cells[guns[i]].Segment && WeaponRemaining(kinds[i]) > 0) Cells[guns[i]].Weapon = kinds[i];
        for (int i = 0; i < edges.Length; i++)
        {
            Link kind = (Link)(i + 1);
            if (Eligible(edges[i]) && LinkRemaining(kind) > 0) Links[edges[i]].Module = kind;
        }
        if (name == "Multi-Layer")
            foreach (var entry in new[] { (11,0,Link.Ricochet), (12,14,Link.ExtraShot), (26,27,Link.FireRate) })
            {
                var edge = FindConnection(entry.Item1,entry.Item2);
                if (Eligible(edge) && LinkRemaining(entry.Item3) > 0) edge.Module = entry.Item3;
            }
        if (name == "Шашечка")
            for (int i = 0; i < 4; i++)
            {
                var edge = FindConnection(guns[i],guns[i+1]); Link kind = (Link)(i+1);
                if (Eligible(edge) && LinkRemaining(kind) > 0) edge.Module = kind;
            }
    }
}
#endif
