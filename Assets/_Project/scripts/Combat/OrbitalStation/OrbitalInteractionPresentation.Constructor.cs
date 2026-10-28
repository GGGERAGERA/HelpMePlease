using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalInteractionPresentation
    {
        [Header("Segmented constructor — authored production UI")]
        [SerializeField] private Canvas constructorCanvas;
        [SerializeField] private GameObject constructorPanel;
        [SerializeField] private Button constructorLauncher, constructorClose, constructorMove, constructorStow, constructorDisconnect;
        [SerializeField] private Button[] constructorMounts, constructorWeapons, constructorLinks, constructorLinkBadges;
        [SerializeField] private RectTransform[] constructorLines;
        [SerializeField] private TextMeshProUGUI constructorStatus;
        [SerializeField] private TextMeshProUGUI constructorSummary;
        private UpgradePanelView constructorRewardPanel;
        private readonly List<LineRenderer> constructorWorldLines = new();
        private int constructorCell, constructorSelectedLink = -1, constructorRevision = -1;
        private bool constructorHidReward, constructorMoving;
        private OrbitalModuleKind? constructorInstalling;
        private SegmentedLinkKind? constructorConnecting;
        private string constructorMessage;
        private static readonly OrbitalModuleKind[] ConstructorWeaponKinds =
            { OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper, OrbitalModuleKind.Saw };
        private static readonly string[] ConstructorWeaponNames = { "Пистолет", "Дробовик", "Снайперка", "Пила" };
        private static readonly string[] ConstructorLinkNames = { "Урон +20%", "Темп +20%", "Доп. выстрел", "Рикошет +1" };
        private static readonly string[] ConstructorLinkSymbols = { "DMG", "FR", "2X", "RIC" };
        private static readonly Color ConstructorCyan = new(.2f, .82f, .9f), ConstructorAmber = new(1f, .75f, .3f);

        public void BindRewardPanel(UpgradePanelView panel)
        {
            CloseConstructor();
            constructorRewardPanel = panel;
        }

        private void BindConstructor()
        {
            // Shared station prefabs in other scenes keep this surface inactive.
            if (!station.UsesSegmentedOrbit) return;
            BindButton(constructorLauncher, () => OpenConstructor());
            BindButton(constructorClose, CloseConstructor);
            BindButton(constructorMove, () => { ResetConstructorTool(); constructorMoving = true; ConstructorMessage("Выберите свободную точку для перестановки."); });
            BindButton(constructorStow, () =>
            {
                var gun = ConstructorGun(constructorCell);
                if (gun != null && station.StowWeapon(gun.StableModuleId))
                { ResetConstructorTool(); ConstructorMessage("Оружие и модули его связей возвращены в запас."); }
            });
            BindButton(constructorDisconnect, () =>
            {
                if (station.DisconnectWeapons(constructorSelectedLink))
                { constructorSelectedLink = -1; ConstructorMessage("Модуль связи возвращён в запас."); }
            });
            for (int i = 0; i < constructorMounts.Length; i++)
            { int cell = i; BindButton(constructorMounts[i], () => SelectConstructorMount(cell)); }
            for (int i = 0; i < constructorWeapons.Length; i++)
            {
                int kind = i;
                BindButton(constructorWeapons[i], () =>
                { ResetConstructorTool(); constructorInstalling = ConstructorWeaponKinds[kind]; ConstructorMessage("Выберите свободную точку: " + ConstructorWeaponNames[kind] + "."); });
            }
            for (int i = 0; i < constructorLinks.Length; i++)
            {
                int kind = i;
                BindButton(constructorLinks[i], () =>
                { ResetConstructorTool(); constructorConnecting = (SegmentedLinkKind)kind; ConstructorMessage("Выберите две соседние пушки. Не более двух активных связей на пушку."); });
            }
            for (int i = 0; i < constructorLinkBadges.Length; i++)
            {
                int index = i;
                BindButton(constructorLinkBadges[i], () =>
                {
                    ResetConstructorTool(); constructorSelectedLink = index;
                    var link = station.State.SegmentedLinks[index];
                    ConstructorMessage(ConstructorLinkNames[(int)link.Kind] + (station.State.ActiveSegmentedLinks().Contains(link)
                        ? " · Активна" : " · Неактивна: соседство или предел двух активных связей."));
                });
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
            ButtonText(constructorClose, station.RewardFlow.IsAwaitingConstructor ? "ПРОДОЛЖИТЬ" : "К НАГРАДЕ");
            constructorLauncher.gameObject.SetActive(false);
            constructorSelectedLink = -1; constructorCell = 0;
            ResetConstructorTool();
            foreach (var ring in station.Rings) ring.SetActive(station.State.IsRingActive(ring.State), true);
            ConstructorMessage(station.RewardFlow.IsAwaitingConstructor
                ? "Модуль получен. Выберите его в запасе и соедините две пушки. Можно сохранить на потом и вернуться в бой."
                : "Выберите оружие из запаса и свободную точку. Выбор награды остаётся открытым.");
            RefreshConstructor();
            return true;
        }

        public void CloseConstructor()
        {
            if (station?.InputOwner != null)
            {
                station.InputOwner.EndConstructor();
                station.RewardFlow?.CompleteConstructorReward();
                if (constructorHidReward && constructorRewardPanel != null && station.InputOwner.IsChoosingReward)
                    constructorRewardPanel.SetConstructorCovered(false);
            }
            constructorHidReward = false;
            if (constructorPanel != null) constructorPanel.SetActive(false);
            ResetConstructorTool();
        }

        private OrbitalModuleRuntime ConstructorGun(int cell) => station.Rings[cell / 12].Mounts[cell % 12].Module;
        private void ResetConstructorTool()
        { constructorInstalling = null; constructorConnecting = null; constructorMoving = false; constructorRevision = -1; }
        private void ConstructorMessage(string message) { constructorMessage = message; constructorRevision = -1; }

        // UI and the bounded Editor check enter through the same state-gated commands.
        public bool SelectConstructorMount(int cell)
        {
            if (!station.InputOwner.CanEditConstructor || cell < 0 || cell >= 36) return false;
            var target = station.Rings[cell / 12].Mounts[cell % 12];
            if (!station.State.IsRingActive(target.Ring.State))
            { ConstructorMessage("Сначала получите награду «Открыть орбиту»."); return false; }
            var source = ConstructorGun(constructorCell);
            bool changed = false;
            string error = null;
            if (constructorInstalling.HasValue)
                changed = station.ConstructWeapon(constructorInstalling.Value, target.Ring.RingId, target.MountIndex, out error);
            else if (constructorMoving && source != null)
                changed = station.RepositionWeapon(source.StableModuleId, target.Ring.RingId, target.MountIndex, out error);
            else if (constructorConnecting.HasValue && source != null && target.Module != null && cell != constructorCell)
                changed = station.ConnectWeapons(source.StableModuleId, target.Module.StableModuleId, constructorConnecting.Value, out error);
            else
            {
                constructorCell = cell; constructorSelectedLink = -1;
                ConstructorMessage(constructorConnecting.HasValue && target.Module != null
                    ? "Теперь выберите соседнюю пушку для связи: " + ConstructorLinkNames[(int)constructorConnecting.Value] + "."
                    : target.Module == null ? "Свободная точка. Выберите оружие из запаса." : "Оружие выбрано: можно переставить, снять или соединить.");
                return true;
            }
            if (changed)
            { constructorCell = cell; constructorSelectedLink = -1; ResetConstructorTool(); ConstructorMessage("Изменение применено."); }
            else ConstructorMessage(error ?? "Действие недоступно.");
            return changed;
        }

        private void UpdateConstructor()
        {
            if (station == null || !station.IsInitialized || !station.UsesSegmentedOrbit) return;
            constructorCanvas.gameObject.SetActive(true);
            if (station.InputOwner.IsConstructing && !station.InputOwner.CanEditConstructor) CloseConstructor();
            constructorLauncher.gameObject.SetActive(station.InputOwner.CanOpenConstructor && constructorRewardPanel != null);
            if (station.InputOwner.IsConstructing && constructorRevision != station.State.Revision) RefreshConstructor();
            UpdateConstructorWorldLinks();
        }

        private void RefreshConstructor()
        {
            var state = station.State;
            constructorRevision = state.Revision;
            constructorStatus.text = constructorMessage;
            constructorSummary.text = $"ОРБИТЫ {state.UnlockedSegmentedLevels}/3 · ОРУЖИЕ {state.Modules.Count}/{state.SegmentedWeaponLimit}";
            float radius = station.Rings[2].Radius;
            for (int i = 0; i < 36; i++)
            {
                var mount = station.Rings[i / 12].Mounts[i % 12];
                bool unlocked = state.IsRingActive(mount.Ring.State);
                constructorMounts[i].interactable = unlocked;
                Vector2 point = mount.Transform.position - station.transform.position;
                constructorMounts[i].GetComponent<RectTransform>().anchoredPosition = point * (184f / radius);
                constructorMounts[i].targetGraphic.color = i == constructorCell ? ConstructorAmber :
                    mount.Occupied ? ConstructorCyan : new Color(.12f, .23f, .28f);
                constructorMounts[i].GetComponentInChildren<TextMeshProUGUI>().color = i == constructorCell || mount.Occupied
                    ? new Color(.025f, .065f, .08f) : new Color(.83f, .93f, .94f);
                ButtonText(constructorMounts[i], !unlocked ? "×" : mount.Module == null ? (i % 12 + 1).ToString() :
                    mount.Module.Kind switch { OrbitalModuleKind.Pistol => "П", OrbitalModuleKind.Shotgun => "Д", OrbitalModuleKind.Sniper => "С", OrbitalModuleKind.Saw => "ПЛ", _ => "О" });
            }
            for (int i = 0; i < 4; i++)
            {
                int weapons = state.SegmentedWeapons.Count(gun => gun.ModuleType == ConstructorWeaponKinds[i]);
                ButtonText(constructorWeapons[i], ConstructorWeaponNames[i] + "  ×" + weapons);
                constructorWeapons[i].interactable = weapons > 0 && state.FreeBuiltMounts > 0;
                int links = state.SegmentedLinkStock.Count(kind => kind == (SegmentedLinkKind)i);
                ButtonText(constructorLinks[i], ConstructorLinkNames[i] + "  ×" + links);
                constructorLinks[i].interactable = links > 0;
            }
            bool selectedGun = ConstructorGun(constructorCell) != null;
            constructorMove.interactable = constructorStow.interactable = selectedGun;
            constructorDisconnect.interactable = constructorSelectedLink >= 0 && constructorSelectedLink < state.SegmentedLinks.Count;
            var active = state.ActiveSegmentedLinks().ToArray();
            for (int i = 0; i < constructorLines.Length; i++)
            {
                bool visible = i < state.SegmentedLinks.Count;
                constructorLines[i].gameObject.SetActive(visible); constructorLinkBadges[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var link = state.SegmentedLinks[i];
                var a = state.FindModule(link.First); var b = state.FindModule(link.Second);
                Vector2 from = constructorMounts[state.FindRing(a.StableRingId).Order * 12 + a.MountIndex].GetComponent<RectTransform>().anchoredPosition;
                Vector2 to = constructorMounts[state.FindRing(b.StableRingId).Order * 12 + b.MountIndex].GetComponent<RectTransform>().anchoredPosition;
                var line = constructorLines[i];
                line.anchoredPosition = (from + to) * .5f;
                line.sizeDelta = new Vector2(Vector2.Distance(from, to), 2f);
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                line.GetComponent<Image>().color = i == constructorSelectedLink ? ConstructorAmber : active.Contains(link) ? ConstructorCyan : Color.gray;
                var badge = constructorLinkBadges[i].GetComponent<RectTransform>();
                Vector2 direction = (to - from).normalized;
                badge.anchoredPosition = (from + to) * .5f + new Vector2(-direction.y, direction.x) * 18f;
                badge.sizeDelta = new Vector2(28f, 16f);
                constructorLinkBadges[i].GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta = badge.sizeDelta;
                ButtonText(constructorLinkBadges[i], ConstructorLinkSymbols[(int)link.Kind]);
            }
        }

        private void UpdateConstructorWorldLinks()
        {
            var links = station.State.SegmentedLinks;
            while (constructorWorldLines.Count < links.Count)
            {
                var line = Instantiate(OrbitalPresentationConfig.Active.EnergyLinePrefab, station.RuntimeRoot, false);
                line.enabled = true; line.useWorldSpace = true; line.positionCount = 2;
                constructorWorldLines.Add(line);
            }
            while (constructorWorldLines.Count > links.Count)
            { int last = constructorWorldLines.Count - 1; Destroy(constructorWorldLines[last].gameObject); constructorWorldLines.RemoveAt(last); }
            var active = station.State.ActiveSegmentedLinks().ToArray();
            for (int i = 0; i < links.Count; i++)
            {
                var a = station.Modules.First(gun => gun.StableModuleId == links[i].First);
                var b = station.Modules.First(gun => gun.StableModuleId == links[i].Second);
                var line = constructorWorldLines[i];
                line.SetPosition(0, a.WorldPosition); line.SetPosition(1, b.WorldPosition);
                line.widthMultiplier = .035f;
                Color color = ConstructorCyan; color.a = active.Contains(links[i]) ? .75f : .16f;
                line.startColor = line.endColor = color;
            }
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
