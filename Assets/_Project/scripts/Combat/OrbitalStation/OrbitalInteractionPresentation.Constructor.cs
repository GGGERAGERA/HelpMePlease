using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalInteractionPresentation
    {
        [Header("Authored orbital constructor")]
        [SerializeField] private Canvas constructorCanvas;
        [SerializeField] private GameObject constructorPanel, constructorWeaponGroup, constructorModuleGroup;
        [SerializeField] private RectTransform constructorReserve;
        [SerializeField] private Button constructorLauncher, constructorClose;
        [SerializeField] private Button[] constructorMounts, constructorWeapons, constructorLinks;
        [SerializeField] private OrbitalModuleKind[] constructorWeaponKinds;
        [SerializeField] private SegmentedLinkKind[] constructorModuleKinds;
        [SerializeField] private Image[] constructorSlotFrames, constructorSlotIcons;
        [SerializeField] private RectTransform[] constructorLines;
        [SerializeField] private TextMeshProUGUI constructorEmpty;
        [SerializeField] private string[] constructorWeaponLabels, constructorModuleLabels;
        private UpgradePanelView constructorRewardPanel;
        private readonly List<LineRenderer> constructorWorldLines = new();
        private readonly List<SpriteRenderer> constructorWorldModules = new();
        private readonly List<SpriteRenderer> constructorWorldModuleCrosses = new();
        private readonly List<RaycastResult> constructorHits = new();
        private readonly List<Vector3> constructorArcPoints = new(64);
        private int constructorRevision = -1, constructorDragRevision, constructorDragCell = -1, constructorDropCell = -1;
        private GameLanguage constructorLanguage;
        private float[] constructorMountRadii;
        private OrbitalModuleState constructorDragWeapon;
        private SegmentedLinkState constructorDragModule;
        private SegmentedLinkKind? constructorDragModuleKind;
        private Image constructorDragIcon;
        private CanvasGroup constructorDragStockVisibility;
        private TextMeshProUGUI constructorDragStockCount;
        private string constructorDragStockText;
        private float constructorDragStockAlpha;
        private bool constructorDragStockRaycasts;
        private bool constructorHidReward;
        private bool ConstructorDragging => constructorDragWeapon != null || constructorDragModuleKind.HasValue;
        private static readonly Color ConstructorBlue = new(.23f, .65f, .95f);
        private static readonly Color ConstructorDropCyan = new(.2f, .85f, 1f);
        private static readonly Color ConstructorGreen = new(.28f, .82f, .52f);
        private static readonly Color ConstructorAmber = new(.98f, .79f, .35f);
        private static readonly Color ConstructorInactiveBridge = new(.18f, .4f, .52f);

        public void BindRewardPanel(UpgradePanelView panel)
        {
            CloseConstructor();
            constructorRewardPanel = panel;
        }

        private void BindConstructor()
        {
            if (!station.UsesSegmentedOrbit) return;
            BindButton(constructorLauncher, () => OpenConstructor());
            BindButton(constructorClose, CloseConstructor);
            constructorMountRadii = new float[constructorMounts.Length];
            for (int i = 0; i < constructorMounts.Length; i++)
            {
                int cell = i;
                constructorMountRadii[i] = ((RectTransform)constructorMounts[i].transform).anchoredPosition.magnitude;
                BindConstructorDrag(constructorMounts[i], pointer => BeginConstructorDrag(pointer, cell));
            }
            for (int i = 0; i < constructorWeapons.Length; i++)
            { int item = i; BindConstructorDrag(constructorWeapons[i], pointer => BeginConstructorDrag(pointer, -1, item)); }
            for (int i = 0; i < constructorLinks.Length; i++)
            { int item = i; BindConstructorDrag(constructorLinks[i], pointer => BeginConstructorDrag(pointer, -1, -1, item)); }
            constructorDragIcon = Instantiate(constructorSlotIcons[0], constructorCanvas.transform, false);
            constructorDragIcon.name = "Dragged constructor item";
            constructorDragIcon.raycastTarget = false;
            constructorDragIcon.rectTransform.anchorMin = constructorDragIcon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            constructorDragIcon.rectTransform.pivot = new Vector2(.5f, .5f);
            constructorDragIcon.rectTransform.sizeDelta = Vector2.one * 34f;
            constructorDragIcon.gameObject.SetActive(false);
            constructorPanel.SetActive(false);
            constructorLauncher.gameObject.SetActive(false);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
        private static void ButtonText(Button button, string text) => button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;

        private void BindConstructorDrag(Button button, Action<PointerEventData> begin)
        {
            button.onClick.RemoveAllListeners();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            void Add(EventTriggerType type, Action<PointerEventData> action)
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(data => action((PointerEventData)data));
                trigger.triggers.Add(entry);
            }
            Add(EventTriggerType.BeginDrag, begin);
            Add(EventTriggerType.Drag, MoveConstructorDrag);
            Add(EventTriggerType.EndDrag, EndConstructorDrag);
            Add(EventTriggerType.Scroll, pointer => button.GetComponentInParent<ScrollRect>()?.OnScroll(pointer));
        }

        public bool OpenConstructor()
        {
            if (constructorRewardPanel == null || !station.InputOwner.BeginConstructor()) return false;
            constructorHidReward = constructorRewardPanel.gameObject.activeSelf;
            constructorRewardPanel.SetConstructorCovered(true);
            constructorPanel.SetActive(true);
            constructorLauncher.gameObject.SetActive(false);
            ResetConstructorDrag();
            RefreshConstructor();
            return true;
        }

        public void CloseConstructor()
        {
            ResetConstructorDrag();
            if (station?.InputOwner != null)
            {
                bool completesReward = station.InputOwner.IsConstructing && station.RewardFlow.IsAwaitingConstructor;
                station.InputOwner.EndConstructor();
                if (completesReward) station.RewardFlow.CompleteConstructorReward();
                if (constructorHidReward && constructorRewardPanel != null && station.InputOwner.IsChoosingReward)
                    constructorRewardPanel.SetConstructorCovered(false);
            }
            constructorHidReward = false;
            if (constructorPanel != null) constructorPanel.SetActive(false);
        }

        private OrbitalMountRuntime ConstructorMount(int cell)
        {
            foreach (var ring in station.Rings)
            {
                if (cell < ring.MountCount) return ring.Mounts[cell];
                cell -= ring.MountCount;
            }
            throw new ArgumentOutOfRangeException(nameof(cell));
        }
        private int ConstructorCell(OrbitalRingState ring, int mount) =>
            station.Rings.Take(ring.Order).Sum(level => level.MountCount) + mount;
        private SegmentedLinkState ConstructorModule(int cell) => station.State.SegmentedModuleAt(ConstructorMount(cell).Ring.RingId, ConstructorMount(cell).MountIndex);
        private bool IsWeaponCell(int cell)
        {
            var mount = ConstructorMount(cell);
            return OrbitalRunState.IsSegmentedWeaponSlot(mount.Ring.State.Order, mount.MountIndex);
        }

        private void BeginConstructorDrag(PointerEventData pointer, int cell, int weaponItem = -1, int moduleItem = -1)
        {
            if (pointer.button != PointerEventData.InputButton.Left || !station.InputOwner.CanEditConstructor) return;
            ResetConstructorDrag();
            var state = station.State;
            constructorDragCell = cell;
            if (cell >= 0)
            {
                if (IsWeaponCell(cell))
                {
                    var gun = ConstructorMount(cell).Module;
                    constructorDragWeapon = gun == null ? null : state.FindModule(gun.StableModuleId);
                }
                else constructorDragModuleKind = (constructorDragModule = ConstructorModule(cell))?.Kind;
            }
            else if (weaponItem >= 0)
                constructorDragWeapon = state.SegmentedWeapons.FirstOrDefault(gun => gun.ModuleType == constructorWeaponKinds[weaponItem]);
            else if (moduleItem >= 0 && state.SegmentedLinkStock.Contains(constructorModuleKinds[moduleItem]))
                constructorDragModuleKind = constructorModuleKinds[moduleItem];
            if (constructorDragWeapon != null && !state.IsWeaponAvailable(constructorDragWeapon.ModuleType))
            {
                ResetConstructorDrag();
                return;
            }
            if (!ConstructorDragging) return;
            constructorDragRevision = state.Revision;
            if (cell < 0)
            {
                var row = weaponItem >= 0 ? constructorWeapons[weaponItem] : constructorLinks[moduleItem];
                constructorDragStockCount = row.GetComponentInChildren<TextMeshProUGUI>(true);
                constructorDragStockText = constructorDragStockCount.text;
                constructorDragStockVisibility = row.GetComponent<CanvasGroup>();
                if (constructorDragStockVisibility == null) constructorDragStockVisibility = row.gameObject.AddComponent<CanvasGroup>();
                constructorDragStockAlpha = constructorDragStockVisibility.alpha;
                constructorDragStockRaycasts = constructorDragStockVisibility.blocksRaycasts;
                int available = constructorDragWeapon != null
                    ? state.SegmentedWeapons.Count(gun => gun.ModuleType == constructorDragWeapon.ModuleType)
                    : state.SegmentedLinkStock.Count(kind => kind == constructorDragModuleKind.Value);
                if (available == 1)
                {
                    // Keep the source active and in the layout so it retains the pointer drag.
                    constructorDragStockVisibility.alpha = 0f;
                    constructorDragStockVisibility.blocksRaycasts = false;
                }
            }
            var icon = constructorDragWeapon != null ? OrbitalRewardIconResolver.Resolve(constructorDragWeapon.ModuleType) :
                OrbitalRewardIconResolver.Resolve(constructorDragModuleKind.Value);
            constructorDragIcon.sprite = icon.Sprite; constructorDragIcon.color = icon.ImageTint;
            constructorDragIcon.gameObject.SetActive(true);
            constructorDragIcon.transform.SetAsLastSibling();
            MoveConstructorDrag(pointer);
            RefreshConstructor();
        }

        private void MoveConstructorDrag(PointerEventData pointer)
        {
            if (!ConstructorDragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)constructorCanvas.transform,
                pointer.position, pointer.pressEventCamera, out var position);
            constructorDragIcon.rectTransform.anchoredPosition = position;
            constructorDropCell = ConstructorCellUnderPointer(pointer);
            constructorRevision = -1;
        }

        private int ConstructorCellUnderPointer(PointerEventData pointer)
        {
            constructorHits.Clear();
            EventSystem.current.RaycastAll(pointer, constructorHits);
            if (constructorHits.Count == 0) return -1;
            var hit = constructorHits[0].gameObject.transform;
            for (int i = 0; i < constructorMounts.Length; i++)
                if (hit.IsChildOf(constructorMounts[i].transform)) return i;
            return -1;
        }

        private bool CanDropConstructorItem(int cell)
        {
            if (!ConstructorDragging || !station.InputOwner.CanEditConstructor || cell < 0 ||
                cell >= constructorMounts.Length || constructorDragRevision != station.State.Revision) return false;
            var mount = ConstructorMount(cell);
            if (constructorDragWeapon != null)
                return IsWeaponCell(cell) && station.State.CanTransferSegmentedWeapon(constructorDragWeapon.StableModuleId,
                    constructorDragCell < 0, mount.Ring.RingId, mount.MountIndex, out _);
            if (IsWeaponCell(cell)) return false;
            return constructorDragModule == null
                ? station.State.CanInstallSegmentedModule(constructorDragModuleKind.Value, mount.Ring.RingId, mount.MountIndex, out _)
                : station.State.CanMoveSegmentedModule(station.State.SegmentedLinks.IndexOf(constructorDragModule), mount.Ring.RingId, mount.MountIndex, out _);
        }

        private void EndConstructorDrag(PointerEventData pointer)
        {
            if (!ConstructorDragging || pointer.button != PointerEventData.InputButton.Left) return;
            if (station.InputOwner.CanEditConstructor && constructorDragRevision == station.State.Revision)
            {
                int cell = ConstructorCellUnderPointer(pointer);
                if (CanDropConstructorItem(cell))
                {
                    var mount = ConstructorMount(cell);
                    if (constructorDragWeapon != null)
                        station.TransferConstructorWeapon(constructorDragWeapon.StableModuleId, constructorDragCell < 0, mount.Ring.RingId, mount.MountIndex, out _);
                    else if (constructorDragModule == null)
                        station.ConstructModule(constructorDragModuleKind.Value, mount.Ring.RingId, mount.MountIndex, out _);
                    else station.RepositionModule(station.State.SegmentedLinks.IndexOf(constructorDragModule), mount.Ring.RingId, mount.MountIndex, out _);
                }
                else if (constructorDragCell >= 0 && constructorHits.Count > 0 &&
                    constructorHits[0].gameObject.transform.IsChildOf(constructorReserve))
                {
                    if (constructorDragWeapon != null) station.StowWeapon(constructorDragWeapon.StableModuleId);
                    else station.StowModule(station.State.SegmentedLinks.IndexOf(constructorDragModule));
                }
            }
            ResetConstructorDrag();
            if (station.InputOwner.CanEditConstructor) RefreshConstructor();
        }

        private void ResetConstructorDrag()
        {
            if (constructorDragStockVisibility != null)
            {
                constructorDragStockVisibility.alpha = constructorDragStockAlpha;
                constructorDragStockVisibility.blocksRaycasts = constructorDragStockRaycasts;
            }
            if (constructorDragStockCount != null) constructorDragStockCount.text = constructorDragStockText;
            constructorDragStockVisibility = null; constructorDragStockCount = null;
            constructorDragStockText = null;
            constructorDragWeapon = null; constructorDragModule = null; constructorDragModuleKind = null;
            constructorDragCell = constructorDropCell = -1; constructorRevision = -1;
            if (constructorDragIcon != null) constructorDragIcon.gameObject.SetActive(false);
        }

        public bool CancelConstructorSelection()
        {
            if (!ConstructorDragging) return false;
            ResetConstructorDrag();
            return true;
        }

        private void OnApplicationFocus(bool focused)
        { if (!focused) ResetConstructorDrag(); }

        private void UpdateConstructor()
        {
            if (station == null || !station.IsInitialized || !station.UsesSegmentedOrbit) return;
            constructorCanvas.gameObject.SetActive(true);
            if (station.InputOwner.IsConstructing && !station.InputOwner.CanEditConstructor) CloseConstructor();
            bool notify = !station.InputOwner.IsChoosingReward && station.InputOwner.CanOpenConstructor &&
                (station.State.SegmentedWeapons.Count > 0 || station.State.SegmentedLinkStock.Count > 0);
            constructorLauncher.gameObject.SetActive(notify);
            if (notify)
            {
                float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.6f);
                constructorLauncher.transform.localScale = Vector3.one * (1f + .035f * pulse);
                constructorLauncher.targetGraphic.color = Color.Lerp(new Color(.16f, .09f, .02f), new Color(.3f, .19f, .04f), pulse);
                constructorLauncher.GetComponentInChildren<TextMeshProUGUI>().color = Color.Lerp(ConstructorAmber, new Color(1f, .94f, .64f), pulse);
            }
            if (ConstructorDragging && (constructorDragRevision != station.State.Revision || !Input.GetMouseButton(0))) ResetConstructorDrag();
            if (station.InputOwner.IsConstructing && (constructorRevision != station.State.Revision ||
                (!ConstructorDragging && constructorLanguage != LocalizationService.EnsureExists().CurrentLanguage))) RefreshConstructor();
            UpdateConstructorWorldLinks();
        }

        private string ConstructorWeaponLabel(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var state = station?.State;
            if (state?.UsesSegmentedOrbit == true && state.MissReturnPistolExperiment &&
                constructorWeaponKinds[index] == OrbitalModuleKind.Pistol)
                return LocalizationService.EnsureExists().Get("constructor.missReturnPistolLabel");
            if (state?.UsesSegmentedOrbit == true && state.PulsedSawExperiment &&
                constructorWeaponKinds[index] == OrbitalModuleKind.Saw)
                return LocalizationService.EnsureExists().Get("constructor.pulsedSawLabel");
#endif
            return constructorWeaponKinds[index] switch
            {
                OrbitalModuleKind.Pistol => LocalizationService.EnsureExists().Get("reward.gun"),
                OrbitalModuleKind.Shotgun => LocalizationService.EnsureExists().Get("reward.shotgun"),
                OrbitalModuleKind.Saw => LocalizationService.EnsureExists().Get("reward.saw"),
                OrbitalModuleKind.Sniper => LocalizationService.EnsureExists().Get("reward.sniper"),
                _ => constructorWeaponLabels[index]
            };
        }

        private string ConstructorModuleLabel(int index)
        {
            return constructorModuleKinds[index] switch
            {
                SegmentedLinkKind.Damage => LocalizationService.EnsureExists().Get("constructor.damageLabel"),
                SegmentedLinkKind.FireRate => LocalizationService.EnsureExists().Get("constructor.fireRateLabel"),
                SegmentedLinkKind.ExtraShot => LocalizationService.EnsureExists().Get("constructor.extraShotLabel"),
                SegmentedLinkKind.Ricochet => LocalizationService.EnsureExists().Get("constructor.ricochetLabel"),
                _ => constructorModuleLabels[index]
            };
        }

        private void RefreshConstructor()
        {
            var state = station.State;
            constructorRevision = state.Revision;
            constructorLanguage = LocalizationService.EnsureExists().CurrentLanguage;
            for (int i = 0; i < constructorMounts.Length; i++)
            {
                var mount = ConstructorMount(i);
                ((RectTransform)constructorMounts[i].transform).anchoredPosition = mount.Ring.Geometry.PositionDegrees(
                    mount.Ring.Phase + mount.LocalPhase, constructorMountRadii[i]);
                var module = ConstructorModule(i);
                bool weaponCell = IsWeaponCell(i), unlocked = state.IsRingActive(mount.Ring.State);
                bool candidate = CanDropConstructorItem(i);
                var typeColor = weaponCell ? ConstructorBlue : ConstructorGreen;
                bool occupied = weaponCell ? mount.Occupied : module != null;
                bool stockWeaponTarget = candidate && constructorDragWeapon != null && constructorDragCell < 0;
                var targetColor = stockWeaponTarget ? occupied ? ConstructorAmber : ConstructorDropCyan : typeColor;
                constructorMounts[i].interactable = unlocked;
                bool dormant = module != null && !state.SegmentedModuleTargets(module).Any();
                constructorSlotFrames[i].color = candidate ? stockWeaponTarget ? targetColor : Color.Lerp(typeColor, Color.white, .45f) :
                    new Color(typeColor.r, typeColor.g, typeColor.b, unlocked ? occupied && !dormant ? 1f : .55f : .14f);
                constructorMounts[i].targetGraphic.color = Color.Lerp(new Color(.025f, .045f, .075f), targetColor,
                    unlocked ? candidate ? i == constructorDropCell ? .65f : .38f : occupied ? .26f : .08f : .025f);
                var label = constructorMounts[i].GetComponentInChildren<TextMeshProUGUI>();
                label.text = occupied ? "" : weaponCell ? "O" : "M";
                label.color = new Color(typeColor.r, typeColor.g, typeColor.b, unlocked ? 1f : .3f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                var image = constructorSlotIcons[i];
                image.gameObject.SetActive(occupied);
                if (occupied)
                {
                    var icon = weaponCell ? OrbitalRewardIconResolver.Resolve(mount.Module.Kind) : OrbitalRewardIconResolver.Resolve(module.Kind);
                    image.sprite = icon.Sprite; image.color = icon.ImageTint;
                    image.rectTransform.anchoredPosition = Vector2.zero;
                    image.rectTransform.sizeDelta = Vector2.one * 28f;
                    if (ConstructorDragging && i == constructorDragCell) image.color *= new Color(1f, 1f, 1f, .3f);
                }
            }
            int weaponRows = 0, moduleRows = 0;
            for (int i = 0; i < constructorWeapons.Length; i++)
            {
                int count = state.SegmentedWeapons.Count(gun => gun.ModuleType == constructorWeaponKinds[i]);
                constructorWeapons[i].gameObject.SetActive(count > 0);
                if (count == 0) continue;
                weaponRows++;
                int displayedCount = count - (ConstructorDragging && constructorDragCell < 0 &&
                    constructorDragWeapon?.ModuleType == constructorWeaponKinds[i] ? 1 : 0);
                bool available = state.IsWeaponAvailable(constructorWeaponKinds[i]);
                string availability = available ? "" : constructorLanguage == GameLanguage.Russian
                    ? " · временно недоступна" : " · temporarily unavailable";
                ButtonText(constructorWeapons[i], ConstructorWeaponLabel(i) + "  ×" + displayedCount + availability);
                constructorWeapons[i].interactable = available;
            }
            for (int i = 0; i < constructorLinks.Length; i++)
            {
                int count = state.SegmentedLinkStock.Count(value => value == constructorModuleKinds[i]);
                constructorLinks[i].gameObject.SetActive(count > 0);
                if (count == 0) continue;
                moduleRows++;
                int displayedCount = count - (ConstructorDragging && constructorDragCell < 0 &&
                    constructorDragModuleKind == constructorModuleKinds[i] ? 1 : 0);
                ButtonText(constructorLinks[i], ConstructorModuleLabel(i) + "  ×" + displayedCount);
                var icon = OrbitalRewardIconResolver.Resolve(constructorModuleKinds[i]);
                var rowIcon = constructorLinks[i].transform.Find("Item icon").GetComponent<Image>();
                rowIcon.sprite = icon.Sprite; rowIcon.color = icon.ImageTint;
                constructorLinks[i].interactable = true;
            }
            constructorWeaponGroup.SetActive(weaponRows > 0); constructorModuleGroup.SetActive(moduleRows > 0);
            constructorEmpty.gameObject.SetActive(weaponRows + moduleRows == 0);
            constructorEmpty.text = LocalizationService.EnsureExists().Get("constructor.emptyStock");
            RefreshConstructorConnections();
        }

        private void RefreshConstructorConnections()
        {
            int lineIndex = 0;
            // Adjacent-level V2-A directions stay readable even before their items are installed.
            for (int level = 1; level < station.Rings.Count; level++)
            foreach (var source in station.Rings[level].Mounts)
            {
                if (OrbitalRunState.IsSegmentedWeaponSlot(level, source.MountIndex)) continue;
                var target = station.Rings[level - 1].Mounts.First(mount =>
                    OrbitalRunState.IsSegmentedWeaponSlot(level - 1, mount.MountIndex) &&
                    Mathf.Abs(Mathf.DeltaAngle(source.LocalPhase, mount.LocalPhase)) < .01f);
                var module = station.State.SegmentedModuleAt(source.Ring.RingId, source.MountIndex);
                bool active = module != null && target.Module != null && station.State.SegmentedModuleTargets(module)
                    .Any(weapon => weapon.StableModuleId == target.Module.StableModuleId);
                SetConstructorConnection(lineIndex++, ConstructorCell(source.Ring.State, source.MountIndex),
                    ConstructorCell(target.Ring.State, target.MountIndex),
                    active ? SegmentedModulePresentation.LinkColor : ConstructorInactiveBridge);
            }
            foreach (var module in station.State.SegmentedLinks)
            {
                int moduleCell = ConstructorCell(station.State.FindRing(module.RingId), module.MountIndex);
                foreach (var weapon in station.State.SegmentedModuleTargets(module))
                {
                    if (weapon.StableRingId != module.RingId) continue;
                    int cell = ConstructorCell(station.State.FindRing(weapon.StableRingId), weapon.MountIndex);
                    SetConstructorConnection(lineIndex++, moduleCell, cell,
                        moduleCell == constructorDragCell || cell == constructorDragCell ? ConstructorAmber : SegmentedModulePresentation.LinkColor);
                }
            }
            while (lineIndex < constructorLines.Length) constructorLines[lineIndex++].gameObject.SetActive(false);
        }

        private void SetConstructorConnection(int index, int sourceCell, int targetCell, Color color)
        {
            var source = (RectTransform)constructorMounts[sourceCell].transform;
            var target = (RectTransform)constructorMounts[targetCell].transform;
            Vector2 from = source.anchoredPosition, to = target.anchoredPosition;
            if (ConstructorMount(sourceCell).Ring != ConstructorMount(targetCell).Ring)
            {
                Vector2 direction = (to - from).normalized;
                from = ConstructorConnectionEdge(source, direction);
                to = ConstructorConnectionEdge(target, -direction);
            }
            var line = constructorLines[index];
            line.gameObject.SetActive(true); line.anchoredPosition = (from + to) * .5f;
            line.sizeDelta = new Vector2(Vector2.Distance(from, to), 2f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            line.GetComponent<Image>().color = color;
        }

        private static Vector2 ConstructorConnectionEdge(RectTransform cell, Vector2 direction)
        {
            Vector2 half = Vector2.Scale(cell.rect.size, cell.localScale) * .5f;
            float edge = 1f / Mathf.Max(Mathf.Abs(direction.x) / half.x, Mathf.Abs(direction.y) / half.y);
            return cell.anchoredPosition + direction * edge;
        }

        private void UpdateConstructorWorldLinks()
        {
            const float pixel = 1f / 32f;
            int lineIndex = 0, moduleIndex = 0;
            foreach (var module in station.State.SegmentedLinks)
            {
                var ring = station.Rings.First(r => r.RingId == module.RingId);
                if (!ring.IsActive) continue;
                if (moduleIndex == constructorWorldModules.Count)
                {
                    var created = station.CreatePixelVisual("Segmented module node", Color.white, Vector2.one, 2);
                    created.transform.SetParent(station.RuntimeRoot, false);
                    var marker = created.GetComponent<SpriteRenderer>();
                    marker.sharedMaterial = station.VisualMaterial;
                    constructorWorldModules.Add(marker);
                    var crossObject = station.CreatePixelVisual("Suppressed module cross",Color.white,Vector2.one,4);
                    crossObject.transform.SetParent(station.RuntimeRoot,false);
                    var cross = crossObject.GetComponent<SpriteRenderer>();
                    cross.sprite = SegmentedModulePresentation.SuppressionCross(); cross.sharedMaterial = station.VisualMaterial;
                    cross.enabled = false; constructorWorldModuleCrosses.Add(cross);
                }
                var icon = OrbitalRewardIconResolver.Resolve(module.Kind);
                var node = constructorWorldModules[moduleIndex++];
                node.sprite = icon.Sprite;
                Vector2 size = node.sprite.bounds.size;
                node.transform.localScale = new Vector3(SegmentedModulePresentation.WorldSize / size.x,
                    SegmentedModulePresentation.WorldSize / size.y, 1f);
                node.transform.position = ring.Mounts[module.MountIndex].Transform.position;
                node.transform.localRotation = Quaternion.identity;
                bool active = station.State.SegmentedModuleTargets(module).Any();
                node.color = icon.ImageTint * new Color(1f, 1f, 1f, active ? 1f : .65f);
                node.enabled = !station.InputOwner.IsConstructing;
                var disabled = constructorWorldModuleCrosses[moduleIndex - 1];
                disabled.transform.position = node.transform.position;
                disabled.transform.localScale = Vector3.one * SegmentedModulePresentation.WorldSize * 1.12f;
                disabled.enabled = node.enabled && station.OrbitModulesSuppressed;
                float from = ring.Mounts[module.MountIndex].LocalPhase;
                foreach (var target in station.State.SegmentedModuleTargets(module))
                {
                    // Cross-level effects remain visible in the constructor, without combat HUD spokes.
                    if (target.StableRingId != ring.RingId) continue;
                    float to = ring.Mounts[target.MountIndex].LocalPhase;
                    float arc = Mathf.DeltaAngle(from, to);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(arc) * Mathf.Deg2Rad * ring.Radius / pixel));
                    var rotation = Quaternion.Euler(0f, 0f, ring.Phase);
                    Bounds nodeBounds = node.bounds;
                    nodeBounds.Expand(pixel * 2f);
                    constructorArcPoints.Clear();
                    for (int step = 0; step <= steps; step++)
                    {
                        float distance = Mathf.Abs(arc) * Mathf.Deg2Rad * ring.Radius * step / steps;
                        if (distance < SegmentedModulePresentation.WorldSize * .5f + pixel) continue;
                        Vector2 point = PixelArcPoint(from + arc * step / steps, ring.Radius, pixel);
                        Vector3 worldPoint = station.RuntimeRoot.TransformPoint(rotation * (Vector3)point);
                        if (nodeBounds.Contains(new Vector3(worldPoint.x, worldPoint.y, nodeBounds.center.z))) continue;
                        if (constructorArcPoints.Count > 0)
                        {
                            Vector2 previous = constructorArcPoints[constructorArcPoints.Count - 1];
                            if (previous == point) continue;
                            if (previous.x != point.x && previous.y != point.y)
                            {
                                Vector2 a = new(previous.x, point.y), b = new(point.x, previous.y);
                                constructorArcPoints.Add(Mathf.Abs(a.sqrMagnitude - ring.Radius * ring.Radius) <
                                    Mathf.Abs(b.sqrMagnitude - ring.Radius * ring.Radius) ? (Vector3)a : (Vector3)b);
                            }
                        }
                        constructorArcPoints.Add(point);
                    }
                    if (constructorArcPoints.Count < 2) continue;
                    if (lineIndex == constructorWorldLines.Count)
                    {
                        var created = Instantiate(OrbitalPresentationConfig.Active.EnergyLinePrefab, station.RuntimeRoot, false);
                        created.name = "Segmented O-M arc";
                        created.useWorldSpace = false; created.widthMultiplier = 1f / 16f;
                        created.numCapVertices = created.numCornerVertices = 0;
                        // Mounted weapon groups are above this line, clipping its end to opaque art.
                        created.sortingOrder = 1;
                        constructorWorldLines.Add(created);
                    }
                    var line = constructorWorldLines[lineIndex++];
                    line.transform.localRotation = rotation;
                    line.positionCount = constructorArcPoints.Count;
                    for (int p = 0; p < constructorArcPoints.Count; p++) line.SetPosition(p, constructorArcPoints[p]);
                    line.startColor = line.endColor = station.OrbitModulesSuppressed
                        ? new Color(.2f,.32f,.36f,.2f) : SegmentedModulePresentation.LinkColor;
                    line.enabled = true;
                }
            }
            for (int i = lineIndex; i < constructorWorldLines.Count; i++) constructorWorldLines[i].enabled = false;
            for (int i = moduleIndex; i < constructorWorldModules.Count; i++) constructorWorldModules[i].enabled = false;
            for (int i = moduleIndex; i < constructorWorldModuleCrosses.Count; i++) constructorWorldModuleCrosses[i].enabled = false;
        }

        private static Vector2 PixelArcPoint(float degrees, float radius, float pixel)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Round(Mathf.Cos(angle) * radius / pixel) * pixel,
                Mathf.Round(Mathf.Sin(angle) * radius / pixel) * pixel);
        }

        private void ReleaseConstructor()
        {
            CloseConstructor();
            if (constructorDragIcon != null) Destroy(constructorDragIcon.gameObject);
            foreach (var line in constructorWorldLines) if (line != null) Destroy(line.gameObject);
            constructorWorldLines.Clear();
            foreach (var node in constructorWorldModules) if (node != null) Destroy(node.gameObject);
            constructorWorldModules.Clear();
            foreach (var cross in constructorWorldModuleCrosses) if (cross != null) Destroy(cross.gameObject);
            constructorWorldModuleCrosses.Clear();
            if (constructorCanvas != null) constructorCanvas.gameObject.SetActive(false);
        }
    }
}
