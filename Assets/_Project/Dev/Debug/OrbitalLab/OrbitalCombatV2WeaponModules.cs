#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Three lab effects only. Each entry owns independent base/runtime parameter copies.
public sealed class OrbitalCombatV2WeaponModules
{
    private sealed class Weapon
    {
        public OrbitalWeaponSettings Source, Baseline, Runtime;
        public int Slots = 1;
        public readonly string[] Modules = { "EMPTY", "EMPTY" };
    }
    private sealed class View
    {
        public GameObject Root;
        public LineRenderer Spoke;
        public readonly LineRenderer[] Slots = new LineRenderer[2], Icons = new LineRenderer[2];
    }
    private readonly EnemyOrbitalLabController lab;
    private readonly Dictionary<int, Weapon> weapons = new();
    private readonly Dictionary<(int, int), Weapon> reset = new();
    private readonly Dictionary<int, View> views = new();
    private readonly HashSet<int> alive = new();
    private static readonly Vector2[] Box = { new(-.1f,-.1f), new(.1f,-.1f), new(.1f,.1f), new(-.1f,.1f), new(-.1f,-.1f) };
    private static readonly Vector2[] Rapid = { new(-.08f,-.06f), Vector2.zero, new(-.08f,.06f), new(.08f,0), new(0,-.06f) };
    private static readonly Vector2[] Pierce = { new(-.08f,0), new(.08f,0), new(.02f,.06f), new(.08f,0), new(.02f,-.06f) };
    private static readonly Vector2[] Multi = { new(-.08f,0), Vector2.zero, new(.08f,.06f), Vector2.zero, new(.08f,-.06f) };
    private OrbitalStationRuntime station;
    public string ActivePreset { get; private set; }
    public int SelectedId { get; private set; }
    public string Message { get; private set; } = "На паузе выберите пушку ЛКМ или кнопкой ниже.";
    public bool CanEdit => station != null && lab.Ready && lab.Paused && Time.timeScale <= 0f &&
        station.InputOwner != null && station.InputOwner.IsIdle && station.InputOwner.IsRewardQueueIdle;
    public OrbitalCombatV2WeaponModules(EnemyOrbitalLabController lab) => this.lab = lab;
    internal static OrbitalWeaponSettings CopyProfile(OrbitalWeaponSettings source) =>
        JsonUtility.FromJson<OrbitalWeaponSettings>(JsonUtility.ToJson(source));

    public void Bind(OrbitalStationRuntime value) => station = value;
    public OrbitalWeaponSettings CreateProfile(int id, OrbitalWeaponSettings source)
    {
        var entry = new Weapon { Source = source, Baseline = CopyProfile(source), Runtime = CopyProfile(source) };
        var state = station?.State?.Modules.FirstOrDefault(m => m.StableModuleId == id);
        var ring = state != null ? station.State.FindRing(state.StableRingId) : null;
        if (lab.IsRestoringLabBuild && ring != null && reset.TryGetValue((ring.Order, state.MountIndex), out var saved) && ReferenceEquals(saved.Source, source))
        { entry.Slots = saved.Slots; entry.Modules[0] = saved.Modules[0]; entry.Modules[1] = saved.Modules[1]; }
        weapons[id] = entry;
        Apply(id, entry);
        return entry.Runtime;
    }

    private EnemyOrbitalLabWeapon Find(int id) => station?.Modules.OfType<EnemyOrbitalLabWeapon>().FirstOrDefault(w => w.StableModuleId == id);
    public bool HasModule(int id, string module) => weapons.TryGetValue(id, out var entry) && entry.Modules.Contains(module);
    public float? ExtraShotAngle(int id) => HasModule(id, "MULTISHOT") ? Mathf.Clamp(lab.ModuleMultishotAngle, 1f, 30f) : null;
    public int SlotCount(int id) => weapons.TryGetValue(id, out var entry) ? entry.Slots : 0;
    public string GetModule(int id, int slot) => weapons.TryGetValue(id, out var entry) && slot >= 0 && slot < entry.Slots ? entry.Modules[slot] : "EMPTY";
    public bool Select(int id)
    {
        if (!CanEdit || Find(id) == null || !weapons.ContainsKey(id)) return false;
        SelectedId = id; return true;
    }
    public bool UnlockSecondSlot(int id)
    {
        if (!CanEdit || Find(id) == null || !weapons.TryGetValue(id, out var entry)) return false;
        entry.Slots = 2; return true;
    }
    public bool SetModule(int id, int slot, string module)
    {
        if (!CanEdit || Find(id) == null || !weapons.TryGetValue(id, out var entry) || slot < 0 || slot >= entry.Slots) return false;
        if (module != "EMPTY" && module != "RAPID" && module != "PIERCE" && module != "MULTISHOT") return false;
        if (module != "EMPTY" && entry.Modules.Where((_, index) => index != slot).Contains(module))
        { Message = "Каждый тип — не более одного раза на пушку."; return false; }
        entry.Modules[slot] = module;
        Apply(id, entry);
        Message = $"#{id}, слот {slot + 1}: {module}. Изменена только эта пушка.";
        return true;
    }
    private void Apply(int id, Weapon entry)
    {
        float previous = entry.Runtime.FireInterval;
        float rapid = entry.Modules.Contains("RAPID") ? Mathf.Clamp(lab.ModuleRapidMultiplier, 1f, 5f) : 1f;
        entry.Runtime.FireInterval = entry.Baseline.FireInterval / rapid;
        entry.Runtime.BurstInterval = entry.Baseline.BurstInterval / rapid;
        entry.Runtime.BodyPierces = entry.Baseline.BodyPierces + (entry.Modules.Contains("PIERCE") ? Mathf.Clamp(lab.ModulePierceBonus, 0, 10) : 0);
        var gun = Find(id);
        if (gun != null && gun.RuntimeCooldown > 0f && previous > 0f) gun.RuntimeCooldown *= entry.Runtime.FireInterval / previous;
    }
    public void Refresh()
    {
        if (!CanEdit) return;
        foreach (var pair in weapons) Apply(pair.Key, pair.Value);
    }

    public void BeginLoadout()
    {
        ActivePreset = null; reset.Clear(); weapons.Clear(); SelectedId = 0; HideSpokes();
    }
    public void BeginPreset(string name) { BeginLoadout(); ActivePreset = name; }
    public void ClearPresetLabel() => ActivePreset = null;
    public void FinishRestore() => reset.Clear();
    public void CaptureReset()
    {
        reset.Clear();
        if (station != null)
            foreach (var gun in station.Modules.OfType<EnemyOrbitalLabWeapon>())
                if (gun.CurrentMount != null && weapons.TryGetValue(gun.StableModuleId, out var entry))
                {
                    var saved = new Weapon { Source = entry.Source, Slots = entry.Slots };
                    saved.Modules[0] = entry.Modules[0]; saved.Modules[1] = entry.Modules[1];
                    reset[(gun.CurrentMount.Ring.State.Order, gun.CurrentMount.MountIndex)] = saved;
                }
        weapons.Clear(); SelectedId = 0; HideSpokes(); station = null;
    }

    public void UpdateSpokes()
    {
        if (station == null || !lab.Ready) { HideSpokes(); return; }
        alive.Clear();
        foreach (var gun in station.Modules.OfType<EnemyOrbitalLabWeapon>())
        {
            if (gun.CurrentMount == null || !weapons.TryGetValue(gun.StableModuleId, out var entry)) continue;
            int id = gun.StableModuleId; alive.Add(id);
            if (!views.TryGetValue(id, out var view)) views[id] = view = CreateView(id);
            Vector2 center = station.transform.position, end = gun.CurrentMount.Transform.position;
            Vector2 axis = (end - center).normalized, side = new(-axis.y, axis.x);
            view.Spoke.startColor = view.Spoke.endColor = new Color(.65f, .8f, .9f, id == SelectedId ? .25f : .09f);
            view.Spoke.SetPosition(0, center); view.Spoke.SetPosition(1, end);
            for (int slot = 0; slot < 2; slot++)
            {
                bool open = slot < entry.Slots;
                view.Slots[slot].enabled = open;
                view.Icons[slot].enabled = open && entry.Modules[slot] != "EMPTY";
                if (!open) continue;
                Vector2 point = Vector2.Lerp(center, end, .44f + slot * .23f);
                string kind = entry.Modules[slot];
                Color color = kind == "RAPID" ? new Color(1f, .65f, .15f, .9f) : kind == "PIERCE" ? new Color(.15f, .85f, 1f, .9f)
                    : kind == "MULTISHOT" ? new Color(.8f, .4f, 1f, .9f) : new Color(.6f, .7f, .8f, .4f);
                view.Slots[slot].startColor = view.Slots[slot].endColor = new Color(color.r, color.g, color.b, .4f);
                for (int i = 0; i < Box.Length; i++) view.Slots[slot].SetPosition(i, point + axis * Box[i].x + side * Box[i].y);
                Vector2[] shape = kind == "RAPID" ? Rapid : kind == "PIERCE" ? Pierce : Multi;
                view.Icons[slot].startColor = view.Icons[slot].endColor = color;
                for (int i = 0; i < shape.Length; i++) view.Icons[slot].SetPosition(i, point + axis * shape[i].x + side * shape[i].y);
            }
        }
        foreach (int id in views.Keys.Where(id => !alive.Contains(id)).ToArray()) { DestroyView(views[id]); views.Remove(id); weapons.Remove(id); }
    }

    private View CreateView(int id)
    {
        var view = new View { Root = new GameObject("Weapon modules " + id) };
        view.Root.transform.SetParent(lab.transform, false);
        view.Spoke = Line(view.Root.transform, "Weapon spoke " + id, 2, .014f);
        for (int slot = 0; slot < 2; slot++)
        { view.Slots[slot] = Line(view.Root.transform, "Slot " + (slot + 1), 5, .018f); view.Icons[slot] = Line(view.Root.transform, "Module icon " + (slot + 1), 5, .024f); }
        return view;
    }
    private LineRenderer Line(Transform parent, string name, int count, float width)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = count;
        line.sharedMaterial = station.VisualMaterial; line.startWidth = line.endWidth = width;
        var core = station.GetComponent<OrbitalStationView>().Core;
        line.sortingLayerID = core.sortingLayerID; line.sortingOrder = core.sortingOrder + 1;
        return line;
    }
    private static void DestroyView(View view) { if (view.Root != null) { view.Root.SetActive(false); Object.Destroy(view.Root); } }
    public void HideSpokes() { foreach (var view in views.Values) DestroyView(view); views.Clear(); }
}
#endif
