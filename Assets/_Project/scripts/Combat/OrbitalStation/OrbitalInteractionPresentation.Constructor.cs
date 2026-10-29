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
        [SerializeField] private Button constructorLauncher, constructorClose, constructorStow;
        [SerializeField] private Button[] constructorMounts, constructorWeapons, constructorLinks;
        [SerializeField] private OrbitalModuleKind[] constructorWeaponKinds;
        [SerializeField] private SegmentedLinkKind[] constructorModuleKinds;
        [SerializeField] private Image[] constructorSlotFrames, constructorSlotIcons;
        [SerializeField] private RectTransform[] constructorLines;
        [SerializeField] private TextMeshProUGUI constructorStatus, constructorEmpty;
        private UpgradePanelView constructorRewardPanel;
        private readonly List<LineRenderer> constructorWorldLines = new();
        [SerializeField] private string[] constructorWeaponLabels, constructorModuleLabels;
        private int constructorCell = -1, constructorRevision = -1;
        private bool constructorHidReward;
        private OrbitalModuleKind? constructorInstalling;
        private SegmentedLinkKind? constructorInstallingModule, constructorHoverModule;
        private string constructorMessage;
        private static readonly Color ConstructorBlue = new(.23f, .65f, .95f);
        private static readonly Color ConstructorGreen = new(.28f, .82f, .52f);
        private static readonly Color ConstructorAmber = new(.98f, .79f, .35f);

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
            BindButton(constructorStow, StowConstructorSelection);
            for (int i = 0; i < constructorMounts.Length; i++)
            { int cell = i; BindButton(constructorMounts[i], () => SelectConstructorMount(cell)); }
            for (int i = 0; i < constructorWeapons.Length; i++)
            { int item = i; BindButton(constructorWeapons[i], () => SelectConstructorWeapon(item)); }
            for (int i = 0; i < constructorLinks.Length; i++)
            {
                int item = i;
                BindButton(constructorLinks[i], () => SelectConstructorModule(item));
                var trigger = constructorLinks[i].GetComponent<EventTrigger>();
                trigger.triggers.Clear();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => PreviewConstructorModule(constructorModuleKinds[item]));
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => PreviewConstructorModule(null));
                trigger.triggers.Add(enter); trigger.triggers.Add(exit);
            }
            constructorPanel.SetActive(false);
            constructorLauncher.gameObject.SetActive(false);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
        private static void ButtonText(Button button, string text) => button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;

        public bool OpenConstructor()
        {
            if (constructorRewardPanel == null || !station.InputOwner.BeginConstructor()) return false;
            constructorHidReward = constructorRewardPanel.gameObject.activeSelf;
            constructorRewardPanel.SetConstructorCovered(true);
            constructorPanel.SetActive(true);
            ButtonText(constructorClose, "ПРОДОЛЖИТЬ");
            constructorLauncher.gameObject.SetActive(false);
            constructorCell = -1;
            ResetConstructorTool();
            var pending = station.RewardFlow.PendingReward;
            ConstructorMessage(!station.RewardFlow.IsAwaitingConstructor
                ? "Выберите O для оружия или M для модуля. Можно начать и с предмета в запасе."
                : pending == OrbitalRewardKind.NewRing ? "Орбита открыта. Выберите свободную O для оружия."
                : pending == OrbitalRewardKind.AddMount ? "Лимит оружия увеличен. Выберите O и оружие в запасе."
                : pending.HasValue && OrbitalRewardProvider.GetModuleKind(pending.Value).HasValue
                    ? "Оружие в запасе. Выберите синюю O — или начните с оружия справа."
                    : "Модуль в запасе. Выберите зелёную M рядом с пушкой — или начните с модуля справа.");
            RefreshConstructor();
            return true;
        }

        public void CloseConstructor()
        {
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
            constructorCell = -1;
            ResetConstructorTool();
        }

        private OrbitalMountRuntime ConstructorMount(int cell) => station.Rings[cell / OrbitalRunState.SegmentedPositions].Mounts[cell % OrbitalRunState.SegmentedPositions];
        private OrbitalModuleRuntime ConstructorGun(int cell) => cell < 0 ? null : ConstructorMount(cell).Module;
        private SegmentedLinkState ConstructorModule(int cell) => cell < 0 ? null :
            station.State.SegmentedModuleAt(ConstructorMount(cell).Ring.RingId, ConstructorMount(cell).MountIndex);
        private static bool IsWeaponCell(int cell) => OrbitalRunState.IsSegmentedWeaponSlot(cell / OrbitalRunState.SegmentedPositions, cell % OrbitalRunState.SegmentedPositions);
        private void ResetConstructorTool()
        { constructorInstalling = null; constructorInstallingModule = constructorHoverModule = null; constructorRevision = -1; }
        private void ConstructorMessage(string message) { constructorMessage = message; constructorRevision = -1; }

        public bool CancelConstructorSelection()
        {
            if (!station.InputOwner.CanEditConstructor || constructorCell < 0 &&
                !constructorInstalling.HasValue && !constructorInstallingModule.HasValue) return false;
            constructorCell = -1; ResetConstructorTool();
            ConstructorMessage("Выбор отменён. Выберите ячейку O / M или предмет справа.");
            return true;
        }

        public void PreviewConstructorModule(SegmentedLinkKind? kind)
        {
            if (!station.InputOwner.CanEditConstructor) return;
            constructorHoverModule = kind; constructorRevision = -1;
        }

        public bool SelectConstructorWeapon(int item)
        {
            if (!station.InputOwner.CanEditConstructor) return false;
            var kind = constructorWeaponKinds[item];
            if (!station.State.SegmentedWeapons.Any(gun => gun.ModuleType == kind)) return false;
            ResetConstructorTool();
            if (constructorCell >= 0 && IsWeaponCell(constructorCell) && ConstructorGun(constructorCell) == null)
            {
                var mount = ConstructorMount(constructorCell);
                return ConstructorApplied(station.ConstructWeapon(kind, mount.Ring.RingId, mount.MountIndex, out string error), error);
            }
            constructorCell = -1; constructorInstalling = kind;
            ConstructorMessage("Выберите подсвеченную синюю O для оружия. Esc — отмена.");
            return true;
        }

        public bool SelectConstructorModule(int item)
        {
            if (!station.InputOwner.CanEditConstructor) return false;
            var kind = constructorModuleKinds[item];
            if (!station.State.SegmentedLinkStock.Contains(kind)) return false;
            ResetConstructorTool();
            if (constructorCell >= 0 && !IsWeaponCell(constructorCell))
            {
                var mount = ConstructorMount(constructorCell);
                return ConstructorApplied(station.ConstructModule(kind, mount.Ring.RingId, mount.MountIndex, out string error), error);
            }
            constructorCell = -1; constructorInstallingModule = kind;
            ConstructorMessage("Выберите подсвеченную зелёную M. Достаточно одной соседней стреляющей пушки.");
            return true;
        }

        public bool SelectConstructorMount(int cell)
        {
            if (!station.InputOwner.CanEditConstructor || cell < 0 || cell >= constructorMounts.Length) return false;
            var target = ConstructorMount(cell);
            if (!station.State.IsRingActive(target.Ring.State)) return false;
            bool weaponCell = IsWeaponCell(cell);
            var sourceWeapon = ConstructorGun(constructorCell);
            var sourceModule = ConstructorModule(constructorCell);
            if (weaponCell && target.Module == null)
            {
                if (constructorInstalling.HasValue)
                {
                    int destination = cell;
                    bool changed = station.ConstructWeapon(constructorInstalling.Value, target.Ring.RingId, target.MountIndex, out string error);
                    if (changed) constructorCell = destination;
                    return ConstructorApplied(changed, error);
                }
                if (sourceWeapon != null)
                {
                    bool changed = station.RepositionWeapon(sourceWeapon.StableModuleId, target.Ring.RingId, target.MountIndex, out string error);
                    if (changed) constructorCell = cell;
                    return ConstructorApplied(changed, error);
                }
            }
            if (!weaponCell)
            {
                if (constructorInstallingModule.HasValue)
                {
                    bool changed = station.ConstructModule(constructorInstallingModule.Value, target.Ring.RingId, target.MountIndex, out string error);
                    if (changed) constructorCell = cell;
                    return ConstructorApplied(changed, error);
                }
                if (sourceModule != null && ConstructorModule(cell) == null)
                {
                    bool changed = station.RepositionModule(station.State.SegmentedLinks.IndexOf(sourceModule), target.Ring.RingId, target.MountIndex, out string error);
                    if (changed) constructorCell = cell;
                    return ConstructorApplied(changed, error);
                }
            }
            ResetConstructorTool(); constructorCell = cell;
            if (weaponCell)
                ConstructorMessage(target.Module == null
                    ? "Синяя O выбрана. Выберите оружие в запасе справа."
                    : WeaponLabel(target.Module.Kind) + " установлен. Выберите свободную O для переноса или верните оружие в запас.");
            else
            {
                var module = ConstructorModule(cell);
                ConstructorMessage(module != null
                    ? station.State.SegmentedModuleTargets(module).Any()
                        ? ModuleLabel(module.Kind) + " работает с соседними пушками. Можно перенести в M или вернуть в запас."
                        : ModuleLabel(module.Kind) + ": рядом нет стреляющей пушки. Модуль сохранён; его можно перенести в M."
                    : "Зелёная M выбрана. Справа подсвечены подходящие модули. Рядом нужна хотя бы одна стреляющая пушка.");
            }
            return true;
        }

        private bool ConstructorApplied(bool changed, string error)
        {
            if (!changed) { ConstructorMessage(error); return false; }
            ResetConstructorTool();
            ConstructorMessage("Установлено. Предмет можно перенести или вернуть в запас.");
            return true;
        }

        private string WeaponLabel(OrbitalModuleKind kind) => constructorWeaponLabels[Array.IndexOf(constructorWeaponKinds, kind)];
        private string ModuleLabel(SegmentedLinkKind kind) => constructorModuleLabels[Array.IndexOf(constructorModuleKinds, kind)];

        private void StowConstructorSelection()
        {
            var gun = ConstructorGun(constructorCell);
            var module = ConstructorModule(constructorCell);
            bool changed = gun != null ? station.StowWeapon(gun.StableModuleId) :
                module != null && station.StowModule(station.State.SegmentedLinks.IndexOf(module));
            if (!changed) return;
            constructorCell = -1; ResetConstructorTool();
            ConstructorMessage("Предмет возвращён в запас.");
        }

        private bool CanPlaceConstructorWeapon(OrbitalModuleKind kind, int cell)
        {
            var mount = ConstructorMount(cell);
            return IsWeaponCell(cell) && station.State.CanInstallModule(kind, mount.Ring.RingId, mount.MountIndex, out _);
        }
        private bool CanPlaceConstructorModule(SegmentedLinkKind kind, int cell)
        {
            var mount = ConstructorMount(cell);
            return !IsWeaponCell(cell) && station.State.CanInstallSegmentedModule(kind, mount.Ring.RingId, mount.MountIndex, out _);
        }

        private void UpdateConstructor()
        {
            if (station == null || !station.IsInitialized || !station.UsesSegmentedOrbit) return;
            constructorCanvas.gameObject.SetActive(true);
            if (station.InputOwner.IsConstructing && !station.InputOwner.CanEditConstructor) CloseConstructor();
            constructorLauncher.gameObject.SetActive(station.InputOwner.IsChoosingReward && station.InputOwner.CanOpenConstructor && constructorRewardPanel != null);
            if (station.InputOwner.IsConstructing && constructorRevision != station.State.Revision) RefreshConstructor();
            UpdateConstructorWorldLinks();
        }

        private void RefreshConstructor()
        {
            var state = station.State;
            constructorRevision = state.Revision; constructorStatus.text = constructorMessage;
            var selectedWeapon = ConstructorGun(constructorCell);
            var selectedModule = ConstructorModule(constructorCell);
            var previewModule = constructorHoverModule ?? constructorInstallingModule;
            for (int i = 0; i < constructorMounts.Length; i++)
            {
                var mount = ConstructorMount(i);
                var module = ConstructorModule(i);
                bool weaponCell = IsWeaponCell(i), unlocked = state.IsRingActive(mount.Ring.State);
                bool candidate = false;
                if (previewModule.HasValue) candidate = CanPlaceConstructorModule(previewModule.Value, i);
                else if (constructorInstalling.HasValue) candidate = CanPlaceConstructorWeapon(constructorInstalling.Value, i);
                else if (selectedWeapon != null && weaponCell)
                    candidate = !mount.Occupied && state.CanMoveModule(selectedWeapon.StableModuleId, mount.Ring.RingId, mount.MountIndex, out _);
                else if (selectedModule != null && !weaponCell)
                    candidate = state.CanMoveSegmentedModule(state.SegmentedLinks.IndexOf(selectedModule), mount.Ring.RingId, mount.MountIndex, out _);
                var typeColor = weaponCell ? ConstructorBlue : ConstructorGreen;
                bool occupied = weaponCell ? mount.Occupied : module != null;
                constructorMounts[i].interactable = unlocked;
                bool dormant = module != null && !state.SegmentedModuleTargets(module).Any();
                constructorSlotFrames[i].color = i == constructorCell ? ConstructorAmber :
                    candidate ? Color.white : new Color(typeColor.r, typeColor.g, typeColor.b, unlocked ? occupied && !dormant ? 1f : .55f : .14f);
                constructorMounts[i].targetGraphic.color = Color.Lerp(new Color(.025f, .045f, .075f), typeColor,
                    unlocked ? candidate ? .45f : occupied ? .26f : .08f : .025f);
                var label = constructorMounts[i].GetComponentInChildren<TextMeshProUGUI>();
                label.text = weaponCell ? "O" : "M";
                label.color = new Color(typeColor.r, typeColor.g, typeColor.b, unlocked ? 1f : .3f);
                label.rectTransform.anchoredPosition = new Vector2(occupied ? -10f : 0f, 0f);
                constructorSlotIcons[i].gameObject.SetActive(occupied);
                if (occupied)
                {
                    var icon = weaponCell ? OrbitalRewardIconResolver.Resolve(mount.Module.Kind) : OrbitalRewardIconResolver.Resolve(module.Kind);
                    constructorSlotIcons[i].sprite = icon.Sprite; constructorSlotIcons[i].color = icon.ImageTint;
                }
            }
            bool? showWeapons = constructorCell >= 0 ? IsWeaponCell(constructorCell) :
                constructorInstalling.HasValue ? true : constructorInstallingModule.HasValue ? false : null;
            int weaponRows = 0, moduleRows = 0;
            for (int i = 0; i < constructorWeapons.Length; i++)
            {
                var kind = constructorWeaponKinds[i];
                int count = state.SegmentedWeapons.Count(gun => gun.ModuleType == kind);
                bool visible = count > 0 && showWeapons != false;
                constructorWeapons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                weaponRows++;
                bool allowed = constructorCell >= 0 && IsWeaponCell(constructorCell) && ConstructorGun(constructorCell) == null
                    ? CanPlaceConstructorWeapon(kind, constructorCell) : Enumerable.Range(0, constructorMounts.Length).Any(cell => CanPlaceConstructorWeapon(kind, cell));
                ButtonText(constructorWeapons[i], constructorWeaponLabels[i] + "  ×" + count);
                constructorWeapons[i].interactable = allowed;
                constructorWeapons[i].targetGraphic.color = constructorInstalling == kind ? Color.Lerp(ConstructorBlue, Color.black, .55f) : new Color(.045f, .095f, .14f);
            }
            for (int i = 0; i < constructorLinks.Length; i++)
            {
                var kind = constructorModuleKinds[i];
                int count = state.SegmentedLinkStock.Count(value => value == kind);
                bool visible = count > 0 && showWeapons != true;
                constructorLinks[i].gameObject.SetActive(visible);
                if (!visible) continue;
                moduleRows++;
                bool allowed = constructorCell >= 0 && !IsWeaponCell(constructorCell)
                    ? CanPlaceConstructorModule(kind, constructorCell) : Enumerable.Range(0, constructorMounts.Length).Any(cell => CanPlaceConstructorModule(kind, cell));
                ButtonText(constructorLinks[i], constructorModuleLabels[i] + "  ×" + count);
                constructorLinks[i].interactable = allowed;
                constructorLinks[i].targetGraphic.color = constructorInstallingModule == kind || allowed && constructorCell >= 0
                    ? Color.Lerp(ConstructorGreen, Color.black, .7f) : new Color(.045f, .1f, .075f);
            }
            constructorWeaponGroup.SetActive(weaponRows > 0); constructorModuleGroup.SetActive(moduleRows > 0);
            constructorEmpty.gameObject.SetActive(weaponRows + moduleRows == 0);
            constructorEmpty.text = showWeapons == true ? "Нет оружия в запасе.\nПолучите его в награду." :
                showWeapons == false ? "Нет модулей в запасе.\nПолучите их в награду." : "Запас пуст.\nНовые предметы приходят с наградами.";
            constructorStow.gameObject.SetActive(selectedWeapon != null || selectedModule != null);
            RefreshConstructorConnections();
        }

        private void RefreshConstructorConnections()
        {
            int lineIndex = 0;
            foreach (var module in station.State.SegmentedLinks)
            {
                int moduleCell = station.State.FindRing(module.RingId).Order * OrbitalRunState.SegmentedPositions + module.MountIndex;
                Vector2 from = constructorMounts[moduleCell].GetComponent<RectTransform>().anchoredPosition;
                foreach (var weapon in station.State.SegmentedModuleTargets(module))
                {
                    int cell = station.State.FindRing(weapon.StableRingId).Order * OrbitalRunState.SegmentedPositions + weapon.MountIndex;
                    Vector2 to = constructorMounts[cell].GetComponent<RectTransform>().anchoredPosition;
                    var line = constructorLines[lineIndex++];
                    line.gameObject.SetActive(true); line.anchoredPosition = (from + to) * .5f;
                    line.sizeDelta = new Vector2(Vector2.Distance(from, to), 2f);
                    line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                    line.GetComponent<Image>().color = moduleCell == constructorCell || cell == constructorCell ? ConstructorAmber : ConstructorGreen;
                }
            }
            while (lineIndex < constructorLines.Length) constructorLines[lineIndex++].gameObject.SetActive(false);
        }

        private void UpdateConstructorWorldLinks()
        {
            int lineIndex = 0;
            foreach (var module in station.State.SegmentedLinks)
            {
                Vector3 from = station.Rings.First(r => r.RingId == module.RingId).Mounts[module.MountIndex].Transform.position;
                foreach (var target in station.State.SegmentedModuleTargets(module))
                {
                    if (lineIndex == constructorWorldLines.Count)
                    {
                        var created = Instantiate(OrbitalPresentationConfig.Active.EnergyLinePrefab, station.RuntimeRoot, false);
                        created.useWorldSpace = true; created.positionCount = 2; created.widthMultiplier = .035f;
                        constructorWorldLines.Add(created);
                    }
                    var line = constructorWorldLines[lineIndex++];
                    line.enabled = true; line.SetPosition(0, from);
                    line.SetPosition(1, station.Modules.First(gun => gun.StableModuleId == target.StableModuleId).WorldPosition);
                    line.startColor = line.endColor = ConstructorGreen;
                }
            }
            for (int i = lineIndex; i < constructorWorldLines.Count; i++) constructorWorldLines[i].enabled = false;
        }

        private void ReleaseConstructor()
        {
            CloseConstructor();
            foreach (var line in constructorWorldLines) if (line != null) Destroy(line.gameObject);
            constructorWorldLines.Clear();
            if (constructorCanvas != null) constructorCanvas.gameObject.SetActive(false);
        }
    }
}
