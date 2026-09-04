using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalRewardFlowState
    {
        CardSelection, RingSelection, ModuleSelection, DirectMountSelection, ModuleFlight,
        SecondLinkPlacement, Applying, Completed, Cancelled
    }

    [DisallowMultipleComponent]
    public sealed class OrbitalRewardFlowController : MonoBehaviour
    {
        private readonly OrbitalMountHoverResolver mountResolver = new();
        private OrbitalStationRuntime station;
        private OrbitalRewardData reward;
        private OrbitalRingRuntime hoveredRing;
        private OrbitalModuleRuntime hoveredModule;
        private OrbitalMountRuntime hoveredMount;
        private OrbitalMountRuntime reservedMount;
        private OrbitalModuleVisual modulePreview;
        private GameObject addMountPreview;
        private Action completed;
        private Action cancelled;
        private int firstRingId, firstMountIndex, targetRingId, targetMountIndex;
        private OrbitalModuleVisual firstLinkPreview;
        private LineRenderer linkPreviewLine;
        private ulong sessionToken, flightToken;
        private bool terminal = true;
        private bool committed;
        public ulong SessionToken => sessionToken;
        private Coroutine flightRoutine;

        public OrbitalRewardFlowState State { get; private set; } =
            OrbitalRewardFlowState.CardSelection;
        public OrbitalRewardKind? PendingReward => reward != null
            ? reward.RewardKind : null;
        public string CompactStatus => reward == null
            ? State.ToString() : $"{State}:{reward.RewardKind}";
        public string CompactTargetStatus => hoveredMount == null
            ? "NONE"
            : $"R{hoveredMount.Ring.State.Order + 1}:M{hoveredMount.MountIndex + 1}:" +
              (!station.IsMountFree(hoveredMount) ? "OCCUPIED" : "FREE");

        public void Bind(OrbitalStationRuntime runtime) => station = runtime;

        public bool Begin(OrbitalRewardData selected,
            Action onCompleted, Action onCancelled)
        {
            if (selected == null || station == null ||
                !station.IsInitialized || reward != null || !station.InputOwner.BeginReward())
                return false;
            sessionToken++;
            terminal = false;
            committed = false;
            reward = selected;
            completed = onCompleted;
            cancelled = onCancelled;
            hoveredRing = null;
            hoveredModule = null;
            hoveredMount = null;
            reservedMount = null;
            firstRingId = 0;
            if (!selected.RequiresArenaSelection)
                return ApplyImmediate();
            if (IsRingReward(selected.RewardKind))
                State = OrbitalRewardFlowState.RingSelection;
            else if (selected.RewardKind == OrbitalRewardKind.ModuleDamage)
                State = OrbitalRewardFlowState.ModuleSelection;
            else
            {
                State = OrbitalRewardFlowState.DirectMountSelection;
                CreateModulePreview();
            }
            RefreshArenaPresentation();
            return true;
        }

        internal void AbortForRestoreFailure()
        {
            CancelToCards();
            station = null;
        }

        public void CancelForSceneTransition()
        {
            if (reward != null)
                CancelToCards();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugChooseRing(int stableRingId)
        {
            hoveredRing = station?.Rings.FirstOrDefault(value =>
                value.RingId == stableRingId);
            if (hoveredRing == null || State != OrbitalRewardFlowState.RingSelection)
                return false;
            SelectRingUpgrade();
            return true;
        }

        public bool DebugChooseMount(int stableRingId, int mountIndex)
        {
            OrbitalRingRuntime ring = station?.Rings.FirstOrDefault(value =>
                value.RingId == stableRingId);
            if (ring == null || mountIndex < 0 || mountIndex >= ring.Mounts.Count ||
                (State != OrbitalRewardFlowState.DirectMountSelection &&
                 State != OrbitalRewardFlowState.SecondLinkPlacement))
                return false;
            hoveredMount = ring.Mounts[mountIndex];
            ConfirmDirectMount(State == OrbitalRewardFlowState.SecondLinkPlacement);
            return true;
        }

        public bool DebugChooseModule(int stableModuleId)
        {
            hoveredModule = station?.Modules.FirstOrDefault(value =>
                value.StableModuleId == stableModuleId);
            if (hoveredModule == null ||
                State != OrbitalRewardFlowState.ModuleSelection)
                return false;
            SelectModuleUpgrade();
            return true;
        }
#endif

        private void LateUpdate()
        {
            if (reward != null && station != null) DrawSecondLinkPreview();
        }

        private void Update()
        {
            if (reward == null || station == null || !station.IsInitialized ||
                !station.InputOwner.CanConsumeRewardPointer)
                return;
            if (State == OrbitalRewardFlowState.ModuleFlight ||
                State == OrbitalRewardFlowState.Applying)
                return;
            UpdateHover();
            UpdateModulePreview();
            RefreshArenaPresentation();
            DrawSecondLinkPreview();
            if (!Input.GetMouseButtonDown(0) || PointerOverInteractiveUi())
                return;
            if (State == OrbitalRewardFlowState.RingSelection)
                SelectRingUpgrade();
            else if (State == OrbitalRewardFlowState.ModuleSelection)
                SelectModuleUpgrade();
            else if (State == OrbitalRewardFlowState.DirectMountSelection)
                ConfirmDirectMount(false);
            else if (State == OrbitalRewardFlowState.SecondLinkPlacement)
                ConfirmDirectMount(true);
        }

        private void SelectRingUpgrade()
        {
            if (hoveredRing == null || !CanUseRing(hoveredRing))
                return;
            State = OrbitalRewardFlowState.Applying;
            int ringId = hoveredRing.RingId;
            bool applied = reward.RewardKind switch
            {
                OrbitalRewardKind.RingSpeed => station.UpgradeRingSpeed(ringId),
                OrbitalRewardKind.RingPower => station.UpgradeRingPower(ringId),
                OrbitalRewardKind.AddMount => ApplyAddMount(ringId),
                _ => false
            };
            if (!applied)
            {
                State = OrbitalRewardFlowState.RingSelection;
                return;
            }
            committed = true;
            if (!station.IsInitialized)
            {
                CompleteReward();
                return;
            }
            station.Rings.FirstOrDefault(value => value.RingId == ringId)?.Pulse();
            station.FlashCore(new Color(0.35f, 0.9f, 1f));
            CompleteReward();
        }

        private void ConfirmDirectMount(bool secondLink)
        {
            if (hoveredMount == null || !CanStageMount(hoveredMount))
                return;
            reservedMount = hoveredMount;
            targetRingId = hoveredMount.Ring.RingId;
            targetMountIndex = hoveredMount.MountIndex;
            StartFlight(secondLink);
        }

        private void SelectModuleUpgrade()
        {
            if (hoveredModule == null ||
                !station.State.CanUpgradeModuleDamage(hoveredModule.StableModuleId, out _))
                return;
            State = OrbitalRewardFlowState.Applying;
            if (!station.UpgradeModuleDamage(hoveredModule.StableModuleId))
            {
                State = OrbitalRewardFlowState.ModuleSelection;
                return;
            }
            committed = true;
            CompleteReward();
        }

        private void StartFlight(bool secondLink)
        {
            if (linkPreviewLine != null) linkPreviewLine.enabled = false;
            State = OrbitalRewardFlowState.ModuleFlight;
            station.InputOwner.SetRewardPhase(OrbitalInteractionMode.RewardFlight);
            ClearArenaVisuals();
            station.Interaction?.ClearHint();
            station.Interaction?.SetCursor(OrbitalCursorState.Normal);
            if (flightRoutine != null)
                StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(PlayFlight(secondLink, sessionToken, ++flightToken));
        }

        private IEnumerator PlayFlight(bool secondLink, ulong session, ulong flight)
        {
            OrbitalMountRuntime destinationMount = reservedMount;
            Vector3 destination = destinationMount.Transform != null ? destinationMount.Transform.position : station.transform.position;
            Vector3 origin = station.transform.position;
            float elapsed = 0f;
            const float duration = 0.32f;
            modulePreview?.SetPreviewState(true);
            while (session == sessionToken && flight == flightToken && !terminal &&
                elapsed < duration && modulePreview != null &&
                station != null && destinationMount.Transform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                destination = destinationMount.Transform.position;
                modulePreview.SetWorldPosition(Vector3.LerpUnclamped(origin,
                    destination, 1f - Mathf.Pow(1f - t, 3f)));
                modulePreview.Tick();
                yield return null;
            }
            FinishFlight(session, flight, secondLink);
        }

        private void FinishFlight(ulong session, ulong flight, bool secondLink)
        {
            if (terminal || reward == null || session != sessionToken || flight != flightToken ||
                State != OrbitalRewardFlowState.ModuleFlight) return;
            // Consume this callback before any state operation or external callback.
            flightToken++;
            flightRoutine = null;
            if (station == null || station.Owner == null || station.Owner.IsDead)
            { CancelToCards(); return; }
            if (reward.RewardKind == OrbitalRewardKind.LinkPair && !secondLink)
            {
                if (!station.State.IsMountFree(targetRingId, targetMountIndex))
                { ResumeMountSelection(false, "Крепление стало недоступно"); return; }
                firstRingId = targetRingId;
                firstMountIndex = targetMountIndex;
                firstLinkPreview = modulePreview;
                modulePreview = null;
                reservedMount = null;
                ResumeMountSelection(true, "Выберите второе крепление");
                return;
            }
            DestroyModulePreview();
            State = OrbitalRewardFlowState.Applying;
            string error;
            bool applied = reward.RewardKind == OrbitalRewardKind.LinkPair
                ? station.InstallLinkPair(firstRingId, firstMountIndex, targetRingId, targetMountIndex, out error)
                : station.InstallModule(ToModuleKind(reward.RewardKind), targetRingId, targetMountIndex, out error);
            if (!applied)
            {
                ResumeMountSelection(secondLink, "Крепление стало недоступно — выберите другое");
                return;
            }
            committed = true;
            // State commit succeeded even if incremental presentation subsequently failed.
            try
            {
                if (station.IsInitialized)
                {
                    station.Rings.FirstOrDefault(r => r.RingId == targetRingId)?.Pulse();
                    station.FlashCore(reward.RewardKind == OrbitalRewardKind.LinkPair
                        ? new Color(0.85f, 0.3f, 1f) : new Color(0.35f, 0.95f, 1f));
                }
            }
            finally { CompleteReward(); }
        }

        private bool CanStageMount(OrbitalMountRuntime mount)
        {
            if (mount == null || reward == null) return false;
            if (State == OrbitalRewardFlowState.SecondLinkPlacement)
                return station.State.CanInstallLinkPair(firstRingId, firstMountIndex,
                    mount.Ring.RingId, mount.MountIndex, out _);
            return station.State.CanInstallModule(ToModuleKind(reward.RewardKind),
                mount.Ring.RingId, mount.MountIndex, out _);
        }

        private void ResumeMountSelection(bool secondLink, string hint)
        {
            reservedMount = null;
            hoveredMount = null;
            State = secondLink ? OrbitalRewardFlowState.SecondLinkPlacement :
                OrbitalRewardFlowState.DirectMountSelection;
            station?.InputOwner?.SetRewardPhase(secondLink ? OrbitalInteractionMode.RewardSecondTarget : OrbitalInteractionMode.RewardSelection);
            CreateModulePreview();
            station?.Interaction?.ShowHint(reward?.upgradeName, hint);
        }

        private bool ApplyImmediate()
        {
            State = OrbitalRewardFlowState.Applying;
            bool applied = reward.RewardKind switch
            {
                OrbitalRewardKind.CoreUpgrade => ApplyCore(),
                OrbitalRewardKind.LinkMatrix => station.UpgradeLinkMatrix(),
                _ => false
            };
            if (!applied)
            {
                State = OrbitalRewardFlowState.Cancelled;
                Clear(false);
                return false;
            }
            committed = true;
            CompleteReward();
            return true;
        }

        private bool ApplyCore()
        {
            return station.UpgradeCore();
        }

        private void CancelToCards()
        {
            if (terminal) return;
            if (committed) { CompleteReward(); return; }
            if (flightRoutine != null)
            {
                StopCoroutine(flightRoutine);
                flightRoutine = null;
            }
            DestroyModulePreview();
            reservedMount = null;
            State = OrbitalRewardFlowState.Cancelled;
            Clear(false);
        }

        private void CompleteReward()
        {
            if (terminal) return;
            State = OrbitalRewardFlowState.Completed;
            Clear(true);
        }

        private void Clear(bool success)
        {
            if (terminal) return;
            terminal = true;
            flightToken++;
            station?.InputOwner?.EndReward();
            firstLinkPreview?.Teardown();
            firstLinkPreview = null;
            if (linkPreviewLine != null) Destroy(linkPreviewLine.gameObject);
            linkPreviewLine = null;
            Action callback = success ? completed : cancelled;
            DestroyModulePreview();
            ClearArenaVisuals();
            station?.Interaction?.ClearHint();
            station?.Interaction?.SetCursor(OrbitalCursorState.Normal);
            reward = null;
            hoveredRing = null;
            hoveredModule = null;
            hoveredMount = null;
            reservedMount = null;
            completed = null;
            cancelled = null;
            firstRingId = 0;
            callback?.Invoke();
        }

        private void UpdateHover()
        {
            hoveredRing = null;
            if (Camera.main == null)
            {
                hoveredMount = null;
                return;
            }
            if (State == OrbitalRewardFlowState.RingSelection)
            {
                hoveredMount = null;
                Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2 local = transform.InverseTransformPoint(world);
                float best = 0.34f;
                for (int i = 0; i < station.Rings.Count; i++)
                {
                    OrbitalRingRuntime ring = station.Rings[i];
                    float delta = Mathf.Abs(local.magnitude - ring.Radius);
                    if (delta < best && CanUseRing(ring))
                    {
                        best = delta;
                        hoveredRing = ring;
                    }
                }
                return;
            }
            if (State == OrbitalRewardFlowState.ModuleSelection)
            {
                hoveredMount = null;
                Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                hoveredModule = station.Modules
                    .Where(value => station.State.CanUpgradeModuleDamage(value.StableModuleId, out _))
                    .OrderBy(value => (value.WorldPosition - world).sqrMagnitude)
                    .FirstOrDefault(value => value.HitTest(world,
                        OrbitalPresentationConfig.Active.ModuleHitRadius));
                return;
            }
            hoveredModule = null;
            hoveredMount = mountResolver.Resolve(station, Camera.main,
                Input.mousePosition, hoveredMount);
        }

        private void UpdateModulePreview()
        {
            if (modulePreview == null || Camera.main == null ||
                (State != OrbitalRewardFlowState.DirectMountSelection &&
                 State != OrbitalRewardFlowState.SecondLinkPlacement))
                return;
            bool valid = CanStageMount(hoveredMount);
            Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 position = valid ? hoveredMount.Transform.position : mouse;
            modulePreview.SetWorldPosition(position);
            if (valid)
            {
                Vector2 radial = (Vector2)hoveredMount.Transform.position -
                    (Vector2)station.transform.position;
                modulePreview.SetWorldRotation(Mathf.Atan2(radial.y, radial.x));
            }
            modulePreview.SetPreviewState(valid);
            modulePreview.Tick();
        }

        private void CreateModulePreview()
        {
            DestroyModulePreview();
            if (reward == null || station == null)
                return;
            OrbitalModuleKind kind = ToModuleKind(reward.RewardKind);
            modulePreview = new OrbitalModuleVisual(station, kind,
                $"{kind} Reward Preview", ModuleColor(kind));
            modulePreview.SetPreviewState(false);
        }

        private void DestroyModulePreview()
        {
            modulePreview?.Teardown();
            modulePreview = null;
        }

        private void DrawSecondLinkPreview()
        {
            if (firstRingId == 0) return;
            OrbitalRingRuntime firstRing = station.Rings.FirstOrDefault(value => value.RingId == firstRingId);
            if (firstRing == null || firstMountIndex >= firstRing.Mounts.Count ||
                firstRing.Mounts[firstMountIndex].Transform == null) return;
            firstLinkPreview?.SetWorldPosition(firstRing.Mounts[firstMountIndex].Transform.position);
            firstLinkPreview?.Tick();
            if (State != OrbitalRewardFlowState.SecondLinkPlacement || Camera.main == null) return;
            bool valid = CanStageMount(hoveredMount);
            Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 end = valid ? hoveredMount.Transform.position : mouse;
            if (linkPreviewLine == null)
            {
                var visual = new GameObject("Link Reward Preview Line");
                visual.transform.SetParent(station.RuntimeRoot, true);
                linkPreviewLine = visual.AddComponent<LineRenderer>();
                linkPreviewLine.useWorldSpace = true;
                linkPreviewLine.positionCount = 2;
                linkPreviewLine.widthMultiplier = 0.045f;
                linkPreviewLine.sharedMaterial = station.VisualMaterial;
                linkPreviewLine.sortingLayerName = "Player";
                linkPreviewLine.sortingOrder = 13;
            }
            linkPreviewLine.enabled = true;
            linkPreviewLine.SetPosition(0, firstRing.Mounts[firstMountIndex].Transform.position);
            linkPreviewLine.SetPosition(1, end);
            linkPreviewLine.startColor = linkPreviewLine.endColor = valid
                ? new Color(0.85f, 0.25f, 1f, 0.72f) : new Color(1f, 0.18f, 0.22f, 0.55f);
        }

        private void RefreshArenaPresentation()
        {
            if (reward == null || station == null)
                return;
            if (State == OrbitalRewardFlowState.DirectMountSelection ||
                State == OrbitalRewardFlowState.SecondLinkPlacement)
                OrbitalMountInteractionPresentation.Apply(station, hoveredMount,
                    station.Rings.FirstOrDefault(r => r.RingId == firstRingId)?.Mounts
                        .FirstOrDefault(m => m.MountIndex == firstMountIndex));
            else if (State == OrbitalRewardFlowState.ModuleSelection)
            {
                for (int i = 0; i < station.Modules.Count; i++)
                {
                    OrbitalModuleRuntime module = station.Modules[i];
                    module.SetHighlighted(module == hoveredModule);
                }
            }
            else if (State == OrbitalRewardFlowState.RingSelection)
            {
                for (int r = 0; r < station.Rings.Count; r++)
                {
                    OrbitalRingRuntime ring = station.Rings[r];
                    bool eligible = CanUseRing(ring);
                    ring.SetInteractionState(eligible, ring == hoveredRing,
                        !eligible || hoveredRing != null && ring != hoveredRing);
                    for (int m = 0; m < ring.Mounts.Count; m++)
                    {
                        OrbitalMountRuntime mount = ring.Mounts[m];
                        mount.SetVisualState(!station.IsMountFree(mount)
                            ? OrbitalMountRuntime.VisualState.Occupied
                            : OrbitalMountRuntime.VisualState.Normal);
                    }
                }
                UpdateAddMountPreview();
            }
            station.Interaction?.ShowHint(reward.upgradeName, GetHint());
            bool valid = State == OrbitalRewardFlowState.RingSelection
                ? hoveredRing != null
                : State == OrbitalRewardFlowState.ModuleSelection
                    ? hoveredModule != null
                : CanStageMount(hoveredMount);
            station.Interaction?.SetCursor(valid
                ? OrbitalCursorState.ValidDrop
                : OrbitalCursorState.InvalidDrop);
        }

        private void ClearArenaVisuals()
        {
            DestroyAddMountPreview();
            OrbitalMountInteractionPresentation.Clear(station);
            if (station != null)
                for (int i = 0; i < station.Modules.Count; i++)
                    station.Modules[i].SetHighlighted(false);
        }

        private bool ApplyAddMount(int ringId)
        {
            if (station.AddMount(ringId, out string error))
                return true;
            Debug.LogWarning($"[OrbitalRewards] Add Mount rejected: {error}", station);
            return false;
        }

        private void UpdateAddMountPreview()
        {
            if (reward.RewardKind != OrbitalRewardKind.AddMount ||
                hoveredRing == null || !CanUseRing(hoveredRing))
            {
                DestroyAddMountPreview();
                return;
            }
            if (addMountPreview == null)
            {
                addMountPreview = station.CreateCircleVisual(
                    "Future Orbital Mount", new Color(0.3f, 1f, 0.55f, 0.9f),
                    Vector2.one * OrbitalPresentationConfig.Active.SelectionMountSize,
                    15);
            }
            int futureCapacity = hoveredRing.MountCapacity + 1;
            float localPhase = hoveredRing.MountCapacity * 360f / futureCapacity;
            float radians = (hoveredRing.Phase + localPhase) * Mathf.Deg2Rad;
            addMountPreview.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * hoveredRing.Radius,
                Mathf.Sin(radians) * hoveredRing.Radius, 0f);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 7f);
            addMountPreview.transform.localScale = Vector3.one *
                OrbitalPresentationConfig.Active.SelectionMountSize * pulse;
        }

        private void DestroyAddMountPreview()
        {
            if (addMountPreview != null)
                Destroy(addMountPreview);
            addMountPreview = null;
        }

        private string GetHint()
        {
            if (State == OrbitalRewardFlowState.ModuleSelection)
            {
                if (hoveredModule == null)
                    return "Выберите установленное оружие · Esc — к карточкам";
                OrbitalModuleState module = station.State.Modules.Find(value =>
                    value.StableModuleId == hoveredModule.StableModuleId);
                int level = module?.DamageLevel ?? 0;
                return $"{hoveredModule.Kind} · DAMAGE LEVEL {level} → {level + 1}\n" +
                    $"УРОН {1f + level * 0.25f:0.##}× → {1f + (level + 1) * 0.25f:0.##}×\n" +
                    "ЛКМ: усилить";
            }
            if (State == OrbitalRewardFlowState.DirectMountSelection ||
                State == OrbitalRewardFlowState.SecondLinkPlacement)
            {
                string step = State == OrbitalRewardFlowState.SecondLinkPlacement
                    ? "Установите второй узел"
                    : reward.RewardKind == OrbitalRewardKind.LinkPair
                        ? "Установите первый узел"
                        : "Выберите свободное крепление";
                if (hoveredMount == null)
                    return step + "\nНаведите оружие на свободное крепление";
                if (!station.IsMountFree(hoveredMount))
                    return "Крепление занято";
                return $"Кольцо {hoveredMount.Ring.State.Order + 1} · " +
                    $"Крепление {hoveredMount.MountIndex + 1}\nЛКМ: установить";
            }
            if (hoveredRing == null)
                return "Выберите подсвеченную орбиту · Esc — к карточкам";
            OrbitalRingState ringState = hoveredRing.State;
            return reward.RewardKind switch
            {
                OrbitalRewardKind.RingSpeed =>
                    $"ОРБИТА {ringState.Order + 1}\n{hoveredRing.RotationSpeed:0.#}°/с → " +
                    $"{hoveredRing.RotationSpeed * 1.25f:0.#}°/с",
                OrbitalRewardKind.RingPower =>
                    $"ОРБИТА {ringState.Order + 1} · СИЛА {ringState.PowerUpgradeLevel}\n" +
                    $"{ringState.PowerMultiplier:0.##}× → {ringState.PowerMultiplier * 1.25f:0.##}×\n" +
                    GetModuleList(ringState.StableRingId),
                OrbitalRewardKind.AddMount =>
                    $"ОРБИТА {ringState.Order + 1}\nКрепления {ringState.MountCapacity} → " +
                    $"{ringState.MountCapacity + 1}",
                _ => $"ОРБИТА {ringState.Order + 1}"
            };
        }

        private bool CanUseRing(OrbitalRingRuntime ring)
        {
            if (ring == null)
                return false;
            return reward.RewardKind switch
            {
                OrbitalRewardKind.RingSpeed =>
                    station.State.CanUpgradeRingSpeed(ring.RingId, out _),
                OrbitalRewardKind.RingPower =>
                    station.State.CanUpgradeRingPower(ring.RingId, out _),
                OrbitalRewardKind.AddMount =>
                    station.State.CanAddMount(ring.RingId, out _),
                _ => station.State.HasFreeMount(ring.RingId)
            };
        }

        private string GetModuleList(int ringId)
        {
            string[] names = station.State.Modules
                .Where(value => value.StableRingId == ringId)
                .Select(value => value.ModuleType.ToString()).ToArray();
            return names.Length == 0 ? "Пока без модулей" :
                "Модули: " + string.Join(", ", names);
        }

        private static bool IsRingReward(OrbitalRewardKind kind) =>
            kind == OrbitalRewardKind.RingSpeed ||
            kind == OrbitalRewardKind.RingPower ||
            kind == OrbitalRewardKind.AddMount;

        private static OrbitalModuleKind ToModuleKind(OrbitalRewardKind kind) =>
            kind switch
            {
                OrbitalRewardKind.Pistol => OrbitalModuleKind.Pistol,
                OrbitalRewardKind.LaserSword => OrbitalModuleKind.LaserSword,
                OrbitalRewardKind.ImpulseGun => OrbitalModuleKind.ImpulseGun,
                OrbitalRewardKind.ArcEmitter => OrbitalModuleKind.ArcEmitter,
                OrbitalRewardKind.LinkPair => OrbitalModuleKind.LinkNode,
                _ => OrbitalModuleKind.Pistol
            };

        private static Color ModuleColor(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => new Color(0.35f, 0.95f, 1f),
            OrbitalModuleKind.LaserSword => new Color(1f, 0.25f, 0.8f),
            OrbitalModuleKind.ImpulseGun => new Color(1f, 0.75f, 0.2f),
            OrbitalModuleKind.ArcEmitter => new Color(0.72f, 0.3f, 1f),
            _ => new Color(0.85f, 0.25f, 1f)
        };

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

        private void OnDisable()
        {
            if (reward != null)
            {
                CancelForSceneTransition();
            }
        }
    }
}
