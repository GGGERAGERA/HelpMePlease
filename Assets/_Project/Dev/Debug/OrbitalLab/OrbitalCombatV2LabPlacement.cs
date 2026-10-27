#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Lab-only angles on the existing mounts. No loadout/save format or firing clocks.
public sealed class OrbitalCombatV2LabPlacement
{
    public const float MinimumGap = .08f;
    private readonly EnemyOrbitalLabController lab;
    // Ring order / mount index survives a lab reset into a freshly created run.
    private readonly Dictionary<(int, int), float> angles = new();
    private readonly Dictionary<(int, int), float> custom = new();
    private readonly HashSet<int> pinnedRings = new();
    private readonly Dictionary<int, int> mountCounts = new();
    private readonly Dictionary<int, float> envelopes = new();
    private readonly Dictionary<OrbitalRingRuntime, Func<int, float, float>> owners = new();
    private OrbitalStationRuntime station;
    private OrbitalModuleRuntime selected;
    private LineRenderer outline, arrow;
    public bool IsComparison { get; private set; }
    public string PresetName { get; private set; } = "CUSTOM";
    public string Message { get; private set; } = "P: пауза → ЛКМ по оружию → позиция → ЛКМ. Esc: отмена.";
    public bool PreviewValid { get; private set; }
    public float PreviewAngle { get; private set; }
    public Vector2 PreviewPosition { get; private set; }
    public Vector2 PreviewDirection { get; private set; }
    public bool HasSelection => selected != null;
    public int SelectedModuleId => selected?.StableModuleId ?? 0;
    private bool CanEdit => station != null && lab.Ready && lab.Paused && Time.timeScale <= 0f &&
        station.InputOwner != null && station.InputOwner.IsIdle && station.InputOwner.IsRewardQueueIdle;

    public OrbitalCombatV2LabPlacement(EnemyOrbitalLabController lab) => this.lab = lab;

    public void Bind(OrbitalStationRuntime value)
    {
        if (!ReferenceEquals(station, value)) { Unbind(); station = value; }
        Synchronize();
    }

    public void Synchronize()
    {
        if (station == null) return;
        foreach (var ring in station.Rings)
        {
            mountCounts.TryGetValue(ring.State.Order, out int previousCount);
            mountCounts[ring.State.Order] = Mathf.Max(previousCount, ring.MountCount);
            if (!owners.ContainsKey(ring))
            {
                if (ring.LabMountPhase != null) throw new InvalidOperationException("Lab mount angles already have an owner.");
                Func<int, float, float> resolve = (index, fallback) => Resolve(ring, index, fallback);
                owners.Add(ring, resolve);
                ring.LabMountPhase = resolve;
            }
            foreach (var mount in ring.Mounts)
            {
                var key = (ring.State.Order, mount.MountIndex);
                if (!angles.TryGetValue(key, out float angle)) angles[key] = angle = mount.LocalPhase;
                if (!Mathf.Approximately(mount.LocalPhase, angle)) SetAngle(mount, angle);
                if (mount.Module != null)
                {
                    float extent = Mathf.Max(.05f, mount.Module.PresentationReach);
                    envelopes.TryGetValue(mount.Module.StableModuleId, out float previous);
                    envelopes[mount.Module.StableModuleId] = Mathf.Max(previous, extent);
                }
            }
        }
    }

    private float Resolve(OrbitalRingRuntime ring, int index, float fallback)
    {
        var key = (ring.State.Order, index);
        if (!pinnedRings.Contains(ring.State.Order)) { angles[key] = fallback; return fallback; }
        if (angles.TryGetValue(key, out float remembered)) return remembered;
        var existing = ring.Mounts.Where(m => m.MountIndex != index).Select(m => m.LocalPhase).OrderBy(a => a).ToArray();
        float largest = -1f, result = fallback;
        for (int i = 0; i < existing.Length; i++)
        {
            float next = existing[(i + 1) % existing.Length] + (i == existing.Length - 1 ? 360f : 0f);
            float gap = next - existing[i];
            if (gap > largest) { largest = gap; result = Mathf.Repeat(existing[i] + gap * .5f, 360f); }
        }
        angles[key] = result;
        return result;
    }

    private float Envelope(OrbitalModuleRuntime module) => envelopes.TryGetValue(module.StableModuleId, out float radius)
        ? radius : Mathf.Max(.05f, module.PresentationReach);

    private bool ClearAt(OrbitalMountRuntime moving, float angle)
    {
        if (moving.Module == null) return false;
        foreach (var other in moving.Ring.Mounts)
        {
            if (other == moving || other.Module == null) continue;
            float chord = 2f * moving.Ring.Radius * Mathf.Sin(Mathf.Abs(Mathf.DeltaAngle(angle, other.LocalPhase)) * Mathf.Deg2Rad * .5f);
            if (chord + .00001f < Envelope(moving.Module) + Envelope(other.Module) + MinimumGap) return false;
        }
        return true;
    }

    private void SetAngle(OrbitalMountRuntime mount, float angle)
    {
        angle = Mathf.Repeat(angle, 360f);
        angles[(mount.Ring.State.Order, mount.MountIndex)] = angle;
        mount.SetLocalPhase(angle);
        mount.UpdatePosition(mount.Ring.Phase, mount.Ring.Radius);
    }

    public bool TrySelect(int moduleId)
    {
        if (!CanEdit) return false;
        var module = station.Modules.FirstOrDefault(m => m.StableModuleId == moduleId);
        if (module?.CurrentMount == null || module.CurrentMount.Ring.Geometry.Type != OrbitalPathType.Circle) return false;
        Cancel(); Synchronize();
        selected = module; selected.SetHighlighted(true);
        PreviewWorld(selected.CurrentMount.Transform.position);
        return true;
    }

    public void PreviewWorld(Vector2 world)
    {
        PreviewValid = false;
        if (!CanEdit || selected?.CurrentMount == null) { HidePreview(); return; }
        Vector2 offset = world - (Vector2)station.transform.position;
        if (offset.sqrMagnitude < .001f) { HidePreview(); return; }
        var ring = selected.CurrentMount.Ring;
        PreviewDirection = offset.normalized;
        PreviewAngle = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg - ring.Phase, 360f);
        PreviewPosition = (Vector2)station.transform.position + PreviewDirection * ring.Radius;
        PreviewValid = ClearAt(selected.CurrentMount, PreviewAngle);
        Message = $"R{ring.State.Order + 1} / M{selected.CurrentMount.MountIndex + 1}: {PreviewAngle:0.0}° — " +
            (PreviewValid ? "ЛКМ: подтвердить; Esc: отмена" : "Мало места: нужен зазор 0,08 м между габаритами.");
        DrawPreview();
    }

    public bool Confirm()
    {
        if (!CanEdit || selected?.CurrentMount == null || !PreviewValid || !ClearAt(selected.CurrentMount, PreviewAngle)) return false;
        pinnedRings.Add(selected.CurrentMount.Ring.State.Order);
        SetAngle(selected.CurrentMount, PreviewAngle);
        PresetName = "CUSTOM";
        foreach (var mount in selected.CurrentMount.Ring.Mounts.Where(m => m.Occupied))
            custom[(mount.Ring.State.Order, mount.MountIndex)] = mount.LocalPhase;
        Message = $"Установлено: {PreviewAngle:0.0}°. P — продолжить бой.";
        Cancel();
        return true;
    }

    public void LeaveComparison() { IsComparison = false; PresetName = "CUSTOM"; Cancel(); }
    public void BeginComparison() => IsComparison = true;
    public int RememberedMountCount(int order) => mountCounts.TryGetValue(order, out int count) ? count : 0;

    public bool ArrangeModuleComparison(int count)
    {
        if (!CanEdit || count < 1 || count > 4) return false;
        Synchronize(); Cancel();
        var mounts = station.Rings[0].Mounts.Where(m => m.Occupied).ToArray();
        if (mounts.Length != count) return false;
        // All three module presets use the same evenly spaced reference geometry.
        for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
            {
                float chord = 2f * mounts[a].Ring.Radius * Mathf.Sin(Mathf.Abs(Mathf.DeltaAngle(a * 360f / count, b * 360f / count)) * Mathf.Deg2Rad * .5f);
                if (chord < Envelope(mounts[a].Module) + Envelope(mounts[b].Module) + MinimumGap) return false;
            }
        pinnedRings.Add(mounts[0].Ring.State.Order);
        for (int i = 0; i < count; i++) SetAngle(mounts[i], i * 360f / count);
        PresetName = "CUSTOM";
        return true;
    }

    public bool ApplyPreset(string name)
        => station != null && station.Rings[0].Mounts.Count(m => m.Occupied) == 4 && ApplyRingLayout(station.Rings[0], name);

    public void ReindexAfterRingRemoval(IReadOnlyDictionary<OrbitalRingRuntime, int> previousOrders)
    {
        var previousCustom = new Dictionary<(int, int), float>(custom);
        Cancel(); angles.Clear(); custom.Clear(); pinnedRings.Clear(); mountCounts.Clear();
        foreach (var removed in owners.Keys.Where(r => !station.Rings.Contains(r)).ToArray())
        { if (removed.LabMountPhase == owners[removed]) removed.LabMountPhase = null; owners.Remove(removed); }
        foreach (var ring in station.Rings)
        {
            pinnedRings.Add(ring.State.Order);
            foreach (var mount in ring.Mounts)
            {
                angles[(ring.State.Order, mount.MountIndex)] = mount.LocalPhase;
                custom[(ring.State.Order, mount.MountIndex)] = previousCustom.TryGetValue((previousOrders[ring], mount.MountIndex), out float saved) ? saved : mount.LocalPhase;
            }
        }
        IsComparison = false; PresetName = "CUSTOM"; Synchronize();
    }

    public bool ApplyRingLayout(OrbitalRingRuntime ring, string name)
    {
        if (!CanEdit || name != "EVEN" && name != "GROUPED" && name != "CUSTOM") return false;
        Synchronize(); Cancel();
        if (ring == null || !station.Rings.Contains(ring)) return false;
        var mounts = ring.Mounts.Where(m => m.Occupied).ToArray();
        if (mounts.Length == 0) return false;
        float[] values = Enumerable.Range(0, mounts.Length).Select(i => i * 360f / mounts.Length).ToArray();
        if (name == "GROUPED")
        {
            float radius = mounts.Max(m => Envelope(m.Module));
            float ratio = (2f * radius + MinimumGap) / (2f * mounts[0].Ring.Radius);
            if (ratio >= 1f) { Message = "Оружие не помещается с этим зазором."; return false; }
            float step = 2f * Mathf.Asin(ratio) * Mathf.Rad2Deg + .25f;
            for (int i = 0; i < values.Length; i++) values[i] = step * i;
        }
        else if (name == "CUSTOM")
            for (int i = 0; i < mounts.Length; i++)
                if (custom.TryGetValue((mounts[i].Ring.State.Order, mounts[i].MountIndex), out float remembered)) values[i] = remembered;
        for (int a = 0; a < mounts.Length; a++)
            for (int b = a + 1; b < mounts.Length; b++)
            {
                float chord = 2f * mounts[a].Ring.Radius * Mathf.Sin(Mathf.Abs(Mathf.DeltaAngle(values[a], values[b])) * Mathf.Deg2Rad * .5f);
                if (chord + .00001f < Envelope(mounts[a].Module) + Envelope(mounts[b].Module) + MinimumGap)
                { Message = "Пресет не помещается с текущими габаритами; углы сохранены."; return false; }
            }
        pinnedRings.Add(mounts[0].Ring.State.Order);
        for (int i = 0; i < mounts.Length; i++) SetAngle(mounts[i], values[i]);
        PresetName = name;
        Message = "Расположение изменено. P — продолжить бой.";
        return true;
    }

    public void TickPointer(Camera camera, bool blocked)
    {
        if (!CanEdit || !Application.isFocused) { Cancel(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
        if (blocked || camera == null) { HidePreview(); return; }
        Vector3 screen = Input.mousePosition; screen.z = -camera.transform.position.z;
        Vector2 world = camera.ScreenToWorldPoint(screen);
        if (selected != null)
        {
            PreviewWorld(world);
            if (Input.GetMouseButtonDown(0)) Confirm();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            var hit = station.Modules.Where(m => m.CurrentMount != null && m.HitTest(world, .08f))
                .OrderBy(m => Vector2.SqrMagnitude(m.WorldPosition - world)).FirstOrDefault();
            if (hit != null) TrySelect(hit.StableModuleId);
        }
    }

    private void DrawPreview()
    {
        if (outline == null) { outline = CreateLine("Lab placement footprint", 33); arrow = CreateLine("Lab fixed muzzle preview", 5); }
        Color color = PreviewValid ? new Color(.2f, 1f, .5f, .9f) : new Color(1f, .25f, .15f, .9f);
        outline.enabled = arrow.enabled = true;
        outline.startColor = outline.endColor = arrow.startColor = arrow.endColor = color;
        float radius = Envelope(selected);
        for (int i = 0; i < 33; i++)
        {
            float radians = i * Mathf.PI / 16f;
            outline.SetPosition(i, PreviewPosition + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
        }
        Vector2 tip = PreviewPosition + PreviewDirection * Mathf.Max(.8f, radius * 1.4f);
        Vector2 side = new(-PreviewDirection.y, PreviewDirection.x);
        arrow.SetPosition(0, PreviewPosition); arrow.SetPosition(1, tip);
        arrow.SetPosition(2, tip - PreviewDirection * .22f + side * .14f); arrow.SetPosition(3, tip);
        arrow.SetPosition(4, tip - PreviewDirection * .22f - side * .14f);
    }

    private LineRenderer CreateLine(string name, int count)
    {
        var go = new GameObject(name); go.transform.SetParent(lab.transform, false);
        var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = count;
        line.sharedMaterial = station.VisualMaterial; line.startWidth = line.endWidth = .035f;
        var core = station.GetComponent<OrbitalStationView>().Core;
        line.sortingLayerID = core.sortingLayerID; line.sortingOrder = core.sortingOrder + 100;
        return line;
    }

    private void HidePreview() { if (outline != null) outline.enabled = false; if (arrow != null) arrow.enabled = false; }
    public void Cancel() { selected?.SetHighlighted(false); selected = null; PreviewValid = false; HidePreview(); }

    public void Unbind()
    {
        Cancel();
        foreach (var owner in owners)
            if (owner.Key.LabMountPhase == owner.Value) owner.Key.LabMountPhase = null;
        owners.Clear(); envelopes.Clear(); station = null;
        if (outline != null) { outline.gameObject.SetActive(false); UnityEngine.Object.Destroy(outline.gameObject); }
        if (arrow != null) { arrow.gameObject.SetActive(false); UnityEngine.Object.Destroy(arrow.gameObject); }
        outline = arrow = null;
    }
}
#endif
