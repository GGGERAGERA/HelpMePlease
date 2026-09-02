using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalRelocationController : MonoBehaviour
    {
        private OrbitalStationRuntime station;
        private OrbitalModuleRuntime hoveredModule;
        private OrbitalModuleRuntime draggedModule;
        private OrbitalMountRuntime sourceMount;
        private OrbitalMountRuntime targetMount;
        private float previousTimeScale = 1f;

        public bool IsDragging => draggedModule != null;

        public void Bind(OrbitalStationRuntime runtime)
        {
            station = runtime;
            draggedModule = null;
            sourceMount = null;
            targetMount = null;
            station.Interaction?.SetCursor(OrbitalCursorState.Normal);
        }

        private void Update()
        {
            if (station == null || !station.IsInitialized)
                return;
            if (IsDragging)
            {
                UpdateDrag();
                return;
            }
            UpdateHover();
        }

        private void UpdateHover()
        {
            if (station.RewardFlow?.PendingReward != null)
                return;
            OrbitalModuleRuntime next = CanStartInteraction()
                ? FindModuleAt(MouseWorld(), OrbitalPresentationConfig.Active.ModuleHitRadius)
                : null;
            if (next != hoveredModule)
            {
                hoveredModule?.SetHighlighted(false);
                hoveredModule = next;
                hoveredModule?.SetHighlighted(true);
                if (hoveredModule != null)
                {
                    station.Interaction?.ShowHint(DisplayName(hoveredModule.Kind),
                        $"Орбита {hoveredModule.CurrentMount.Ring.State.Order + 1} · " +
                        "Зажмите ЛКМ и перетащите");
                    station.Interaction?.SetCursor(OrbitalCursorState.Grabbable);
                }
                else
                {
                    station.Interaction?.ClearHint();
                    station.Interaction?.SetCursor(OrbitalCursorState.Normal);
                }
            }
            if (hoveredModule != null && Input.GetMouseButtonDown(0))
                BeginDrag(hoveredModule);
        }

        private bool CanStartInteraction()
        {
            if (Time.timeScale <= 0f || Camera.main == null ||
                Subject42DebugMenu.IsDebugMenuOpen || station.IsDebugPlacementActive ||
                station.RewardFlow?.PendingReward != null || PointerOverInteractiveUi())
                return false;
            if (UpgradeManager.Instance != null && UpgradeManager.Instance.IsChoosingUpgrade)
                return false;
            return true;
        }

        private void BeginDrag(OrbitalModuleRuntime module)
        {
            draggedModule = module;
            sourceMount = module.CurrentMount;
            targetMount = null;
            previousTimeScale = Time.timeScale;
            Time.timeScale = Mathf.Min(previousTimeScale,
                OrbitalPresentationConfig.Active.RelocationTimeScale);
            module.BeginPresentationDrag(station.RuntimeRoot);
            SetAllMounts(OrbitalMountRuntime.VisualState.Invalid, freeOnly: false);
            station.Interaction?.ShowHint(DisplayName(module.Kind),
                "Перетащите на зелёное крепление\nEsc / ПКМ — отменить");
            station.Interaction?.SetCursor(OrbitalCursorState.Dragging);
        }

        private void UpdateDrag()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1) ||
                station.RewardFlow?.PendingReward != null ||
                (Time.timeScale <= 0f && previousTimeScale > 0f))
            {
                if (Time.timeScale <= 0f && previousTimeScale > 0f)
                    previousTimeScale = 0f;
                CancelDrag("cancelled");
                return;
            }

            Vector2 world = MouseWorld();
            draggedModule.SetDragPosition(world);
            targetMount = FindMountOnRingPath(world,
                OrbitalPresentationConfig.Active.MountHitRadius);
            bool valid = targetMount != null && targetMount != sourceMount &&
                !targetMount.Occupied;
            draggedModule.SetDragValidity(valid);
            station.Interaction?.SetCursor(valid
                ? OrbitalCursorState.ValidDrop
                : OrbitalCursorState.InvalidDrop);
            station.Interaction?.ShowHint(DisplayName(draggedModule.Kind), valid
                ? $"Отпустить: орбита {targetMount.Ring.State.Order + 1}"
                : "Найдите свободное зелёное крепление\nEsc / ПКМ — отменить");
            RefreshMountStates();
            if (draggedModule is OrbitalLinkNodeModule)
            {
                OrbitalLinkNodeModule partner = FindLinkPartner(draggedModule.StableModuleId);
                if (partner?.CurrentMount != null)
                    station.FlashLink(world, partner.CurrentMount.Transform.position,
                        new Color(0.85f, 0.3f, 1f, 0.42f), 0.05f);
            }
            if (!Input.GetMouseButtonUp(0))
                return;
            if (targetMount == null || targetMount == sourceMount || targetMount.Occupied)
            {
                CancelDrag("invalid target");
                return;
            }
            int id = draggedModule.StableModuleId;
            int ringId = targetMount.Ring.RingId;
            int mountIndex = targetMount.MountIndex;
            SetAllMounts(OrbitalMountRuntime.VisualState.Normal, freeOnly: false);
            RestoreTimeScale();
            draggedModule.SetHighlighted(false);
            draggedModule = null;
            sourceMount = null;
            targetMount = null;
            station.Interaction?.ClearHint();
            station.Interaction?.SetCursor(OrbitalCursorState.Normal);
            if (!station.MoveModule(id, ringId, mountIndex, out string error))
                Debug.LogWarning($"[OrbitalStation] Relocation rejected: {error}", station);
        }

        public void CancelDrag(string reason)
        {
            if (draggedModule == null)
                return;
            draggedModule.CancelPresentationDrag();
            draggedModule.SetHighlighted(false);
            SetAllMounts(OrbitalMountRuntime.VisualState.Normal, freeOnly: false);
            draggedModule = null;
            sourceMount = null;
            targetMount = null;
            RestoreTimeScale();
            station.Interaction?.ClearHint();
            station.Interaction?.SetCursor(OrbitalCursorState.Normal);
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = previousTimeScale;
            previousTimeScale = 1f;
        }

        private void RefreshMountStates()
        {
            for (int r = 0; r < station.Rings.Count; r++)
                for (int m = 0; m < station.Rings[r].Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = station.Rings[r].Mounts[m];
                    if (mount == sourceMount)
                        mount.SetVisualState(OrbitalMountRuntime.VisualState.Occupied);
                    else if (mount == targetMount)
                        mount.SetVisualState(mount.Occupied
                            ? OrbitalMountRuntime.VisualState.Invalid
                            : OrbitalMountRuntime.VisualState.Valid);
                    else
                        mount.SetVisualState(mount.Occupied
                            ? OrbitalMountRuntime.VisualState.Invalid
                            : OrbitalMountRuntime.VisualState.Valid);
                }
        }

        private void SetAllMounts(OrbitalMountRuntime.VisualState state, bool freeOnly)
        {
            for (int r = 0; r < station.Rings.Count; r++)
                for (int m = 0; m < station.Rings[r].Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = station.Rings[r].Mounts[m];
                    mount.SetVisualState(mount.Occupied
                        ? OrbitalMountRuntime.VisualState.Occupied
                        : state);
                }
        }

        private OrbitalModuleRuntime FindModuleAt(Vector2 world, float padding)
        {
            OrbitalModuleRuntime best = null;
            float bestDistance = float.MaxValue;
            for (int i = station.Modules.Count - 1; i >= 0; i--)
            {
                OrbitalModuleRuntime module = station.Modules[i];
                if (!module.HitTest(world, padding))
                    continue;
                float distance = (module.WorldPosition - world).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = module;
                }
            }
            return best;
        }

        private OrbitalMountRuntime FindMountOnRingPath(Vector2 world,
            float ringTolerance)
        {
            OrbitalMountRuntime best = null;
            float bestRingDistance = ringTolerance;
            for (int r = station.Rings.Count - 1; r >= 0; r--)
            {
                OrbitalRingRuntime ring = station.Rings[r];
                float distanceToRing = Mathf.Abs(Vector2.Distance(transform.position,
                    world) - ring.Radius);
                if (distanceToRing >= bestRingDistance)
                    continue;
                OrbitalMountRuntime nearest = null;
                float nearestSqr = float.MaxValue;
                for (int m = ring.Mounts.Count - 1; m >= 0; m--)
                {
                    OrbitalMountRuntime mount = ring.Mounts[m];
                    if (mount.Occupied)
                        continue;
                    float distance = ((Vector2)mount.Transform.position - world).sqrMagnitude;
                    if (distance < nearestSqr)
                    {
                        nearestSqr = distance;
                        nearest = mount;
                    }
                }
                if (nearest != null)
                {
                    bestRingDistance = distanceToRing;
                    best = nearest;
                }
            }
            return best;
        }

        private static bool PointerOverInteractiveUi()
        {
            if (EventSystem.current == null)
                return false;
            PointerEventData pointer = new(EventSystem.current)
            {
                position = Input.mousePosition
            };
            List<RaycastResult> hits = new();
            EventSystem.current.RaycastAll(pointer, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                GameObject hit = hits[i].gameObject;
                if (hit != null && (hit.GetComponentInParent<Selectable>() != null ||
                    hit.GetComponentInParent<ScrollRect>() != null))
                    return true;
            }
            return false;
        }

        private OrbitalLinkNodeModule FindLinkPartner(int stableId)
        {
            OrbitalLinkNodeModule pending = null;
            for (int i = 0; i < station.Modules.Count; i++)
            {
                if (station.Modules[i] is not OrbitalLinkNodeModule node)
                    continue;
                if (pending == null)
                    pending = node;
                else
                {
                    if (pending.StableModuleId == stableId)
                        return node;
                    if (node.StableModuleId == stableId)
                        return pending;
                    pending = null;
                }
            }
            return null;
        }

        private static Vector2 MouseWorld()
        {
            Vector3 point = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(point.x, point.y);
        }

        private static string DisplayName(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => "PISTOL",
            OrbitalModuleKind.LaserSword => "LASER SWORD",
            OrbitalModuleKind.ImpulseGun => "IMPULSE GUN",
            OrbitalModuleKind.ArcEmitter => "ARC EMITTER",
            OrbitalModuleKind.LinkNode => "LINK NODE",
            _ => kind.ToString().ToUpperInvariant()
        };

        private void OnDisable()
        {
            CancelDrag("controller disabled");
            hoveredModule?.SetHighlighted(false);
            hoveredModule = null;
            station?.Interaction?.ClearHint();
            station?.Interaction?.SetCursor(OrbitalCursorState.Normal);
        }
    }
}
