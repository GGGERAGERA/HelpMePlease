using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    /// <summary>
    /// A deliberately narrow playable route through systems already proven in the Lab.
    /// It owns presentation and sequencing only; Advanced Lab keeps every original control.
    /// </summary>
    public sealed class OrbitalGoldenPath : MonoBehaviour
    {
        private enum FlowState { Running, ChoosingCard, PlacingWeapon, ChoosingMount, ChoosingRing, FlyingWeapon, DeployingRing }
        private enum RewardKind { Weapon, RingUpgrade, CoreUpgrade }

        private readonly struct Reward
        {
            public readonly RewardKind Kind;
            public readonly OrbitalMountType Weapon;
            public readonly OrbitalRingUpgradeType RingUpgrade;
            public readonly OrbitalCoreUpgradeType CoreUpgrade;
            public readonly string Title;
            public readonly string Description;

            private Reward(RewardKind kind, OrbitalMountType weapon, OrbitalRingUpgradeType ringUpgrade,
                OrbitalCoreUpgradeType coreUpgrade, string title, string description)
            {
                Kind = kind;
                Weapon = weapon;
                RingUpgrade = ringUpgrade;
                CoreUpgrade = coreUpgrade;
                Title = title;
                Description = description;
            }

            public static Reward WeaponReward(OrbitalMountType type, string title, string description) =>
                new(RewardKind.Weapon, type, default, default, title, description);

            public static Reward RingReward(OrbitalRingUpgradeType type, string title, string description) =>
                new(RewardKind.RingUpgrade, default, type, default, title, description);

            public static Reward CoreReward(OrbitalCoreUpgradeType type, string title, string description) =>
                new(RewardKind.CoreUpgrade, default, default, type, title, description);
        }

        // All Golden Path timings live here. Values are active-play seconds; reward choices pause the clock.
        private static readonly float[] RingMilestones =
            { 45f, 90f, 135f, 180f, 225f, 270f, 315f, 360f, 405f, 450f, 495f };
        private const float FirstRewardAt = 22f;
        private const float RewardStep = 26f;
        private const int RewardCount = 18;

        public bool AdvancedLab { get; private set; }
        public bool SelectionActive => !AdvancedLab &&
            (state == FlowState.PlacingWeapon || state == FlowState.ChoosingMount ||
             state == FlowState.ChoosingRing || state == FlowState.FlyingWeapon);
        public int HoveredRing { get; private set; } = -1;
        public int CandidateSlot { get; private set; } = -1;
        public bool InvalidHoveredRing { get; private set; }
        public float Elapsed => elapsed;
        public int RewardIndex => rewardIndex;
        public bool FixedTest => fixedTest;

        public bool PointerOverUi
        {
            get
            {
                Vector2 pointer = Input.mousePosition;
                pointer.y = Screen.height - pointer.y;
                if (AdvancedLab) return AdvancedReturnRect.Contains(pointer);
                if (HudRect.Contains(pointer)) return true;
                if (menuOpen && MenuRect.Contains(pointer)) return true;
                if (state == FlowState.ChoosingCard && CardArea.Contains(pointer)) return true;
                return false;
            }
        }

        private Rect HudRect => new(12f, 12f, 370f, 132f);
        private Rect MenuRect => new(12f, 152f, 370f, 350f);
        private Rect CardArea => new(70f, Screen.height * .5f - 135f, Screen.width - 140f, 270f);
        private Rect AdvancedReturnRect => new(Screen.width - 222f, 12f, 210f, 34f);

        private readonly Reward[] cards = new Reward[3];
        private OrbitalCombatLabController lab;
        private FlowState state;
        private Reward selectedReward;
        private OrbitalMountedObject pendingWeapon;
        private OrbitalRing deployingRing;
        private float deployingTargetRadius;
        private float stateBorn;
        private Vector2 flightTarget;
        private int flightRing;
        private int flightSlot;
        private int placementRing = -1;
        private float elapsed;
        private float progressionSpeed = 1f;
        private int nextMilestoneIndex;
        private int rewardIndex;
        private uint randomState = 0x42A7u;
        private bool fixedTest = true;
        private bool menuOpen;
        private bool paused;
        private bool diagnosticsEnabled;
        private bool qaManualMode;
        private string lastAttachResult = "—";
        private string lastFailureReason = "—";
        private string banner = "";
        private float bannerUntil;
        private GUIStyle hudStyle;
        private GUIStyle titleStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardBodyStyle;
        private GUIStyle centerStyle;

        public void Configure(OrbitalCombatLabController controller) => lab = controller;

        public void BeginFullRun()
        {
            AdvancedLab = false;
            fixedTest = true;
            progressionSpeed = 1f;
            qaManualMode = false;
            menuOpen = false;
            ApplyBeginning();
        }

        public void ApplyBeginning()
        {
            PrepareReset();
            lab.ApplyStartState();
            ConfigureGoldenPresentation();
            lab.Crowd.SetCount(40, lab.PlayerPosition, lab.OuterRingRadius);
            lab.Rings[0].Settings.MaxMounts = 3;
            elapsed = 0f;
            rewardIndex = 0;
            nextMilestoneIndex = 0;
            state = FlowState.Running;
            ShowBanner("ТЕЛЕКИНЕТИЧЕСКОЕ ЯДРО АКТИВНО", 2.8f);
        }

        public void ApplyMidpoint()
        {
            PrepareReset();
            lab.ApplyStartState();
            ConfigureGoldenPresentation();
            lab.SetRingCount(6);
            ResetGoldenMountCapacityToBase();
            PrepareAllRings();
            lab.ClearMounted();
            AddBaselineGuns();
            lab.CreateGoldenMountedAt(1, 1, OrbitalMountType.Blade);
            lab.CreateGoldenMountedAt(2, 1, OrbitalMountType.Pusher);
            lab.CreateGoldenMountedAt(3, 1, OrbitalMountType.LinkNode);
            lab.CreateGoldenMountedAt(4, 1, OrbitalMountType.LinkNode);
            lab.CreateGoldenMountedAt(5, 1, OrbitalMountType.ArcEmitter);
            lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.PulseFrequency);
            ActivateCascade(false);
            lab.Crowd.SetCount(140, lab.PlayerPosition, lab.OuterRingRadius);
            lab.CameraRig.Snap(lab.PlayerPosition, lab.OuterRingRadius);
            elapsed = RingMilestones[4];
            rewardIndex = 8;
            nextMilestoneIndex = 5;
            state = FlowState.Running;
            ShowBanner("MIDPOINT · СЕТЬ СФОРМИРОВАНА", 2.8f);
        }

        public void ApplyFinale()
        {
            PrepareReset();
            lab.ApplyStartState();
            ConfigureGoldenPresentation();
            lab.SetRingCount(12);
            ResetGoldenMountCapacityToBase();
            PrepareAllRings();
            lab.ClearMounted();
            AddBaselineGuns();
            lab.CreateGoldenMountedAt(1, 1, OrbitalMountType.Blade);
            lab.CreateGoldenMountedAt(3, 1, OrbitalMountType.Pusher);
            lab.CreateGoldenMountedAt(6, 1, OrbitalMountType.Blade);
            lab.CreateGoldenMountedAt(7, 1, OrbitalMountType.Pusher);
            lab.CreateGoldenMountedAt(2, 2, OrbitalMountType.LinkNode);
            lab.CreateGoldenMountedAt(5, 2, OrbitalMountType.LinkNode);
            lab.CreateGoldenMountedAt(8, 2, OrbitalMountType.LinkNode);
            lab.CreateGoldenMountedAt(11, 2, OrbitalMountType.LinkNode);
            lab.CreateGoldenMountedAt(4, 1, OrbitalMountType.ArcEmitter);
            lab.CreateGoldenMountedAt(9, 1, OrbitalMountType.ArcEmitter);
            lab.ApplyRingUpgrade(5, OrbitalRingUpgradeType.Amplifier);
            lab.ApplyRingUpgrade(9, OrbitalRingUpgradeType.Overdrive);
            lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.PulseFrequency);
            lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.CorePower);
            lab.ApplyCoreUpgrade(OrbitalCoreUpgradeType.LinkMatrix);
            ActivateCascade(true);
            lab.Core.PulseInterval = 3.25f;
            lab.Crowd.SetCount(240, lab.PlayerPosition, lab.OuterRingRadius);
            lab.CameraRig.Snap(lab.PlayerPosition, lab.OuterRingRadius);
            elapsed = RingMilestones[10];
            rewardIndex = RewardCount;
            nextMilestoneIndex = RingMilestones.Length;
            state = FlowState.Running;
            ShowBanner("FINALE · ORBITAL CASCADE", 3.2f);
        }

        private void Update()
        {
            if (lab == null) return;
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ScreenCapture.CaptureScreenshot(QaCapturePath());
                ShowBanner("QA FRAME SAVED", 1.2f);
            }
            if (Input.GetKeyDown(KeyCode.F7) && !AdvancedLab) ApplyFinale();
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F2) && !AdvancedLab && state == FlowState.Running)
                OfferQaCards(Blade(), Gun(), Pusher());
            if (Input.GetKeyDown(KeyCode.F3) && !AdvancedLab && state == FlowState.Running)
                OfferQaCards(ExtraMount(), Overdrive(), Amplifier());
            if (Input.GetKeyDown(KeyCode.F4) && !AdvancedLab && state == FlowState.Running)
            {
                progressionSpeed = progressionSpeed > 1.5f ? 1f : 2f;
                ShowBanner(progressionSpeed > 1.5f ? "QA · ×2 PROGRESS" : "QA · NORMAL", 1.4f);
            }
            if (Input.GetKeyDown(KeyCode.F6) && !AdvancedLab) BeginFullRun();
#endif
            if (Input.GetKeyDown(KeyCode.F1) && !AdvancedLab && state == FlowState.Running)
                menuOpen = !menuOpen;
            HoveredRing = CandidateSlot = -1;
            InvalidHoveredRing = false;
            if (AdvancedLab) return;

            if (state == FlowState.ChoosingCard)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) { SelectReward(cards[0]); return; }
                if (Input.GetKeyDown(KeyCode.Alpha2)) { SelectReward(cards[1]); return; }
                if (Input.GetKeyDown(KeyCode.Alpha3)) { SelectReward(cards[2]); return; }
            }

            if (state == FlowState.PlacingWeapon) { TickWeaponPlacement(); return; }
            if (state == FlowState.ChoosingMount) { TickMountChoice(); return; }
            if (state == FlowState.ChoosingRing) { TickRingChoice(); return; }
            if (state == FlowState.FlyingWeapon) { TickWeaponFlight(); return; }
            if (state == FlowState.DeployingRing) { TickRingDeployment(); return; }
            if (state == FlowState.ChoosingCard || paused) return;
            if (qaManualMode) return;

            elapsed += Time.unscaledDeltaTime * progressionSpeed;
            if (nextMilestoneIndex < RingMilestones.Length && elapsed >= RingMilestones[nextMilestoneIndex])
            {
                BeginRingMilestone();
                return;
            }
            if (rewardIndex < RewardCount && elapsed >= FirstRewardAt + rewardIndex * RewardStep)
                OfferNextReward();
        }

        private void OnGUI()
        {
            if (lab == null) return;
            EnsureStyles();
            if (AdvancedLab)
            {
                if (GUI.Button(AdvancedReturnRect, "← ВЕРНУТЬСЯ В GOLDEN PATH")) ReturnToGoldenPath();
                if (GUI.Button(new Rect(AdvancedReturnRect.x, 54f, AdvancedReturnRect.width, 30f),
                    diagnosticsEnabled ? "GOLDEN DIAGNOSTICS: ON" : "GOLDEN DIAGNOSTICS: OFF"))
                    diagnosticsEnabled = !diagnosticsEnabled;
                if (diagnosticsEnabled) DrawAdvancedDiagnostics();
                return;
            }

            DrawHud();
            if (menuOpen) DrawMenu();
            if (state == FlowState.ChoosingCard) DrawCards();
            if (state == FlowState.PlacingWeapon || state == FlowState.ChoosingMount || state == FlowState.ChoosingRing)
                DrawPlacementPrompt();
            if (Time.unscaledTime < bannerUntil)
                GUI.Box(new Rect(Screen.width * .5f - 300f, 16f, 600f, 48f), banner, centerStyle);
        }

        private void DrawHud()
        {
            GUI.Box(HudRect, GUIContent.none);
            GUI.Label(new Rect(26f, 20f, 340f, 26f), "ORBITAL GOLDEN PATH", titleStyle);
            GUI.Label(new Rect(26f, 50f, 340f, 78f),
                $"ВРЕМЯ  {FormatTime(elapsed)}     УРОВЕНЬ СТАНЦИИ  {lab.RingCount}/12\n" +
                $"КОЛЬЦА  {lab.RingCount}     ОБЪЕКТЫ  {InstalledObjects()}     CORE  {lab.Core.Level}\n" +
                $"{CurrentFlowLabel()}\nF1 — меню     Tab — вся станция", hudStyle);
        }

        private void DrawMenu()
        {
            GUI.Box(MenuRect, GUIContent.none);
            float x = 24f;
            float y = 164f;
            GUI.Label(new Rect(x, y, 344f, 24f), "УПРАВЛЕНИЕ ПРОГОНОМ", cardTitleStyle);
            y += 30f;
            if (GUI.Button(new Rect(x, y, 108f, 30f), progressionSpeed < 1.5f ? "NORMAL ✓" : "NORMAL")) progressionSpeed = 1f;
            if (GUI.Button(new Rect(x + 116f, y, 108f, 30f), progressionSpeed > 1.5f ? "×2 ✓" : "×2 PROGRESS")) progressionSpeed = 2f;
            if (GUI.Button(new Rect(x + 232f, y, 108f, 30f), paused ? "ПРОДОЛЖИТЬ" : "ПАУЗА")) TogglePause();
            y += 38f;
            if (GUI.Button(new Rect(x, y, 108f, 30f), "NEXT REWARD")) OfferNextReward();
            if (GUI.Button(new Rect(x + 116f, y, 108f, 30f), "PREV STAGE")) PreviousStage();
            if (GUI.Button(new Rect(x + 232f, y, 108f, 30f), "RESTART")) BeginFullRun();
            y += 45f;
            GUI.Label(new Rect(x, y, 344f, 22f), "ПОСЛЕДОВАТЕЛЬНОСТЬ", cardTitleStyle);
            y += 27f;
            if (GUI.Button(new Rect(x, y, 164f, 30f), fixedTest ? "FIXED TEST ✓" : "FIXED TEST")) fixedTest = true;
            if (GUI.Button(new Rect(x + 176f, y, 164f, 30f), !fixedTest ? "RANDOM TEST ✓" : "RANDOM TEST")) fixedTest = false;
            y += 42f;
            GUI.Label(new Rect(x, y, 344f, 22f), "БЫСТРЫЕ ТОЧКИ", cardTitleStyle);
            y += 27f;
            if (GUI.Button(new Rect(x, y, 108f, 30f), "BEGINNING")) ApplyBeginning();
            if (GUI.Button(new Rect(x + 116f, y, 108f, 30f), "MIDPOINT")) ApplyMidpoint();
            if (GUI.Button(new Rect(x + 232f, y, 108f, 30f), "FINALE")) ApplyFinale();
            y += 38f;
            if (GUI.Button(new Rect(x, y, 164f, 32f), "FULL RUN")) BeginFullRun();
            if (GUI.Button(new Rect(x + 176f, y, 164f, 32f), "ADVANCED LAB")) OpenAdvancedLab();
        }

        private void DrawCards()
        {
            Rect area = CardArea;
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x, area.y + 12f, area.width, 32f), "ВЫБЕРИТЕ СЛЕДУЮЩЕЕ УСИЛЕНИЕ", centerStyle);
            float gap = 16f;
            float width = (area.width - 60f - gap * 2f) / 3f;
            for (int i = 0; i < cards.Length; i++)
            {
                Rect card = new(area.x + 30f + i * (width + gap), area.y + 56f, width, 176f);
                GUI.Box(card, GUIContent.none);
                GUI.Label(new Rect(card.x + 12f, card.y + 14f, card.width - 24f, 52f), cards[i].Title, cardTitleStyle);
                GUI.Label(new Rect(card.x + 12f, card.y + 68f, card.width - 24f, 58f), cards[i].Description, cardBodyStyle);
                if (GUI.Button(new Rect(card.x + 14f, card.yMax - 42f, card.width - 28f, 30f), $"{i + 1} · ВЫБРАТЬ")) SelectReward(cards[i]);
            }
        }

        private void DrawPlacementPrompt()
        {
            int ringIndex = placementRing >= 0 ? placementRing :
                HoveredRing >= 0 ? HoveredRing : Mathf.Clamp(lab.SelectedRing, 0, lab.RingCount - 1);
            OrbitalRing ring = lab.Rings[ringIndex];
            string roles = RingRoles(ring);
            string details;
            if (state == FlowState.ChoosingRing)
                details = lab.DescribeRingUpgrade(ringIndex, selectedReward.RingUpgrade);
            else if (state == FlowState.PlacingWeapon)
                details = InvalidHoveredRing ? "НЕТ СВОБОДНЫХ КРЕПЛЕНИЙ" :
                    HoveredRing >= 0 ? $"Свободно креплений: {FreeMountCount(ring)} · ЛКМ — выбрать кольцо" :
                    "Шаг 1/2 · наведите на кольцо";
            else
                details = CandidateSlot >= 0 ? $"Шаг 2/2 · крепление {CandidateSlot + 1}" :
                    "Шаг 2/2 · наведите на свободную точку";
            if (state == FlowState.ChoosingRing && selectedReward.RingUpgrade == OrbitalRingUpgradeType.Amplifier)
                details += $" · МОЩНОСТЬ {Roman(ring.DamageUpgradeLevel)} → {Roman(ring.DamageUpgradeLevel + 1)}";
            string controls = state == FlowState.ChoosingMount
                ? "ЛКМ — установить · Enter — первое свободное · цифры — другое кольцо"
                : "ЛКМ/1–9/0/-/= — выбрать кольцо · ПКМ/Esc — отменить";
            string text = $"{selectedReward.Title}\nКОЛЬЦО {ringIndex + 1} · {roles}\n{details}\n{controls}";
            GUI.Box(new Rect(Screen.width * .5f - 330f, Screen.height - 118f, 660f, 102f), text, centerStyle);
        }

        private void DrawAdvancedDiagnostics()
        {
            int ringIndex = placementRing >= 0 ? placementRing :
                Mathf.Clamp(HoveredRing >= 0 ? HoveredRing : lab.SelectedRing, 0, lab.RingCount - 1);
            OrbitalRing ring = lab.Rings[ringIndex];
            string pending = pendingWeapon == null ? "—" : pendingWeapon.Type.ToString();
            string text =
                $"GOLDEN PATH DIAGNOSTICS\n" +
                $"Reward State: {state}\n" +
                $"Selected Ring: {lab.SelectedRing + 1}\n" +
                $"Hovered Ring: {(HoveredRing >= 0 ? (HoveredRing + 1).ToString() : "—")}\n" +
                $"Selected Mount: {(CandidateSlot >= 0 ? (CandidateSlot + 1).ToString() : "—")}\n" +
                $"Free Mount Count: {FreeMountCount(ring)} / {ring.Settings.MaxMounts}\n" +
                $"Pending Weapon: {pending}\n" +
                $"Flight State: {(state == FlowState.FlyingWeapon ? $"{flightRing + 1}:{flightSlot + 1}" : "—")}\n" +
                $"Attach Result: {lastAttachResult}\n" +
                $"Failure: {lastFailureReason}";
            GUI.Box(new Rect(Screen.width - 422f, 92f, 410f, 238f), text, hudStyle);
        }

        private void OfferNextReward()
        {
            if (AdvancedLab || state != FlowState.Running || rewardIndex >= RewardCount) return;
            if (fixedTest) FillFixedCards(rewardIndex);
            else FillRandomCards();
            rewardIndex++;
            state = FlowState.ChoosingCard;
            SlowForChoice();
            menuOpen = false;
        }

#if UNITY_EDITOR
        private void OfferQaCards(Reward first, Reward second, Reward third)
        {
            qaManualMode = true;
            SetCards(first, second, third);
            state = FlowState.ChoosingCard;
            SlowForChoice();
            menuOpen = false;
        }
#endif

        private void SelectReward(Reward reward)
        {
            selectedReward = reward;
            Debug.Log($"[OrbitalGoldenPath] Reward selected: {reward.Title} ({reward.Kind})");
            lastFailureReason = "—";
            lastAttachResult = "—";
            placementRing = -1;
            if (reward.Kind == RewardKind.Weapon)
            {
                lab.Drag.CancelDrag();
                pendingWeapon = lab.CreateGoldenPendingMounted(reward.Weapon);
                if (pendingWeapon == null)
                {
                    lastFailureReason = "Не удалось создать preview оружия";
                    FinishChoice("Не удалось создать оружие.");
                    return;
                }
                state = FlowState.PlacingWeapon;
                lab.ShowMounts = true;
                return;
            }
            if (reward.Kind == RewardKind.RingUpgrade)
            {
                state = FlowState.ChoosingRing;
                lab.ShowMounts = false;
                return;
            }
            ApplyGoldenCoreUpgrade(reward.CoreUpgrade);
            FinishChoice(reward.Title + " АКТИВИРОВАН");
        }

        private void TickWeaponPlacement()
        {
            if (CancelPressed()) { CancelPendingWeapon(); FinishChoice("НАГРАДА ОТМЕНЕНА"); return; }
            if (pendingWeapon == null || Camera.main == null) { FinishChoice("РАЗМЕЩЕНИЕ ОТМЕНЕНО"); return; }
            if (TryKeyboardRing(out int keyboardRing))
            {
                SelectPlacementRing(keyboardRing);
                return;
            }
            Vector2 world = MouseWorld();
            pendingWeapon.SetDraggedPosition(world);
            HoveredRing = FindNearestRing(world, Mathf.Max(.62f, lab.OuterRingRadius * .028f));
            if (HoveredRing >= 0)
            {
                lab.SelectedRing = HoveredRing;
                InvalidHoveredRing = FreeMountCount(lab.Rings[HoveredRing]) == 0;
            }
            pendingWeapon.SetDragValidity(HoveredRing >= 0 && !InvalidHoveredRing);
            lab.Pattern.ClearGoldenLinkPreview();
            if (PointerOverUi || HoveredRing < 0 || !Input.GetMouseButtonDown(0)) return;
            SelectPlacementRing(HoveredRing);
        }

        private void TickMountChoice()
        {
            if (CancelPressed()) { CancelPendingWeapon(); FinishChoice("НАГРАДА ОТМЕНЕНА"); return; }
            if (pendingWeapon == null || placementRing < 0 || placementRing >= lab.RingCount)
            {
                lastFailureReason = "Потеряно выбранное кольцо или preview";
                state = FlowState.PlacingWeapon;
                placementRing = -1;
                return;
            }
            if (TryKeyboardRing(out int keyboardRing))
            {
                SelectPlacementRing(keyboardRing);
                return;
            }

            OrbitalRing ring = lab.Rings[placementRing];
            HoveredRing = placementRing;
            lab.SelectedRing = placementRing;
            Vector2 world = MouseWorld();
            pendingWeapon.SetDraggedPosition(world);
            CandidateSlot = FindFreeMountNear(ring, world);
            pendingWeapon.SetDragValidity(CandidateSlot >= 0);
            if (selectedReward.Weapon == OrbitalMountType.LinkNode && CandidateSlot >= 0)
                lab.Pattern.ShowGoldenLinkPreview(ring.GetSlotPosition(lab.PlayerPosition, CandidateSlot));
            else lab.Pattern.ClearGoldenLinkPreview();

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                CandidateSlot = FirstFreeSlot(ring);
            if (CandidateSlot < 0)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    lastFailureReason = FreeMountCount(ring) == 0
                        ? "НЕТ СВОБОДНЫХ КРЕПЛЕНИЙ"
                        : "Клик не попал в свободное крепление";
                    InvalidHoveredRing = true;
                    ring.FlashField(.35f);
                    ShowBanner(lastFailureReason, 1.8f);
                }
                return;
            }
            if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Return) &&
                !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
            BeginWeaponFlight(placementRing, CandidateSlot);
        }

        private void TickWeaponFlight()
        {
            if (pendingWeapon == null) { FinishChoice("РАЗМЕЩЕНИЕ ОТМЕНЕНО"); return; }
            float t = Mathf.Clamp01((Time.unscaledTime - stateBorn) / .58f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            pendingWeapon.SetDraggedPosition(Vector2.Lerp(lab.PlayerPosition, flightTarget, eased));
            pendingWeapon.SetDragValidity(true);
            if (t < 1f) return;
            if (!lab.AttachGoldenPendingMounted(pendingWeapon, flightRing, flightSlot, out string failureReason))
            {
                lastAttachResult = "FAILED";
                lastFailureReason = failureReason;
                pendingWeapon.SetDraggedPosition(lab.PlayerPosition);
                pendingWeapon.SetDragValidity(false);
                placementRing = -1;
                state = FlowState.PlacingWeapon;
                SlowForChoice();
                ShowBanner(failureReason, 2.2f);
                Debug.LogWarning($"[OrbitalGoldenPath] Attach failed: {failureReason}");
                return;
            }
            lastAttachResult = $"OK · кольцо {flightRing + 1}, крепление {flightSlot + 1}";
            lastFailureReason = "—";
            string title = selectedReward.Title + " УСТАНОВЛЕН";
            pendingWeapon = null;
            placementRing = -1;
            Debug.Log($"[OrbitalGoldenPath] Attach OK: ring {flightRing + 1}, mount {flightSlot + 1}");
            FinishChoice(title);
        }

        private void TickRingChoice()
        {
            ResetRingPreviews();
            if (CancelPressed()) { FinishChoice("НАГРАДА ОТМЕНЕНА"); return; }
            if (TryKeyboardRing(out int keyboardRing))
            {
                ApplySelectedRingUpgrade(keyboardRing);
                return;
            }
            if (Camera.main == null || PointerOverUi) return;
            Vector2 world = MouseWorld();
            HoveredRing = FindNearestRing(world, Mathf.Max(.42f, lab.OuterRingRadius * .022f));
            if (HoveredRing < 0) return;
            lab.SelectedRing = HoveredRing;
            OrbitalRing ring = lab.Rings[HoveredRing];
            ring.FlashUpgrade(.06f);
            if (selectedReward.RingUpgrade == OrbitalRingUpgradeType.Overdrive) ring.PreviewRotationMultiplier = 1.25f;
            if (!Input.GetMouseButtonDown(0)) return;
            ApplySelectedRingUpgrade(HoveredRing);
        }

        private void ApplySelectedRingUpgrade(int ringIndex)
        {
            OrbitalRing ring = lab.Rings[ringIndex];
            if (selectedReward.RingUpgrade == OrbitalRingUpgradeType.ExtraMount &&
                ring.Settings.MaxMounts >= OrbitalRing.AbsoluteMaxMounts)
            {
                HoveredRing = ringIndex;
                InvalidHoveredRing = true;
                lastFailureReason = "Кольцо уже имеет максимум креплений";
                ring.FlashField(.4f);
                ShowBanner(lastFailureReason, 2f);
                return;
            }

            int previousCapacity = ring.Settings.MaxMounts;
            int previousBonus = ring.Upgrades.MountCapacityBonus;
            OrbitalMountedObject[] occupants = new OrbitalMountedObject[ring.Mounts.Length];
            System.Array.Copy(ring.Mounts, occupants, ring.Mounts.Length);
            lab.ApplyRingUpgrade(ringIndex, selectedReward.RingUpgrade);
            if (selectedReward.RingUpgrade == OrbitalRingUpgradeType.ExtraMount)
            {
                bool preserved = true;
                for (int i = 0; i < occupants.Length; i++)
                    if (occupants[i] != null && ring.Mounts[i] != occupants[i]) preserved = false;
                bool increased = ring.Settings.MaxMounts == previousCapacity + 1 &&
                    ring.Upgrades.MountCapacityBonus == previousBonus + 1;
                if (!increased || !preserved)
                {
                    lastAttachResult = "MOUNT UPGRADE FAILED";
                    lastFailureReason = !increased ? "Фактическая ёмкость не увеличилась" :
                        "Установленные объекты не сохранились";
                    ShowBanner(lastFailureReason, 2.4f);
                    return;
                }
                lastAttachResult = $"MOUNT {previousCapacity} → {ring.Settings.MaxMounts}; старые объекты сохранены";
                Debug.Log($"[OrbitalGoldenPath] {lastAttachResult}; free={FreeMountCount(ring)}");
            }
            lastFailureReason = "—";
            FinishChoice(selectedReward.Title + " · КОЛЬЦО " + (ringIndex + 1));
        }

        private void SelectPlacementRing(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= lab.RingCount) return;
            OrbitalRing ring = lab.Rings[ringIndex];
            HoveredRing = ringIndex;
            lab.SelectedRing = ringIndex;
            if (FreeMountCount(ring) <= 0)
            {
                InvalidHoveredRing = true;
                lastFailureReason = "НЕТ СВОБОДНЫХ КРЕПЛЕНИЙ";
                ring.FlashField(.4f);
                ShowBanner(lastFailureReason, 2f);
                return;
            }
            placementRing = ringIndex;
            CandidateSlot = -1;
            state = FlowState.ChoosingMount;
            lastFailureReason = "—";
            Debug.Log($"[OrbitalGoldenPath] Ring selected: {ringIndex + 1}; free mounts={FreeMountCount(ring)}");
        }

        private void BeginWeaponFlight(int ringIndex, int slot)
        {
            if (ringIndex < 0 || ringIndex >= lab.RingCount || slot < 0 ||
                slot >= lab.Rings[ringIndex].Settings.MaxMounts || lab.Rings[ringIndex].Mounts[slot] != null)
            {
                lastFailureReason = "Выбранное крепление больше не свободно";
                state = FlowState.ChoosingMount;
                ShowBanner(lastFailureReason, 2f);
                return;
            }
            flightRing = ringIndex;
            flightSlot = slot;
            flightTarget = lab.Rings[flightRing].GetSlotPosition(lab.PlayerPosition, flightSlot);
            pendingWeapon.SetDraggedPosition(lab.PlayerPosition);
            stateBorn = Time.unscaledTime;
            state = FlowState.FlyingWeapon;
            lab.Pattern.ClearGoldenLinkPreview();
            lab.CoreSystem.ForcePulse();
            Debug.Log($"[OrbitalGoldenPath] Flight started: ring {ringIndex + 1}, mount {slot + 1}");
        }

        private int FindFreeMountNear(OrbitalRing ring, Vector2 world)
        {
            int best = -1;
            float threshold = Mathf.Max(.34f, lab.OuterRingRadius * .012f);
            float bestSqr = threshold * threshold;
            int max = Mathf.Min(ring.Settings.MaxMounts, ring.Mounts.Length);
            for (int i = 0; i < max; i++)
            {
                if (ring.Mounts[i] != null) continue;
                float sqr = (ring.GetSlotPosition(lab.PlayerPosition, i) - world).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        private bool TryKeyboardRing(out int ringIndex)
        {
            ringIndex = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) ringIndex = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) ringIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) ringIndex = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) ringIndex = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) ringIndex = 4;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) ringIndex = 5;
            else if (Input.GetKeyDown(KeyCode.Alpha7)) ringIndex = 6;
            else if (Input.GetKeyDown(KeyCode.Alpha8)) ringIndex = 7;
            else if (Input.GetKeyDown(KeyCode.Alpha9)) ringIndex = 8;
            else if (Input.GetKeyDown(KeyCode.Alpha0)) ringIndex = 9;
            else if (Input.GetKeyDown(KeyCode.Minus)) ringIndex = 10;
            else if (Input.GetKeyDown(KeyCode.Equals)) ringIndex = 11;
            return ringIndex >= 0 && ringIndex < lab.RingCount;
        }

        private static int FirstFreeSlot(OrbitalRing ring)
        {
            int max = Mathf.Min(ring.Settings.MaxMounts, ring.Mounts.Length);
            for (int i = 0; i < max; i++) if (ring.Mounts[i] == null) return i;
            return -1;
        }

        private static int FreeMountCount(OrbitalRing ring)
        {
            int free = 0;
            int max = Mathf.Min(ring.Settings.MaxMounts, ring.Mounts.Length);
            for (int i = 0; i < max; i++) if (ring.Mounts[i] == null) free++;
            return free;
        }

        private void BeginRingMilestone()
        {
            nextMilestoneIndex++;
            if (!lab.AddRing()) return;
            deployingRing = lab.Rings[lab.RingCount - 1];
            deployingRing.Settings.Shape = OrbitalShape.Circle;
            deployingRing.Settings.FieldMode = OrbitalRingFieldMode.Ghost;
            deployingRing.Settings.MaxMounts = 3;
            deployingTargetRadius = deployingRing.Settings.Radius;
            deployingRing.Settings.Radius = .16f;
            stateBorn = Time.unscaledTime;
            state = FlowState.DeployingRing;
            Time.timeScale = .22f;
            lab.CoreSystem.ForcePulse();
            ShowBanner("ТЕЛЕКИНЕТИЧЕСКИЙ УРОВЕНЬ УВЕЛИЧЕН", 2.2f);
        }

        private void TickRingDeployment()
        {
            if (deployingRing == null) { FinishRingDeployment(); return; }
            float t = Mathf.Clamp01((Time.unscaledTime - stateBorn) / 1.05f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            deployingRing.Settings.Radius = Mathf.Lerp(.16f, deployingTargetRadius, eased);
            deployingRing.FlashUpgrade(.08f);
            if (t < 1f) return;
            lab.CreateGoldenMountedAt(deployingRing.Index, 0, OrbitalMountType.Gun);
            if (lab.RingCount >= 6) ActivateCascade(false);
            lab.Crowd.SetCount(Mathf.RoundToInt(Mathf.Lerp(40f, 240f, (lab.RingCount - 1f) / 11f)),
                lab.PlayerPosition, lab.OuterRingRadius);
            FinishRingDeployment();
        }

        private void FinishRingDeployment()
        {
            deployingRing = null;
            state = FlowState.Running;
            RestoreTimeScale();
        }

        private void PreviousStage()
        {
            if (state != FlowState.Running || lab.RingCount <= 1) return;
            int target = lab.RingCount - 1;
            lab.SetRingCount(target);
            elapsed = target <= 1 ? 0f : RingMilestones[target - 2] - .1f;
            nextMilestoneIndex = target - 1;
            rewardIndex = Mathf.Min(rewardIndex, Mathf.Max(0, Mathf.FloorToInt((elapsed - FirstRewardAt) / RewardStep) + 1));
            lab.Crowd.SetCount(Mathf.RoundToInt(Mathf.Lerp(40f, 240f, (target - 1f) / 11f)),
                lab.PlayerPosition, lab.OuterRingRadius);
            ShowBanner("ПРЕДЫДУЩИЙ ЭТАП · " + target + " КОЛЕЦ", 2f);
        }

        private void ApplyGoldenCoreUpgrade(OrbitalCoreUpgradeType type)
        {
            ActivateCascade(type == OrbitalCoreUpgradeType.CorePower);
            lab.ApplyCoreUpgrade(type);
            if (type == OrbitalCoreUpgradeType.CorePower) lab.Core.PulseGameplayEffect = true;
        }

        private void ActivateCascade(bool charged)
        {
            lab.Core.PulseMode = OrbitalCorePulseMode.Cascade;
            if (charged) lab.Core.PulseGameplayEffect = true;
        }

        private void FinishChoice(string message)
        {
            ResetRingPreviews();
            lab.Pattern.ClearGoldenLinkPreview();
            lab.ShowMounts = false;
            placementRing = -1;
            InvalidHoveredRing = false;
            state = FlowState.Running;
            RestoreTimeScale();
            ShowBanner(message, 2.2f);
        }

        private void CancelPendingWeapon()
        {
            if (pendingWeapon != null) lab.CancelGoldenPendingMounted(pendingWeapon);
            pendingWeapon = null;
            lab.Pattern.ClearGoldenLinkPreview();
        }

        private void PrepareReset()
        {
            CancelPendingWeapon();
            ResetRingPreviews();
            placementRing = -1;
            lastAttachResult = "—";
            lastFailureReason = "—";
            paused = false;
            Time.timeScale = 1f;
        }

        private void ConfigureGoldenPresentation()
        {
            lab.DebugUI.SuppressedByGoldenPath = true;
            lab.DebugUI.SetMenuOpen(false);
            lab.RingGeneration.SpacingMode = OrbitalRingSpacingMode.Compressed;
            lab.RingGeneration.SpeedMode = OrbitalRingSpeedMode.GoldenRatio;
            lab.RegenerateRingLayout();
            lab.PatternCombat = true;
            lab.ShowStats = false;
            lab.ShowAttackRanges = false;
            lab.ShowMounts = false;
            lab.ShowRings = true;
            lab.RingEditMode = false;
            lab.RingContactDamage = false;
            lab.RingContactPush = false;
            lab.Trails.Mode = OrbitalTrailMode.Off;
            lab.Trails.FollowVisualProfile = false;
            lab.WeaponVisuals.ShowPrototypeColliders = false;
            lab.WeaponVisuals.ShowMuzzlePoints = false;
            lab.WeaponVisuals.ShowVisualForward = false;
            lab.WeaponVisuals.ShowMountRoots = false;
            lab.ApplyVisualProfile(OrbitalVisualProfile.Combat);
            lab.Trails.Mode = OrbitalTrailMode.Off;
            lab.Links.Mode = OrbitalLinkMode.Chain;
            lab.Links.ShowLinks = true;
            lab.Links.DealDamage = true;
            lab.Links.LineWidth = .052f;
            lab.Links.MaxDistance = 10f;
            lab.Links.LineColor = new Color(1f, .06f, .84f, 1f);
            lab.Resonance.Enabled = true;
            lab.Resonance.VisualOnly = false;
            lab.Resonance.Mode = OrbitalResonanceMode.Beam;
            lab.Resonance.AlignmentTolerance = 7f;
            lab.Resonance.Cooldown = 2.2f;
            lab.Resonance.Range = 9f;
            lab.Core.PulseMode = OrbitalCorePulseMode.Visual;
            lab.Core.PulseGameplayEffect = false;
            lab.Core.PulseInterval = 6.5f;
            lab.Core.PulseBrightness = .72f;
            lab.CameraRig.Mode = OrbitalCameraMode.CombatFocus;
            lab.CameraRig.MaximumAutoCameraSize = 13f;
            PrepareAllRings();
            lab.CameraRig.Snap(lab.PlayerPosition, lab.OuterRingRadius);
        }

        private void PrepareAllRings()
        {
            for (int i = 0; i < lab.RingCount; i++)
            {
                lab.Rings[i].Settings.Shape = OrbitalShape.Circle;
                lab.Rings[i].Settings.FieldMode = OrbitalRingFieldMode.Ghost;
            }
        }

        private void ResetGoldenMountCapacityToBase()
        {
            for (int i = 0; i < lab.RingCount; i++)
                lab.Rings[i].Settings.MaxMounts = Mathf.Clamp(3 + lab.Rings[i].Upgrades.MountCapacityBonus,
                    1, OrbitalRing.AbsoluteMaxMounts);
        }

        private void AddBaselineGuns()
        {
            for (int i = 0; i < lab.RingCount; i++) lab.CreateGoldenMountedAt(i, 0, OrbitalMountType.Gun);
        }

        private void OpenAdvancedLab()
        {
            CancelPendingWeapon();
            ResetRingPreviews();
            state = FlowState.Running;
            AdvancedLab = true;
            lab.DebugUI.SuppressedByGoldenPath = false;
            lab.DebugUI.SetMenuOpen(true);
            lab.ShowStats = true;
            lab.ShowMounts = true;
            Time.timeScale = 1f;
        }

        private void ReturnToGoldenPath()
        {
            AdvancedLab = false;
            lab.DebugUI.SuppressedByGoldenPath = true;
            lab.DebugUI.SetMenuOpen(false);
            menuOpen = false;
            paused = false;
            ConfigureGoldenPresentation();
            RestoreTimeScale();
        }

        private void TogglePause()
        {
            if (state != FlowState.Running) return;
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        private void SlowForChoice()
        {
            paused = false;
            Time.timeScale = .08f;
        }

        private void RestoreTimeScale() => Time.timeScale = paused ? 0f : 1f;

        private void FindWeaponCandidate(Vector2 world)
        {
            if (PointerOverUi) return;
            float best = Mathf.Max(.78f, lab.OuterRingRadius * .026f);
            for (int i = 0; i < lab.RingCount; i++)
            {
                OrbitalRing ring = lab.Rings[i];
                float distance = ring.DistanceToPath(lab.PlayerPosition, world);
                if (distance >= best) continue;
                int slot = ring.FindFreeSlot(lab.PlayerPosition, world);
                if (slot < 0) continue;
                best = distance;
                HoveredRing = i;
                CandidateSlot = slot;
                lab.SelectedRing = i;
            }
        }

        private int FindNearestRing(Vector2 world, float threshold)
        {
            int result = -1;
            float best = threshold;
            for (int i = 0; i < lab.RingCount; i++)
            {
                float distance = lab.Rings[i].DistanceToPath(lab.PlayerPosition, world);
                if (distance >= best) continue;
                best = distance;
                result = i;
            }
            return result;
        }

        private void ResetRingPreviews()
        {
            if (lab == null) return;
            for (int i = 0; i < lab.RingCount; i++) lab.Rings[i].PreviewRotationMultiplier = 1f;
        }

        private bool CancelPressed() => Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);

        private Vector2 MouseWorld()
        {
            Vector3 screen = Input.mousePosition;
            screen.z = -Camera.main.transform.position.z;
            return Camera.main.ScreenToWorldPoint(screen);
        }

        private int InstalledObjects() => lab.MountedCount - (pendingWeapon != null ? 1 : 0);

        private string CurrentFlowLabel()
        {
            if (paused) return "ПАУЗА";
            if (state == FlowState.ChoosingCard) return "ТЕКУЩАЯ НАГРАДА: ВЫБЕРИТЕ КАРТОЧКУ";
            if (state == FlowState.PlacingWeapon || state == FlowState.ChoosingMount || state == FlowState.FlyingWeapon)
                return "ТЕКУЩАЯ НАГРАДА: " + selectedReward.Title;
            if (state == FlowState.ChoosingRing) return "ТЕКУЩАЯ НАГРАДА: " + selectedReward.Title;
            if (state == FlowState.DeployingRing) return "НОВОЕ КОЛЬЦО РАЗВОРАЧИВАЕТСЯ";
            if (nextMilestoneIndex >= RingMilestones.Length) return "СТАНЦИЯ ЗАВЕРШЕНА · FINALE";
            float ringIn = Mathf.Max(0f, RingMilestones[nextMilestoneIndex] - elapsed);
            string rewardIn = rewardIndex < RewardCount
                ? FormatTime(Mathf.Max(0f, FirstRewardAt + rewardIndex * RewardStep - elapsed))
                : "—";
            return $"НАГРАДА {rewardIn} · НОВОЕ КОЛЬЦО {FormatTime(ringIn)}";
        }

        private string QaCapturePath()
        {
            string suffix = menuOpen ? "MENU" : state == FlowState.ChoosingCard ? "REWARD" :
                state == FlowState.PlacingWeapon || state == FlowState.ChoosingMount ||
                state == FlowState.ChoosingRing || state == FlowState.FlyingWeapon
                    ? "PLACEMENT" : lab.RingCount >= 12 ? "FINALE" : lab.RingCount >= 6 ? "MIDPOINT" : "BEGINNING";
            return $"Assets/_Project/Prototype/OrbitalCombatLab/QA_GOLDEN_{suffix}.png";
        }

        private string RingRoles(OrbitalRing ring)
        {
            string result = "пусто";
            int count = 0;
            for (int i = 0; i < ring.Mounts.Length; i++)
            {
                OrbitalMountedObject mounted = ring.Mounts[i];
                if (mounted == null) continue;
                string name = WeaponName(mounted.Type);
                result = count == 0 ? name : result + ", " + name;
                count++;
            }
            return result;
        }

        private void FillFixedCards(int step)
        {
            switch (step)
            {
                case 0: SetCards(Blade(), Overdrive(), Gun()); break;
                case 1: SetCards(Overdrive(), Blade(), ExtraMount()); break;
                case 2: SetCards(Link(), Pusher(), Amplifier()); break;
                case 3: SetCards(Link(), Gun(), Overdrive()); break;
                case 4: SetCards(ChargedCascade(), Amplifier(), Arc()); break;
                case 5: SetCards(Arc(), Link(), Blade()); break;
                case 6: SetCards(ExtraMount(), Overdrive(), Amplifier()); break;
                case 7: SetCards(Pusher(), Gun(), Blade()); break;
                case 8: SetCards(Amplifier(), Overdrive(), LinkPulse()); break;
                case 9: SetCards(Link(), Arc(), ExtraMount()); break;
                case 10: SetCards(Arc(), Link(), Amplifier()); break;
                case 11: SetCards(FasterCascade(), ChargedCascade(), LinkPulse()); break;
                case 12: SetCards(Gun(), Blade(), Pusher()); break;
                case 13: SetCards(Overdrive(), Amplifier(), ExtraMount()); break;
                case 14: SetCards(Blade(), Arc(), Link()); break;
                case 15: SetCards(Amplifier(), Overdrive(), ExtraMount()); break;
                case 16: SetCards(LinkPulse(), FasterCascade(), ChargedCascade()); break;
                default: SetCards(ExtraMount(), Amplifier(), Overdrive()); break;
            }
        }

        private void FillRandomCards()
        {
            Reward[] pool = { Gun(), Blade(), Pusher(), Link(), Arc(), Overdrive(), Amplifier(), ExtraMount(),
                FasterCascade(), ChargedCascade(), LinkPulse() };
            int first = NextRandom(pool.Length);
            int second = NextRandom(pool.Length);
            while (second == first) second = NextRandom(pool.Length);
            int third = NextRandom(pool.Length);
            while (third == first || third == second) third = NextRandom(pool.Length);
            SetCards(pool[first], pool[second], pool[third]);
        }

        private int NextRandom(int count)
        {
            randomState = randomState * 1664525u + 1013904223u;
            return (int)(randomState % (uint)count);
        }

        private void SetCards(Reward a, Reward b, Reward c) { cards[0] = a; cards[1] = b; cards[2] = c; }

        private static Reward Gun() => Reward.WeaponReward(OrbitalMountType.Gun, "НОВЫЙ PISTOL", "Дальняя автоматическая защита.\nПосле выбора укажите кольцо.");
        private static Reward Blade() => Reward.WeaponReward(OrbitalMountType.Blade, "НОВЫЙ LASERSWARD", "Контактный клинок для плотного центра.\nПосле выбора укажите кольцо.");
        private static Reward Pusher() => Reward.WeaponReward(OrbitalMountType.Pusher, "НОВЫЙ IMPULSGUN", "Раздвигает толпу вокруг орбиты.\nПосле выбора укажите кольцо.");
        private static Reward Link() => Reward.WeaponReward(OrbitalMountType.LinkNode, "НОВЫЙ LINK NODE", "Перестраивает фиолетовую сеть.\nPreview покажет будущую связь.");
        private static Reward Arc() => Reward.WeaponReward(OrbitalMountType.ArcEmitter, "НОВЫЙ ARC EMITTER", "Короткий разряд по цепочке врагов.\nПосле выбора укажите кольцо.");
        private static Reward Overdrive() => Reward.RingReward(OrbitalRingUpgradeType.Overdrive, "ПЕРЕГРУЗКА КОЛЬЦА", "Выбранное кольцо вращается на 25% быстрее.\nПосле выбора укажите кольцо.");
        private static Reward Amplifier() => Reward.RingReward(OrbitalRingUpgradeType.Amplifier, "УСИЛИТЕЛЬ КОЛЬЦА", "Мощность всех объектов кольца выше на 25%.\nПосле выбора укажите кольцо.");
        private static Reward ExtraMount() => Reward.RingReward(OrbitalRingUpgradeType.ExtraMount, "НОВОЕ КРЕПЛЕНИЕ", "На выбранном кольце появляется ещё одна точка.\nПосле выбора укажите кольцо.");
        private static Reward FasterCascade() => Reward.CoreReward(OrbitalCoreUpgradeType.PulseFrequency, "БЫСТРЫЙ КАСКАД", "Импульс ядра проходит через станцию чаще.");
        private static Reward ChargedCascade() => Reward.CoreReward(OrbitalCoreUpgradeType.CorePower, "ЗАРЯЖЕННЫЙ КАСКАД", "Проходящий импульс усиливает оружие эшелона.");
        private static Reward LinkPulse() => Reward.CoreReward(OrbitalCoreUpgradeType.LinkMatrix, "СВЯЗУЮЩИЙ ИМПУЛЬС", "При каскаде фиолетовые связи вспыхивают сильнее.");

        private static string WeaponName(OrbitalMountType type) => type switch
        {
            OrbitalMountType.Gun => "Pistol",
            OrbitalMountType.Blade => "LaserSward",
            OrbitalMountType.Pusher => "ImpulsGun",
            OrbitalMountType.LinkNode => "Link Node",
            OrbitalMountType.ArcEmitter => "Arc Emitter",
            _ => "Mine Layer"
        };

        private static string FormatTime(float seconds)
        {
            int value = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{value / 60:00}:{value % 60:00}";
        }

        private static string Roman(int value) => value <= 0 ? "0" : value == 1 ? "I" : value == 2 ? "II" : value == 3 ? "III" : "IV";

        private void ShowBanner(string text, float duration)
        {
            banner = text;
            bannerUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (hudStyle != null) return;
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            hudStyle.normal.textColor = new Color(.82f, .94f, .98f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(.38f, 1f, 1f);
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.UpperCenter };
            cardTitleStyle.normal.textColor = new Color(.65f, 1f, 1f);
            cardBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperCenter };
            cardBodyStyle.normal.textColor = new Color(.84f, .89f, .94f);
            centerStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            centerStyle.normal.textColor = new Color(.86f, 1f, 1f);
        }
    }
}
