using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalRewardFlowState
    {
        CardSelection,
        RingSelection,
        MountSelection,
        ModuleFlight,
        SecondLinkPlacement,
        Applying,
        Completed,
        Cancelled
    }

    [DisallowMultipleComponent]
    public sealed class OrbitalRewardFlowController : MonoBehaviour
    {
        private OrbitalStationRuntime station;
        private OrbitalRewardData reward;
        private OrbitalRingRuntime selectedRing;
        private OrbitalRingRuntime hoveredRing;
        private OrbitalMountRuntime hoveredMount;
        private Action completed;
        private Action cancelled;
        private int firstLinkModuleId;
        private int pendingModuleId;
        private bool secondLinkChoosingRing;
        private Coroutine flightRoutine;

        public OrbitalRewardFlowState State { get; private set; } =
            OrbitalRewardFlowState.CardSelection;
        public OrbitalRewardKind? PendingReward => reward != null
            ? reward.RewardKind
            : null;
        public string CompactStatus => reward == null
            ? State.ToString()
            : $"{State}:{reward.RewardKind}";

        public void Bind(OrbitalStationRuntime runtime)
        {
            station = runtime;
        }

        public bool Begin(OrbitalRewardData selected,
            Action onCompleted, Action onCancelled)
        {
            if (selected == null || station == null ||
                !station.IsInitialized || reward != null)
                return false;
            reward = selected;
            completed = onCompleted;
            cancelled = onCancelled;
            selectedRing = null;
            hoveredRing = null;
            hoveredMount = null;
            firstLinkModuleId = 0;
            pendingModuleId = 0;
            secondLinkChoosingRing = false;

            if (!selected.RequiresArenaSelection)
                return ApplyImmediate();
            State = OrbitalRewardFlowState.RingSelection;
            RefreshArenaPresentation();
            return true;
        }

        public void CancelForSceneTransition()
        {
            if (reward == null)
                return;
            CancelToCards();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugChooseRing(int stableRingId)
        {
            hoveredRing = station?.Rings.FirstOrDefault(value =>
                value.RingId == stableRingId);
            if (hoveredRing == null)
                return false;
            if (State == OrbitalRewardFlowState.RingSelection)
                SelectRing();
            else if (State == OrbitalRewardFlowState.SecondLinkPlacement &&
                secondLinkChoosingRing)
                SelectSecondLinkRing();
            else
                return false;
            return true;
        }

        public bool DebugChooseMount(int stableRingId, int mountIndex)
        {
            selectedRing = station?.Rings.FirstOrDefault(value =>
                value.RingId == stableRingId);
            if (selectedRing == null || mountIndex < 0 ||
                mountIndex >= selectedRing.Mounts.Count)
                return false;
            hoveredMount = selectedRing.Mounts[mountIndex];
            if (State == OrbitalRewardFlowState.MountSelection)
                SelectMount(false);
            else if (State == OrbitalRewardFlowState.SecondLinkPlacement &&
                !secondLinkChoosingRing)
                SelectMount(true);
            else
                return false;
            return true;
        }
#endif

        private void Update()
        {
            if (reward == null || station == null ||
                Subject42DebugMenu.IsDebugMenuOpen)
                return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                HandleBack();
                return;
            }
            if (State == OrbitalRewardFlowState.ModuleFlight ||
                State == OrbitalRewardFlowState.Applying)
                return;
            UpdateHover();
            RefreshArenaPresentation();
            DrawSecondLinkPreview();
            if (!Input.GetMouseButtonDown(0))
                return;
            if (State == OrbitalRewardFlowState.RingSelection)
                SelectRing();
            else if (State == OrbitalRewardFlowState.MountSelection)
                SelectMount(false);
            else if (State == OrbitalRewardFlowState.SecondLinkPlacement)
            {
                if (secondLinkChoosingRing)
                    SelectSecondLinkRing();
                else
                    SelectMount(true);
            }
        }

        private void SelectRing()
        {
            if (hoveredRing == null || !CanUseRing(hoveredRing))
                return;
            selectedRing = hoveredRing;
            if (IsRingReward(reward.RewardKind))
            {
                State = OrbitalRewardFlowState.Applying;
                int selectedRingId = selectedRing.RingId;
                bool applied = reward.RewardKind switch
                {
                    OrbitalRewardKind.RingSpeed =>
                        station.UpgradeRingSpeed(selectedRingId),
                    OrbitalRewardKind.RingPower =>
                        station.UpgradeRingPower(selectedRingId),
                    OrbitalRewardKind.AddMount =>
                        station.AddMount(selectedRingId, out _),
                    _ => false
                };
                if (applied)
                {
                    station.Rings.FirstOrDefault(value =>
                        value.RingId == selectedRingId)?.Pulse();
                    station.FlashCore(new Color(0.35f, 0.9f, 1f));
                    CompleteReward();
                }
                else
                {
                    State = OrbitalRewardFlowState.RingSelection;
                }
                return;
            }
            State = OrbitalRewardFlowState.MountSelection;
        }

        private void SelectSecondLinkRing()
        {
            if (hoveredRing == null || !HasFreeMount(hoveredRing))
                return;
            selectedRing = hoveredRing;
            secondLinkChoosingRing = false;
        }

        private void SelectMount(bool secondLink)
        {
            if (selectedRing == null || hoveredMount == null ||
                hoveredMount.Ring != selectedRing || hoveredMount.Occupied)
                return;
            OrbitalModuleKind kind = ToModuleKind(reward.RewardKind);
            int previousNextId = station.State.NextStableModuleId;
            if (!station.InstallModule(kind, selectedRing.RingId,
                    hoveredMount.MountIndex, out _))
                return;
            int installedId = previousNextId;
            pendingModuleId = installedId;
            station.SetModuleRewardPresentationVisible(installedId, false);
            if (reward.RewardKind == OrbitalRewardKind.LinkPair && !secondLink)
                firstLinkModuleId = installedId;
            StartFlight(hoveredMount.Transform.position, secondLink);
        }

        private void StartFlight(Vector3 destination, bool secondLink)
        {
            State = OrbitalRewardFlowState.ModuleFlight;
            ClearArenaVisuals();
            station.Interaction?.SetCursor(OrbitalCursorState.Normal);
            if (flightRoutine != null)
                StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(PlayFlight(destination, secondLink));
        }

        private IEnumerator PlayFlight(Vector3 destination, bool secondLink)
        {
            Color color = reward.RewardKind == OrbitalRewardKind.LinkPair
                ? new Color(0.85f, 0.25f, 1f)
                : new Color(0.35f, 0.95f, 1f);
            GameObject visual = station.CreateModuleVisual(
                "Orbital Reward Flight", color, Vector2.one * 0.13f);
            Vector3 origin = station.transform.position;
            float elapsed = 0f;
            const float duration = 0.32f;
            while (elapsed < duration && visual != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                visual.transform.position = Vector3.LerpUnclamped(origin,
                    destination, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            if (visual != null)
                Destroy(visual);
            if (station != null && pendingModuleId > 0)
                station.SetModuleRewardPresentationVisible(pendingModuleId, true);
            pendingModuleId = 0;
            flightRoutine = null;
            if (reward == null)
                yield break;
            if (reward.RewardKind == OrbitalRewardKind.LinkPair && !secondLink)
            {
                State = OrbitalRewardFlowState.SecondLinkPlacement;
                selectedRing = null;
                secondLinkChoosingRing = true;
            }
            else
            {
                CompleteReward();
            }
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
            CompleteReward();
            return true;
        }

        private bool ApplyCore()
        {
            int before = station.State.CoreState.Level;
            station.UpgradeCore();
            return station.State.CoreState.Level == before + 1;
        }

        private void HandleBack()
        {
            if (State == OrbitalRewardFlowState.MountSelection)
            {
                selectedRing = null;
                State = OrbitalRewardFlowState.RingSelection;
                return;
            }
            if (State == OrbitalRewardFlowState.SecondLinkPlacement)
            {
                if (!secondLinkChoosingRing)
                {
                    selectedRing = null;
                    secondLinkChoosingRing = true;
                    return;
                }
                if (firstLinkModuleId > 0)
                    station.RemoveModule(firstLinkModuleId);
            }
            CancelToCards();
        }

        private void CancelToCards()
        {
            if (pendingModuleId > 0 && station != null && station.State != null &&
                station.State.Modules.Any(value =>
                    value.StableModuleId == pendingModuleId))
                station.RemoveModule(pendingModuleId);
            if (firstLinkModuleId > 0 && station != null && station.State != null &&
                station.State.Modules.Any(value =>
                    value.StableModuleId == firstLinkModuleId))
                station.RemoveModule(firstLinkModuleId);
            State = OrbitalRewardFlowState.Cancelled;
            Clear(false);
        }

        private void CompleteReward()
        {
            State = OrbitalRewardFlowState.Completed;
            Clear(true);
        }

        private void Clear(bool success)
        {
            Action callback = success ? completed : cancelled;
            ClearArenaVisuals();
            station?.Interaction?.ClearHint();
            station?.Interaction?.SetCursor(OrbitalCursorState.Normal);
            reward = null;
            selectedRing = null;
            hoveredRing = null;
            hoveredMount = null;
            completed = null;
            cancelled = null;
            firstLinkModuleId = 0;
            pendingModuleId = 0;
            callback?.Invoke();
        }

        private void UpdateHover()
        {
            hoveredRing = null;
            hoveredMount = null;
            if (Camera.main == null)
                return;
            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 local = transform.InverseTransformPoint(world);
            if (State == OrbitalRewardFlowState.RingSelection ||
                (State == OrbitalRewardFlowState.SecondLinkPlacement &&
                 secondLinkChoosingRing))
            {
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
            if (selectedRing == null)
                return;
            float bestMount = 0.38f * 0.38f;
            for (int i = 0; i < selectedRing.Mounts.Count; i++)
            {
                OrbitalMountRuntime mount = selectedRing.Mounts[i];
                if (mount.Occupied || mount.Transform == null)
                    continue;
                float distance = ((Vector2)mount.Transform.position -
                    (Vector2)world).sqrMagnitude;
                if (distance < bestMount)
                {
                    bestMount = distance;
                    hoveredMount = mount;
                }
            }
        }

        private void DrawSecondLinkPreview()
        {
            if (State != OrbitalRewardFlowState.SecondLinkPlacement ||
                firstLinkModuleId <= 0 || hoveredMount == null)
                return;
            OrbitalModuleState first = station.State.Modules.Find(value =>
                value.StableModuleId == firstLinkModuleId);
            OrbitalRingRuntime firstRing = first != null
                ? station.Rings.FirstOrDefault(value =>
                    value.RingId == first.StableRingId)
                : null;
            if (firstRing == null || first.MountIndex < 0 ||
                first.MountIndex >= firstRing.Mounts.Count ||
                firstRing.Mounts[first.MountIndex].Transform == null)
                return;
            station.FlashLink(
                firstRing.Mounts[first.MountIndex].Transform.position,
                hoveredMount.Transform.position,
                new Color(0.85f, 0.25f, 1f, 0.7f), 0.06f);
        }

        private bool CanUseRing(OrbitalRingRuntime ring)
        {
            if (ring == null)
                return false;
            OrbitalProgressionConfig config = OrbitalProgressionConfig.Default;
            return reward.RewardKind switch
            {
                OrbitalRewardKind.RingSpeed =>
                    ring.State.SpeedUpgradeLevel < config.MaxSpeedUpgradeLevel,
                OrbitalRewardKind.RingPower =>
                    ring.State.PowerUpgradeLevel < config.MaxPowerUpgradeLevel,
                OrbitalRewardKind.AddMount =>
                    ring.State.MountCapacity < config.MaxMountsPerRing,
                _ => HasFreeMount(ring)
            };
        }

        private static bool HasFreeMount(OrbitalRingRuntime ring) =>
            ring != null && ring.Mounts.Any(mount => !mount.Occupied);

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

        private void RefreshArenaPresentation()
        {
            if (reward == null || station == null)
                return;
            bool choosingRing = State == OrbitalRewardFlowState.RingSelection ||
                (State == OrbitalRewardFlowState.SecondLinkPlacement &&
                 secondLinkChoosingRing);
            bool choosingMount = State == OrbitalRewardFlowState.MountSelection ||
                (State == OrbitalRewardFlowState.SecondLinkPlacement &&
                 !secondLinkChoosingRing);
            for (int r = 0; r < station.Rings.Count; r++)
            {
                OrbitalRingRuntime ring = station.Rings[r];
                bool eligible = choosingRing && CanUseRing(ring);
                ring.SetSelected(choosingMount && ring == selectedRing);
                ring.SetInteractionState(eligible, ring == hoveredRing);
                for (int m = 0; m < ring.Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = ring.Mounts[m];
                    OrbitalMountRuntime.VisualState state;
                    if (mount.Occupied)
                        state = OrbitalMountRuntime.VisualState.Occupied;
                    else if (choosingMount && ring == selectedRing)
                        state = mount == hoveredMount
                            ? OrbitalMountRuntime.VisualState.Hover
                            : OrbitalMountRuntime.VisualState.Valid;
                    else if (choosingRing && eligible && !IsRingReward(reward.RewardKind))
                        state = OrbitalMountRuntime.VisualState.Valid;
                    else
                        state = OrbitalMountRuntime.VisualState.Normal;
                    mount.SetVisualState(state);
                }
            }
            station.Interaction?.ShowHint(reward.upgradeName, GetHint());
            bool validTarget = choosingRing ? hoveredRing != null :
                choosingMount && hoveredMount != null;
            station.Interaction?.SetCursor(validTarget
                ? OrbitalCursorState.ValidDrop
                : OrbitalCursorState.InvalidDrop);
        }

        private void ClearArenaVisuals()
        {
            if (station == null)
                return;
            for (int r = 0; r < station.Rings.Count; r++)
            {
                OrbitalRingRuntime ring = station.Rings[r];
                ring.SetSelected(false);
                ring.SetInteractionState(false, false);
                for (int m = 0; m < ring.Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = ring.Mounts[m];
                    mount.SetVisualState(mount.Occupied
                        ? OrbitalMountRuntime.VisualState.Occupied
                        : OrbitalMountRuntime.VisualState.Normal);
                }
            }
        }

        private string GetHint()
        {
            if (State == OrbitalRewardFlowState.MountSelection ||
                (State == OrbitalRewardFlowState.SecondLinkPlacement &&
                 !secondLinkChoosingRing))
            {
                if (hoveredMount != null)
                    return $"Крепление {hoveredMount.MountIndex + 1} · ЛКМ: установить";
                return selectedRing == null
                    ? "Выберите подсвеченную орбиту"
                    : $"Орбита {selectedRing.State.Order + 1} · выберите зелёное крепление\nEsc — назад";
            }
            if (hoveredRing == null)
            {
                return IsRingReward(reward.RewardKind)
                    ? "Выберите подсвеченную орбиту · Esc — к карточкам"
                    : "Зелёные точки — доступные крепления\nСначала выберите орбиту";
            }
            OrbitalRingState ring = hoveredRing.State;
            return reward.RewardKind switch
            {
                OrbitalRewardKind.RingSpeed =>
                    $"ОРБИТА {ring.Order + 1}\n{hoveredRing.RotationSpeed:0.#}°/с → " +
                    $"{hoveredRing.RotationSpeed * 1.25f:0.#}°/с",
                OrbitalRewardKind.RingPower =>
                    $"ОРБИТА {ring.Order + 1} · СИЛА {ring.PowerUpgradeLevel}\n" +
                    $"{ring.PowerMultiplier:0.##}× → {ring.PowerMultiplier * 1.25f:0.##}×\n" +
                    GetModuleList(ring.StableRingId),
                OrbitalRewardKind.AddMount =>
                    $"ОРБИТА {ring.Order + 1}\nКрепления {ring.MountCapacity} → {ring.MountCapacity + 1}",
                _ => $"ОРБИТА {ring.Order + 1}\nВыберите свободное крепление"
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

        private void OnDisable()
        {
            if (reward != null)
                CancelForSceneTransition();
        }
    }
}
