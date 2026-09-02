using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Subject42.Prototype.OrbitalCombatLab
{
    [DisallowMultipleComponent]
    public sealed class OrbitalCombatLabController : MonoBehaviour
    {
        // Absolute prototype storage ceilings. Play-mode creation is governed by the
        // user-facing safety limits below, not by the old six-ring design cap.
        public const int MaxRings = 64;
        public const int MaxMountedObjects = 768;

        public readonly GunSettings Gun = new();
        public readonly BladeSettings Blade = new();
        public readonly PusherSettings Pusher = new();
        public readonly LinkSettings Links = new();
        public readonly ResonanceSettings Resonance = new();
        public readonly TrailSettings Trails = new();
        public readonly WeaponVisualSettings WeaponVisuals = new();
        public readonly OrbitalRingGenerationSettings RingGeneration = new();
        public readonly OrbitalCoreSettings Core = new();
        public readonly MineSettings Mines = new();
        public readonly ArcSettings Arc = new();
        public readonly OrbitalLabStats Stats = new();
        public readonly OrbitalRing[] Rings = new OrbitalRing[MaxRings];
        public readonly OrbitalMountedObject[] MountedObjects = new OrbitalMountedObject[MaxMountedObjects];

        [Header("Integration Sandbox")]
        [Tooltip("Uses the production player, enemies and camera without creating Lab replacements.")]
        public bool IntegrationMode;
        public Transform IntegrationPlayer;
        public Camera IntegrationCamera;
        [Tooltip("When enabled, the Lab may temporarily frame its station with the production camera.")]
        public bool IntegrationCameraOverride;

        public Transform WorldRoot { get; private set; }
        public Vector2 PlayerPosition => player != null ? player.position : Vector2.zero;
        public bool HasIntegrationPlayer => !IntegrationMode ||
            (IntegrationPlayer != null && player == IntegrationPlayer);
        public OrbitalEnemyCrowd Crowd { get; private set; }
        public OrbitalProjectilePool Projectiles { get; private set; }
        public OrbitalLabDragController Drag { get; private set; }
        public OrbitalLabCameraRig CameraRig { get; private set; }
        public OrbitalLabDebugUI DebugUI { get; private set; }
        public OrbitalPatternCombatSystem Pattern { get; private set; }
        public OrbitalMineSystem MineSystem { get; private set; }
        public OrbitalArcSystem ArcSystem { get; private set; }
        public OrbitalCoreSystem CoreSystem { get; private set; }
        public OrbitalGoldenPath GoldenPath { get; private set; }
        public Light2D GlobalLight { get; private set; }
        public OrbitalActorVisual PlayerVisual { get; private set; }
        public int RingCount { get; private set; }
        public int MountedCount { get; private set; }
        public int SelectedRing { get; set; }
        public bool ShowRings = true;
        public bool ShowMounts = true;
        public bool PlayerImmortal = true;
        public bool RingContactDamage;
        public bool RingContactPush = true;
        public bool SlowDuringDrag = true;
        public bool ShowAttackRanges;
        public bool ShowStats = true;
        public bool CameraImpulse = true;
        public bool RingUpgradeVisuals = true;
        public int SafetyRingLimit = 32;
        public int SafetyObjectLimit = 256;
        public int LabLevel = 1;
        public string UserMessage { get; private set; } = "";
        public float UserMessageUntil { get; private set; }
        public bool PatternCombat;
        public bool RingEditMode;
        public bool PauseSelectedRingWhileEditing = true;
        public bool FreeMountPhase;
        public float MinimumMountSpacing = 12f;
        public float RingAlpha = 1f;
        public float TrailAlpha = 1f;
        public float LinkAlpha = 1f;
        public float ResonanceFlash = 1f;
        public float EnemyAlpha = 1f;
        public float ProjectileAlpha = 1f;
        public OrbitalMovementPreset CurrentMovementPreset = OrbitalMovementPreset.Default;
        public OrbitalVisualProfile CurrentVisualProfile = OrbitalVisualProfile.Combat;
        public OrbitalMountedObject SelectedMounted;
        public Vector2 LastMoveDirection { get; private set; } = Vector2.right;
        public float PlayerHp = 100f;

        private sealed class Pulse
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float Born;
            public float Duration;
            public float FinalSize;
            public Color Color;
            public bool Active;
        }

        private readonly Pulse[] pulses = new Pulse[72];
        private int pulseCursor;
        private OrbitalPrimitiveFactory factory;
        private Transform player;
        private float fpsAccumulator;
        private int fpsFrames;
        private readonly float[] frozenSpeeds = new float[MaxRings];
        private readonly bool[] frozenPaused = new bool[MaxRings];
        private bool movementFrozen;
        private Vector2 previousIntegratedPlayerPosition;

        private void Awake()
        {
            BuildWorld();
            if (IntegrationMode)
            {
                ApplyIntegrationStart();
                GoldenPath.enabled = false;
                DebugUI.enabled = false;
            }
            else
            {
                ApplyStartState();
                GoldenPath.BeginFullRun();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickFps();
            TickPlayer(dt);

            for (int i = 0; i < RingCount; i++)
            {
                if (Rings[i] == null) continue;
                bool goldenCandidate = GoldenPath != null && GoldenPath.SelectionActive &&
                    GoldenPath.HoveredRing == i;
                bool highlighted = (Drag != null && Drag.CandidateRing == i) || goldenCandidate;
                int previewSlot = Drag != null && Drag.CandidateRing == i ? Drag.CandidateSlot :
                    goldenCandidate ? GoldenPath.CandidateSlot : -1;
                bool selectionMode = RingEditMode || (DebugUI != null && DebugUI.UpgradeSelectionActive) ||
                    (GoldenPath != null && GoldenPath.SelectionActive);
                bool selectedForEdit = selectionMode && SelectedRing == i;
                bool hoveredForEdit = selectionMode && ((DebugUI != null && DebugUI.HoveredRing == i) ||
                    (GoldenPath != null && GoldenPath.HoveredRing == i));
                bool readableMounts = CameraRig == null || CameraRig.ApproximateObjectScreenSize > .012f;
                Rings[i].Tick(PlayerPosition, dt, ShowRings, (ShowMounts && readableMounts) || selectionMode,
                    highlighted, selectedForEdit, hoveredForEdit,
                    selectedForEdit && PauseSelectedRingWhileEditing, previewSlot, RingAlpha,
                    RingUpgradeVisuals, GoldenPath != null && GoldenPath.SelectionActive,
                    GoldenPath != null && GoldenPath.InvalidHoveredRing && GoldenPath.HoveredRing == i);
            }
            for (int i = 0; i < MountedCount; i++)
            {
                OrbitalMountedObject mounted = MountedObjects[i];
                if (mounted == null) continue;
                mounted.SetRangesVisible(ShowAttackRanges);
                mounted.Tick(dt);
            }

            Pattern.Tick(dt);
            MineSystem.Tick();
            ArcSystem.Tick();
            CoreSystem.Tick();
            Crowd.VisualAlpha = EnemyAlpha;
            Projectiles.VisualAlpha = ProjectileAlpha;
            Crowd.Tick(PlayerPosition, OuterRingRadius, dt, PlayerImmortal, ref PlayerHp);
            Crowd.ApplyRingContact(PlayerPosition, Rings, RingCount,
                RingContactDamage, RingContactPush, dt);
            Projectiles.Tick(dt);
            TickPulses();
            // Golden Path owns its two-step ring/mount pointer flow. Letting the generic
            // Lab drag controller see the same click makes it attach the preview early.
            if (!IntegrationMode && (GoldenPath == null || !GoldenPath.SelectionActive)) Drag.Tick();
            if (!IntegrationMode || IntegrationCameraOverride)
                CameraRig.Tick(PlayerPosition, OuterRingRadius);
        }

        public float OuterRingRadius
        {
            get
            {
                float radius = 0f;
                for (int i = 0; i < RingCount; i++)
                    radius = Mathf.Max(radius, Rings[i].MaximumVisualRadius);
                return radius;
            }
        }

        public bool AddRing()
        {
            int limit = Mathf.Clamp(SafetyRingLimit, 1, MaxRings);
            if (RingCount >= limit)
            {
                Notify($"Safety Ring Limit: {limit}. Увеличьте лимит осознанно (максимум {MaxRings}).");
                return false;
            }
            int index = RingCount;
            OrbitalRing ring = new(index, WorldRoot, factory);
            ring.ApplyDefaults(CalculateGeneratedRadius(index), CalculateGeneratedSpeed(index),
                CalculateGeneratedClockwise(index), index < 2 ? 4 : index < 10 ? 6 : 8);
            ring.RotationAngle = Mathf.Repeat(index * 37f, 360f);
            ring.PhaseOffset = CalculateGeneratedPhase(index);
            ring.Settings.GeneratedLineAlpha = index < 12 ? 1f : Mathf.Max(.36f, 1f - (index - 11) * .028f);
            ring.Settings.LineWidth = Mathf.Max(.025f, .048f - index * .00065f);
            Rings[RingCount++] = ring;
            SelectedRing = RingCount - 1;
            ring.FlashUpgrade(.8f);
            Notify($"Добавлено кольцо {RingCount}: R {ring.Settings.Radius:0.##}, {ring.Settings.RotationSpeed:0.#}°/с");
            return true;
        }

        public bool RemoveRing()
        {
            if (RingCount <= 1) return false;
            Drag.CancelDrag();
            MineSystem?.Clear();
            int index = RingCount - 1;
            OrbitalRing ring = Rings[index];
            for (int slot = 0; slot < OrbitalRing.AbsoluteMaxMounts; slot++)
                if (ring.Mounts[slot] != null) RemoveMounted(ring.Mounts[slot]);
            ring.Destroy();
            Rings[index] = null;
            RingCount--;
            SelectedRing = Mathf.Clamp(SelectedRing, 0, RingCount - 1);
            return true;
        }

        public bool AddMounted(OrbitalMountType type)
        {
            if (RingCount == 0 || MountedCount >= MaxMountedObjects) return false;
            int ringIndex = Mathf.Clamp(SelectedRing, 0, RingCount - 1);
            OrbitalRing ring = Rings[ringIndex];
            int slot = ring.FindFreeSlot(PlayerPosition, PlayerPosition + Vector2.right * ring.Settings.Radius);
            if (slot < 0)
            {
                for (int i = 0; i < RingCount && slot < 0; i++)
                {
                    ring = Rings[i];
                    slot = ring.FindFreeSlot(PlayerPosition, PlayerPosition + Vector2.right * ring.Settings.Radius);
                }
            }
            if (slot < 0) return false;

            return CreateMountedAt(ring, slot, type);
        }

        public bool CreateGoldenMountedAt(int ringIndex, int slot, OrbitalMountType type)
        {
            if (ringIndex < 0 || ringIndex >= RingCount) return false;
            return CreateMountedAt(Rings[ringIndex], slot, type);
        }

        public OrbitalMountedObject CreateGoldenPendingMounted(OrbitalMountType type)
        {
            if (type == OrbitalMountType.MineLayer || MountedCount >= MaxMountedObjects) return null;
            OrbitalMountedObject mounted = CreateMountedInstance(type);
            MountedObjects[MountedCount++] = mounted;
            mounted.IsDragging = true;
            mounted.SetDraggedPosition(PlayerPosition);
            return mounted;
        }

        public bool AttachGoldenPendingMounted(OrbitalMountedObject mounted, int ringIndex, int slot)
            => AttachGoldenPendingMounted(mounted, ringIndex, slot, out _);

        public bool AttachGoldenPendingMounted(OrbitalMountedObject mounted, int ringIndex, int slot,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (mounted == null)
            {
                failureReason = "Preview оружия отсутствует";
                return false;
            }
            if (mounted.IsDestroyed)
            {
                failureReason = "Preview оружия уже уничтожен";
                return false;
            }
            if (ringIndex < 0 || ringIndex >= RingCount)
            {
                failureReason = "Выбранное кольцо больше не существует";
                return false;
            }
            OrbitalRing ring = Rings[ringIndex];
            if (slot < 0 || slot >= ring.Settings.MaxMounts || slot >= ring.Mounts.Length)
            {
                failureReason = "Выбранное крепление вне ёмкости кольца";
                return false;
            }
            if (ring.Mounts[slot] != null)
            {
                failureReason = "Крепление уже занято";
                return false;
            }
            mounted.Attach(ring, slot);
            SelectedRing = ringIndex;
            ring.FlashUpgrade(.7f);
            bool attached = mounted.Ring == ring && mounted.Slot == slot && ring.Mounts[slot] == mounted &&
                !mounted.IsDragging;
            if (!attached) failureReason = "Attach не подтвердился логическим mount slot";
            return attached;
        }

        public void CancelGoldenPendingMounted(OrbitalMountedObject mounted) => RemoveMounted(mounted);

        public void ClearMounted()
        {
            Drag.CancelDrag();
            for (int i = MountedCount - 1; i >= 0; i--)
                MountedObjects[i]?.Destroy();
            for (int i = 0; i < MountedObjects.Length; i++) MountedObjects[i] = null;
            MountedCount = 0;
            Projectiles.Clear();
            MineSystem?.Clear();
            ArcSystem?.Clear();
            Pattern?.Reset();
        }

        public void FillAllRings()
        {
            for (int r = 0; r < RingCount; r++)
            {
                SelectedRing = r;
                int count = Mathf.Clamp(Rings[r].Settings.MaxMounts, 1, OrbitalRing.AbsoluteMaxMounts);
                for (int slot = 0; slot < count; slot++)
                {
                    if (Rings[r].Mounts[slot] != null) continue;
                    CreateMountedAt(Rings[r], slot, (OrbitalMountType)((r + slot) % 3));
                }
            }
            SelectedRing = Mathf.Clamp(RingCount - 1, 0, RingCount - 1);
        }

        public void ApplyStartState()
        {
            ResetLab(1);
            Gun.Damage = 8f; Gun.FireRate = 1.7f; Gun.Range = 6.5f; Gun.ProjectileSpeed = 14f;
            Blade.Damage = 20f; Blade.HitCooldown = .32f; Blade.Size = 1.05f;
            Pusher.PushForce = 11f; Pusher.PushRadius = 1.35f; Pusher.Cooldown = .75f;
            SelectedRing = 0;
            Rings[0].Settings.MaxMounts = 3;
            AddMounted(OrbitalMountType.Gun);
            Crowd.EnemyMaxHp = 38f;
            Crowd.EnemySpeed = 1.65f;
            Crowd.SetCount(50, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyMidState()
        {
            ResetLab(3);
            Gun.Damage = 11f; Gun.FireRate = 2.7f; Gun.Range = 8f; Gun.ProjectileSpeed = 17f;
            Blade.Damage = 23f; Blade.HitCooldown = .27f; Blade.Size = 1.2f;
            Pusher.PushForce = 14f; Pusher.PushRadius = 1.55f; Pusher.Cooldown = .62f;
            AddAt(0, OrbitalMountType.Gun, 2);
            AddAt(1, OrbitalMountType.Blade, 2);
            AddAt(2, OrbitalMountType.Pusher, 2);
            AddAt(2, OrbitalMountType.Gun, 1);
            Crowd.EnemyMaxHp = 42f;
            Crowd.EnemySpeed = 1.8f;
            Crowd.SetCount(120, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyFinalState()
        {
            ResetLab(6);
            Gun.Damage = 15f; Gun.FireRate = 4.2f; Gun.Range = 10f; Gun.ProjectileSpeed = 21f;
            Blade.Damage = 34f; Blade.HitCooldown = .2f; Blade.Size = 1.45f;
            Pusher.PushForce = 20f; Pusher.PushRadius = 1.9f; Pusher.Cooldown = .48f;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = r < 2 ? 5 : 6;
                Rings[r].Settings.RotationSpeed *= 1.12f;
            }
            FillAllRings();
            Crowd.EnemyMaxHp = 50f;
            Crowd.EnemySpeed = 2.05f;
            Crowd.SetCount(300, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ResetTest() => ApplyStartState();

        public void SpawnEnemies(int count) => Crowd.SetCount(count, PlayerPosition, OuterRingRadius);

        public void Notify(string message, float duration = 4f)
        {
            UserMessage = message ?? string.Empty;
            UserMessageUntil = Time.unscaledTime + duration;
        }

        public int AddRings(int count)
        {
            int added = 0;
            for (int i = 0; i < count && AddRing(); i++) added++;
            if (CameraRig != null) CameraRig.Snap(PlayerPosition, OuterRingRadius);
            return added;
        }

        public void SetRingCount(int count)
        {
            count = Mathf.Clamp(count, 1, Mathf.Clamp(SafetyRingLimit, 1, MaxRings));
            while (RingCount < count && AddRing()) { }
            while (RingCount > count) RemoveRing();
            CameraRig?.Snap(PlayerPosition, OuterRingRadius);
        }

        public void KeepOnlyOneRing() => SetRingCount(1);

        public void ClearStation()
        {
            ClearMounted();
            KeepOnlyOneRing();
            ResetAllUpgrades();
            Notify("Станция очищена: оставлено одно пустое кольцо.");
        }

        public void RegenerateRingLayout()
        {
            for (int i = 0; i < RingCount; i++)
            {
                OrbitalRing ring = Rings[i];
                ring.Settings.Radius = CalculateGeneratedRadius(i);
                ring.Settings.RotationSpeed = CalculateGeneratedSpeed(i);
                ring.Settings.Clockwise = CalculateGeneratedClockwise(i);
                ring.PhaseOffset = CalculateGeneratedPhase(i);
            }
            CameraRig?.Snap(PlayerPosition, OuterRingRadius);
        }

        public float CalculateGeneratedRadius(int index)
        {
            if (index <= 0) return Mathf.Max(.6f, RingGeneration.FirstRingRadius);
            float radius = Mathf.Max(.6f, RingGeneration.FirstRingRadius);
            for (int i = 1; i <= index; i++)
            {
                float gap;
                if (RingGeneration.SpacingMode == OrbitalRingSpacingMode.ConstantGap)
                    gap = RingGeneration.BaseRingGap;
                else if (RingGeneration.SpacingMode == OrbitalRingSpacingMode.GrowingGap)
                    gap = RingGeneration.BaseRingGap + RingGeneration.GapGrowth * i;
                else
                {
                    int after = Mathf.Max(0, i - Mathf.Max(1, RingGeneration.CompressionStartRing));
                    gap = RingGeneration.BaseRingGap / (1f + after * Mathf.Max(.01f, RingGeneration.GapGrowth));
                }
                radius += Mathf.Max(RingGeneration.MinimumGap, gap);
            }
            return radius;
        }

        public float CalculateGeneratedSpeed(int index)
        {
            float baseSpeed = Mathf.Max(1f, RingGeneration.BaseSpeed);
            switch (RingGeneration.SpeedMode)
            {
                case OrbitalRingSpeedMode.Constant: return baseSpeed;
                case OrbitalRingSpeedMode.OuterSlower: return Mathf.Max(12f, baseSpeed / (1f + index * .16f));
                case OrbitalRingSpeedMode.GoldenRatio:
                    return Mathf.Max(15f, baseSpeed / (1f + index * .115f)) * (index % 3 == 2 ? .918f : 1f);
                case OrbitalRingSpeedMode.ControlledChaos:
                    return 24f + Hash01(index, RingGeneration.ChaosSeed) * (baseSpeed - 16f);
                default: return Mathf.Max(18f, baseSpeed / (1f + index * .12f));
            }
        }

        public bool CalculateGeneratedClockwise(int index)
        {
            if (RingGeneration.SpeedMode == OrbitalRingSpeedMode.ControlledChaos)
                return Hash01(index + 83, RingGeneration.ChaosSeed) > .5f;
            return index % 2 == 1;
        }

        private float CalculateGeneratedPhase(int index)
        {
            if (RingGeneration.SpeedMode == OrbitalRingSpeedMode.ControlledChaos)
                return Hash01(index + 191, RingGeneration.ChaosSeed) * 360f;
            return Mathf.Repeat(index * 137.508f, 360f);
        }

        private static float Hash01(int index, int seed)
        {
            unchecked
            {
                uint value = (uint)index * 747796405u + (uint)seed * 2891336453u;
                value = (value >> ((int)(value >> 28) + 4)) ^ value;
                value *= 277803737u;
                value = (value >> 22) ^ value;
                return (value & 0x00ffffff) / 16777215f;
            }
        }

        public void ApplyRingUpgrade(int ringIndex, OrbitalRingUpgradeType type)
        {
            if (ringIndex < 0 || ringIndex >= RingCount) return;
            OrbitalRing ring = Rings[ringIndex];
            switch (type)
            {
                case OrbitalRingUpgradeType.Overdrive: ring.Upgrades.RotationSpeedMultiplier *= 1.25f; break;
                case OrbitalRingUpgradeType.Amplifier: ring.Upgrades.DamageMultiplier *= 1.25f; break;
                case OrbitalRingUpgradeType.SystemsAcceleration: ring.Upgrades.CooldownMultiplier *= .85f; break;
                case OrbitalRingUpgradeType.ExtraMount:
                    if (ring.Settings.MaxMounts < OrbitalRing.AbsoluteMaxMounts)
                    {
                        ring.Settings.MaxMounts++;
                        ring.Upgrades.MountCapacityBonus++;
                    }
                    break;
                case OrbitalRingUpgradeType.EffectField: ring.Upgrades.EffectSizeMultiplier *= 1.2f; break;
                case OrbitalRingUpgradeType.ResonantRing:
                    ring.Upgrades.LinkPowerMultiplier *= 1.25f;
                    ring.Upgrades.ResonancePower *= 1.25f;
                    break;
                case OrbitalRingUpgradeType.Stabilizer: ring.Upgrades.PushMultiplier *= 1.3f; break;
            }
            ring.Upgrades.Level++;
            ring.FlashUpgrade();
            for (int i = 0; i < ring.Mounts.Length; i++) ring.Mounts[i]?.FlashResonance(.55f);
            SelectedRing = ringIndex;
            Notify($"Кольцо {ringIndex + 1} усилено: {RingUpgradeName(type)}");
        }

        public string DescribeRingUpgrade(int ringIndex, OrbitalRingUpgradeType type)
        {
            if (ringIndex < 0 || ringIndex >= RingCount) return "—";
            OrbitalRing ring = Rings[ringIndex];
            switch (type)
            {
                case OrbitalRingUpgradeType.Overdrive:
                    return $"{ring.EffectiveRotationSpeed:0.#}°/с → {ring.EffectiveRotationSpeed * 1.25f:0.#}°/с";
                case OrbitalRingUpgradeType.Amplifier:
                    return $"урон ×{ring.Upgrades.DamageMultiplier:0.##} → ×{ring.Upgrades.DamageMultiplier * 1.25f:0.##}";
                case OrbitalRingUpgradeType.SystemsAcceleration:
                    return $"перезарядка ×{ring.Upgrades.CooldownMultiplier:0.##} → ×{ring.Upgrades.CooldownMultiplier * .85f:0.##}";
                case OrbitalRingUpgradeType.ExtraMount:
                    return $"крепления {ring.Settings.MaxMounts} → {Mathf.Min(OrbitalRing.AbsoluteMaxMounts, ring.Settings.MaxMounts + 1)}";
                case OrbitalRingUpgradeType.EffectField:
                    return $"область ×{ring.Upgrades.EffectSizeMultiplier:0.##} → ×{ring.Upgrades.EffectSizeMultiplier * 1.2f:0.##}";
                case OrbitalRingUpgradeType.ResonantRing:
                    return $"Link/Resonance ×{ring.Upgrades.LinkPowerMultiplier:0.##} → ×{ring.Upgrades.LinkPowerMultiplier * 1.25f:0.##}";
                default:
                    return $"push ×{ring.Upgrades.PushMultiplier:0.##} → ×{ring.Upgrades.PushMultiplier * 1.3f:0.##}";
            }
        }

        public static string RingUpgradeName(OrbitalRingUpgradeType type) => type switch
        {
            OrbitalRingUpgradeType.Overdrive => "ПЕРЕГРУЗКА КОЛЬЦА +25% скорости",
            OrbitalRingUpgradeType.Amplifier => "УСИЛИТЕЛЬ КОЛЬЦА +25% урона",
            OrbitalRingUpgradeType.SystemsAcceleration => "УСКОРЕНИЕ СИСТЕМ −15% cooldown",
            OrbitalRingUpgradeType.ExtraMount => "РАСШИРЕНИЕ КРЕПЛЕНИЙ +1",
            OrbitalRingUpgradeType.EffectField => "УСИЛЕНИЕ ПОЛЯ +20% области",
            OrbitalRingUpgradeType.ResonantRing => "РЕЗОНАНСНОЕ КОЛЬЦО +25% Link",
            _ => "СТАБИЛИЗАТОР +30% push"
        };

        public void ApplyCoreUpgrade(OrbitalCoreUpgradeType type)
        {
            switch (type)
            {
                case OrbitalCoreUpgradeType.NewRing: AddRing(); break;
                case OrbitalCoreUpgradeType.CorePower: Core.GlobalDamageMultiplier *= 1.1f; break;
                case OrbitalCoreUpgradeType.PulseFrequency: Core.PulseInterval = Mathf.Max(.75f, Core.PulseInterval * .85f); break;
                case OrbitalCoreUpgradeType.FieldScale: Core.GlobalEffectSizeMultiplier *= 1.1f; break;
                case OrbitalCoreUpgradeType.LinkMatrix:
                    Core.LinkCapacityBonus += 2; Core.LinkRangeMultiplier *= 1.1f; Core.ResonancePowerMultiplier *= 1.1f; break;
                case OrbitalCoreUpgradeType.Stabilization: SafetyRingLimit = Mathf.Min(MaxRings, SafetyRingLimit + 4); break;
            }
            Core.Level++;
            CoreSystem?.ForcePulse();
            Notify($"Ядро усилено: {type}");
        }

        public void ResetAllUpgrades()
        {
            Core.Reset();
            for (int i = 0; i < RingCount; i++) Rings[i].Upgrades.Reset();
            LabLevel = 1;
            Notify("Все тестовые улучшения сброшены.");
        }

        public void MaxSelectedRing()
        {
            if (RingCount == 0) return;
            foreach (OrbitalRingUpgradeType type in System.Enum.GetValues(typeof(OrbitalRingUpgradeType)))
                ApplyRingUpgrade(SelectedRing, type);
        }

        public void MaxCore()
        {
            for (int i = 0; i < 5; i++)
            {
                ApplyCoreUpgrade(OrbitalCoreUpgradeType.CorePower);
                ApplyCoreUpgrade(OrbitalCoreUpgradeType.PulseFrequency);
                ApplyCoreUpgrade(OrbitalCoreUpgradeType.FieldScale);
            }
            ApplyCoreUpgrade(OrbitalCoreUpgradeType.LinkMatrix);
        }

        public void MaxStation()
        {
            MaxCore();
            for (int ring = 0; ring < RingCount; ring++)
                foreach (OrbitalRingUpgradeType type in System.Enum.GetValues(typeof(OrbitalRingUpgradeType)))
                    ApplyRingUpgrade(ring, type);
        }

        public int EstimateFill(float fraction, int fixedPerRing = 0)
        {
            int total = 0;
            for (int i = 0; i < RingCount; i++)
            {
                int capacity = Mathf.Clamp(Rings[i].Settings.MaxMounts, 1, OrbitalRing.AbsoluteMaxMounts);
                int desired = fixedPerRing > 0 ? Mathf.Min(fixedPerRing, capacity) : Mathf.CeilToInt(capacity * fraction);
                for (int slot = 0; slot < desired; slot++) if (Rings[i].Mounts[slot] == null) total++;
            }
            return total;
        }

        public bool FillStation(float fraction, int fixedPerRing = 0, bool confirmed = false)
        {
            int amount = EstimateFill(fraction, fixedPerRing);
            if (MountedCount + amount > SafetyObjectLimit && !confirmed)
            {
                Notify($"Будет создано {amount} объектов (итого {MountedCount + amount}). Нажмите подтверждение массового заполнения.", 7f);
                return false;
            }
            int typeCount = System.Enum.GetValues(typeof(OrbitalMountType)).Length;
            for (int r = 0; r < RingCount; r++)
            {
                OrbitalRing ring = Rings[r];
                int capacity = Mathf.Clamp(ring.Settings.MaxMounts, 1, OrbitalRing.AbsoluteMaxMounts);
                int desired = fixedPerRing > 0 ? Mathf.Min(fixedPerRing, capacity) : Mathf.CeilToInt(capacity * fraction);
                for (int slot = 0; slot < desired; slot++)
                    if (ring.Mounts[slot] == null)
                        CreateMountedAt(ring, slot, (OrbitalMountType)((r * 3 + slot) % typeCount));
            }
            Notify($"Создано {amount} объектов.");
            return true;
        }

        public void FillTheme(OrbitalMountType primary, OrbitalMountType secondary, int perRing)
        {
            int amount = EstimateFill(1f, perRing);
            if (MountedCount + amount > SafetyObjectLimit)
            {
                Notify($"Будет создано {amount} объектов — выше Safety Object Limit {SafetyObjectLimit}.", 7f);
                return;
            }
            for (int r = 0; r < RingCount; r++)
            {
                OrbitalRing ring = Rings[r];
                int count = Mathf.Min(perRing, ring.Settings.MaxMounts);
                for (int slot = 0; slot < count; slot++)
                    if (ring.Mounts[slot] == null)
                        CreateMountedAt(ring, slot, slot % 3 == 0 ? secondary : primary);
            }
        }

        public void FillRandomBalanced()
        {
            ClearMounted();
            int desired = EstimateFill(0f, 2);
            if (desired > SafetyObjectLimit)
            {
                Notify($"Будет создано {desired} объектов — выше Safety Object Limit {SafetyObjectLimit}.", 7f);
                return;
            }
            FillStation(0f, 2, true);
            Notify($"RANDOM BALANCED: создано {desired} объектов, роли равномерно перемешаны по кольцам.");
        }

        public void ApplyHypnoticStation()
        {
            ResetLab(16);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.AllNearby;
            Links.DealDamage = false;
            Links.MaxDistance = 9.5f;
            Core.PulseMode = OrbitalCorePulseMode.Resonance;
            Core.PulseGameplayEffect = false;
            RingGeneration.SpeedMode = OrbitalRingSpeedMode.GoldenRatio;
            RegenerateRingLayout();
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 4;
                if ((r & 1) == 0 || r < 3) AddAt(r, OrbitalMountType.LinkNode, r % 4 == 0 ? 2 : 1);
                if (r % 5 == 2) AddAt(r, OrbitalMountType.ArcEmitter, 1);
            }
            Trails.Mode = OrbitalTrailMode.Off;
            ApplyVisualProfile(OrbitalVisualProfile.Hypnotic);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(80, PlayerPosition, OuterRingRadius);
            CameraRig.Mode = OrbitalCameraMode.FullStation;
            CoreSystem.ForcePulse();
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
            Notify("HYPNOTIC STATION: Golden Ratio, Link-собор, короткие Arc-вспышки, trails OFF.");
        }

        public void ApplyGrowthStage(int stage)
        {
            int[] rings = { 1, 3, 6, 10, 16, 24 };
            int index = Mathf.Clamp(stage, 0, rings.Length - 1);
            ResetLab(rings[index]);
            ApplyMovementPreset(index >= 3 ? OrbitalMovementPreset.Flower : OrbitalMovementPreset.Gear);
            FillStation(0f, index < 2 ? 1 : 2, true);
            Crowd.SetCount(index < 2 ? 50 : index < 4 ? 120 : 200, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
            Notify($"GROWTH TIMELINE: {new[] { "Minute 1", "Minute 3", "Minute 6", "Minute 10", "Minute 15", "Extreme" }[index]}");
        }

        public void ApplyCoreCascade()
        {
            ResetLab(12);
            PatternCombat = true;
            Core.PulseMode = OrbitalCorePulseMode.Cascade;
            Core.PulseGameplayEffect = true;
            Links.Mode = OrbitalLinkMode.Chain;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 5;
                CreateMountedAt(Rings[r], 0, OrbitalMountType.Gun);
                CreateMountedAt(Rings[r], 1, OrbitalMountType.MineLayer);
                CreateMountedAt(Rings[r], 2, OrbitalMountType.ArcEmitter);
                if ((r & 1) == 0) CreateMountedAt(Rings[r], 3, OrbitalMountType.LinkNode);
            }
            ApplyMovementPreset(OrbitalMovementPreset.Flower);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(200, PlayerPosition, OuterRingRadius);
            CoreSystem.ForcePulse();
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyIntegrationStart()
        {
            ResetLab(1);
            Rings[0].Settings.MaxMounts = 3;
            CreateGoldenMountedAt(0, 0, OrbitalMountType.Gun);
            Core.Level = 1;
            Core.PulseBrightness = .85f;
            CameraSnapIfAllowed();
        }

        public void ApplyIntegrationMid()
        {
            ResetLab(6);
            for (int ring = 0; ring < RingCount; ring++)
            {
                Rings[ring].Settings.MaxMounts = 4;
                CreateGoldenMountedAt(ring, 0, (OrbitalMountType)(ring % 4));
                CreateGoldenMountedAt(ring, 1, ring == 2 || ring == 4
                    ? OrbitalMountType.LinkNode
                    : ring == 5 ? OrbitalMountType.ArcEmitter : (OrbitalMountType)((ring + 1) % 3));
            }
            Links.Mode = OrbitalLinkMode.Chain;
            Core.Level = 2;
            Core.PulseBrightness = 1.05f;
            CameraSnapIfAllowed();
        }

        public void ApplyIntegrationFinal()
        {
            ResetLab(12);
            for (int ring = 0; ring < RingCount; ring++)
            {
                Rings[ring].Settings.MaxMounts = 4;
                CreateGoldenMountedAt(ring, 0, (OrbitalMountType)(ring % 4));
                CreateGoldenMountedAt(ring, 1, ring % 3 == 0
                    ? OrbitalMountType.LinkNode
                    : ring % 5 == 0 ? OrbitalMountType.ArcEmitter : (OrbitalMountType)((ring + 2) % 3));
            }
            Links.Mode = OrbitalLinkMode.Chain;
            Links.MaxDistance = 12f;
            Arc.ChainCount = 4;
            Core.Level = 3;
            Core.PulseBrightness = 1.2f;
            Trails.Mode = OrbitalTrailMode.Off;
            MineSystem?.Clear();
            CameraSnapIfAllowed();
        }

        public void SetIntegrationPresentationActive(bool active)
        {
            if (WorldRoot != null) WorldRoot.gameObject.SetActive(active);
        }

        public void BindIntegrationPlayer(Transform target)
        {
            if (!IntegrationMode || target == null) return;
            if (player == target && IntegrationPlayer == target) return;
            IntegrationPlayer = target;
            player = target;
            previousIntegratedPlayerPosition = target.position;
            Debug.Log($"[OrbitalIntegration] Bound production player: {target.name}");
        }

        public void ApplyLinkCathedral()
        {
            ResetLab(16);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.AllNearby;
            Links.DealDamage = false;
            Links.MaxDistance = 10f;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 4;
                if (r % 2 == 0 || r < 3) AddAt(r, OrbitalMountType.LinkNode, r % 3 == 0 ? 2 : 1);
            }
            RingGeneration.SpeedMode = OrbitalRingSpeedMode.GoldenRatio;
            RegenerateRingLayout();
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(50, PlayerPosition, OuterRingRadius);
            CameraRig.Mode = OrbitalCameraMode.FullStation;
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyMinePerimeter()
        {
            ResetLab(8);
            PatternCombat = true;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 5;
                AddAt(r, r < 3 ? OrbitalMountType.Pusher : OrbitalMountType.MineLayer, r < 3 ? 2 : 3);
            }
            Crowd.SetCount(200, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyArcReactor()
        {
            ResetLab(10);
            PatternCombat = true;
            Core.PulseMode = OrbitalCorePulseMode.Resonance;
            Links.Mode = OrbitalLinkMode.Chain;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 5;
                AddAt(r, OrbitalMountType.ArcEmitter, 2);
                if ((r & 1) == 0) AddAt(r, OrbitalMountType.LinkNode, 1);
            }
            Crowd.SetCount(200, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyAbsurdStation()
        {
            ResetLab(24);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 5;
                CreateMountedAt(Rings[r], 0, (OrbitalMountType)(r % 6));
                if (r % 3 == 0) CreateMountedAt(Rings[r], 1, OrbitalMountType.LinkNode);
            }
            RingAlpha = .72f;
            Trails.Mode = OrbitalTrailMode.Off;
            CameraRig.Mode = OrbitalCameraMode.FullStation;
            Crowd.SetCount(300, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplySoloLink()
        {
            ResetLab(3);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain;
            Links.DealDamage = true;
            for (int r = 0; r < RingCount; r++) AddAt(r, OrbitalMountType.LinkNode, 2);
            Crowd.SetCount(24, PlayerPosition, OuterRingRadius);
            Trails.Mode = OrbitalTrailMode.Off;
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplySoloResonance()
        {
            ResetLab(4);
            PatternCombat = true;
            Resonance.Enabled = true;
            Resonance.VisualOnly = false;
            for (int r = 0; r < RingCount; r++)
            {
                AddAt(r, r % 2 == 0 ? OrbitalMountType.Gun : OrbitalMountType.LinkNode, 1);
                Rings[r].Settings.RotationSpeed = 38f;
                Rings[r].Settings.Clockwise = false;
                Rings[r].PhaseOffset = 0f;
            }
            Crowd.SetCount(36, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplySoloCorePulse()
        {
            ResetLab(8);
            Core.PulseMode = OrbitalCorePulseMode.Cascade;
            for (int r = 0; r < RingCount; r++) AddAt(r, OrbitalMountType.Gun, 1);
            Crowd.SetCount(60, PlayerPosition, OuterRingRadius);
            CoreSystem.ForcePulse();
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplySoloMine()
        {
            ResetLab(4);
            for (int r = 0; r < RingCount; r++) AddAt(r, OrbitalMountType.MineLayer, 1);
            ShowAttackRanges = true;
            Crowd.SetCount(60, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplySoloArc()
        {
            ResetLab(4);
            for (int r = 0; r < RingCount; r++) AddAt(r, OrbitalMountType.ArcEmitter, 1);
            AddAt(2, OrbitalMountType.LinkNode, 1);
            ShowAttackRanges = true;
            Crowd.SetCount(60, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyMovementPreset(OrbitalMovementPreset preset)
        {
            if (preset == OrbitalMovementPreset.Freeze)
            {
                ToggleFreeze();
                return;
            }
            movementFrozen = false;
            CurrentMovementPreset = preset;
            for (int i = 0; i < RingCount; i++) Rings[i].Settings.Paused = false;
            switch (preset)
            {
                case OrbitalMovementPreset.Gear:
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.RotationSpeed = Mathf.Max(22f, 112f - i * 16f);
                        Rings[i].Settings.Clockwise = i % 2 == 1;
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = i * 12f;
                    }
                    break;
                case OrbitalMovementPreset.Flower:
                {
                    float[] speeds = { 96f, 64f, 48f, 38.4f, 32f, 27.43f };
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.RotationSpeed = speeds[i % speeds.Length] *
                            Mathf.Pow(.985f, i / speeds.Length);
                        Rings[i].Settings.Clockwise = i % 2 == 1;
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = i * 36f;
                    }
                    break;
                }
                case OrbitalMovementPreset.Wave:
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.RotationSpeed = Mathf.Max(25f, 112f - i * 15f);
                        Rings[i].Settings.Clockwise = false;
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = i * 28f;
                    }
                    break;
                case OrbitalMovementPreset.Sync:
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.RotationSpeed = 52f;
                        Rings[i].Settings.Clockwise = false;
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = i * 31f;
                    }
                    break;
                case OrbitalMovementPreset.Chaos:
                {
                    float[] speeds = { 113f, 71f, 47f, 83f, 29f, 61f };
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.RotationSpeed = speeds[i % speeds.Length] *
                            Mathf.Pow(.99f, i / speeds.Length);
                        Rings[i].Settings.Clockwise = ((i * 17 + 3) % 7) < 3;
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = (i * 67f + 19f) % 360f;
                    }
                    break;
                }
                default:
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.Radius = CalculateGeneratedRadius(i);
                        Rings[i].Settings.RotationSpeed = CalculateGeneratedSpeed(i);
                        Rings[i].Settings.Clockwise = CalculateGeneratedClockwise(i);
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = 0f;
                    }
                    CurrentMovementPreset = OrbitalMovementPreset.Default;
                    break;
            }
        }

        public void ToggleFreeze()
        {
            if (!movementFrozen)
            {
                for (int i = 0; i < RingCount; i++)
                {
                    frozenSpeeds[i] = Rings[i].Settings.RotationSpeed;
                    frozenPaused[i] = Rings[i].Settings.Paused;
                    Rings[i].Settings.RotationSpeed = 0f;
                    Rings[i].Settings.Paused = false;
                }
                movementFrozen = true;
                CurrentMovementPreset = OrbitalMovementPreset.Freeze;
            }
            else
            {
                for (int i = 0; i < RingCount; i++)
                {
                    Rings[i].Settings.RotationSpeed = frozenSpeeds[i];
                    Rings[i].Settings.Paused = frozenPaused[i];
                }
                movementFrozen = false;
                CurrentMovementPreset = OrbitalMovementPreset.Default;
            }
        }

        public void SynchronizeSelectedWithPrevious(bool copyPhase)
        {
            if (SelectedRing <= 0 || SelectedRing >= RingCount) return;
            OrbitalRing current = Rings[SelectedRing];
            OrbitalRing previous = Rings[SelectedRing - 1];
            current.Settings.RotationSpeed = previous.Settings.RotationSpeed;
            current.Settings.Clockwise = previous.Settings.Clockwise;
            if (copyPhase)
            {
                current.RotationAngle = previous.RotationAngle;
                current.PhaseOffset = previous.PhaseOffset;
            }
        }

        public void CopyPreviousSpeed()
        {
            if (SelectedRing <= 0 || SelectedRing >= RingCount) return;
            Rings[SelectedRing].Settings.RotationSpeed = Rings[SelectedRing - 1].Settings.RotationSpeed;
        }

        public void MultiplySelectedSpeed(float multiplier)
        {
            if (RingCount == 0) return;
            Rings[Mathf.Clamp(SelectedRing, 0, RingCount - 1)].Settings.RotationSpeed *= multiplier;
        }

        public void NudgeSelectedPhase(float degrees)
        {
            if (RingCount == 0) return;
            OrbitalRing ring = Rings[Mathf.Clamp(SelectedRing, 0, RingCount - 1)];
            ring.PhaseOffset = Mathf.Repeat(ring.PhaseOffset + degrees, 360f);
        }

        public void AlignMountZeroWithForward()
        {
            if (RingCount == 0) return;
            OrbitalRing ring = Rings[Mathf.Clamp(SelectedRing, 0, RingCount - 1)];
            float forward = Mathf.Atan2(LastMoveDirection.y, LastMoveDirection.x) * Mathf.Rad2Deg;
            ring.PhaseOffset = Mathf.Repeat(forward - ring.RotationAngle, 360f);
        }

        public void DistributeSelectedEvenly() => ApplyFormationToSelected(0);
        public void ClusterSelected() => ApplyFormationToSelected(1);
        public void FrontArcSelected() => ApplyFormationToSelected(2);
        public void AlternateSelected() => ApplyFormationToSelected(3);

        public bool CanUsePhase(OrbitalRing ring, OrbitalMountedObject moving, float phase)
        {
            float proposed = BaseSlotAngle(ring, moving.Slot) + phase;
            for (int i = 0; i < ring.Mounts.Length; i++)
            {
                OrbitalMountedObject other = ring.Mounts[i];
                if (other == null || other == moving) continue;
                float angle = BaseSlotAngle(ring, other.Slot) + other.PhaseOffset;
                if (Mathf.Abs(Mathf.DeltaAngle(proposed, angle)) < MinimumMountSpacing) return false;
            }
            return true;
        }

        public float PhaseFromWorld(OrbitalRing ring, int slot, Vector2 world)
        {
            float desired = Mathf.Atan2(world.y - PlayerPosition.y, world.x - PlayerPosition.x) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(BaseSlotAngle(ring, slot), desired);
        }

        public int GetRingIndex(OrbitalRing ring)
        {
            for (int i = 0; i < RingCount; i++) if (Rings[i] == ring) return i;
            return -1;
        }

        public void ResetSelectedPhase()
        {
            if (RingCount == 0) return;
            Rings[Mathf.Clamp(SelectedRing, 0, RingCount - 1)].PhaseOffset = 0f;
        }

        public void ApplyWeaponVisualMode(OrbitalWeaponVisualMode mode)
        {
            WeaponVisuals.Mode = mode;
            for (int i = 0; i < MountedCount; i++) MountedObjects[i]?.RefreshVisualMode();
        }

        public void ApplyVisualProfile(OrbitalVisualProfile profile)
        {
            CurrentVisualProfile = profile;
            switch (profile)
            {
                case OrbitalVisualProfile.Clean:
                    RingAlpha = .52f; TrailAlpha = .25f; LinkAlpha = .42f;
                    ResonanceFlash = .65f; EnemyAlpha = .82f; ProjectileAlpha = .9f;
                    if (Trails.FollowVisualProfile) Trails.Mode = OrbitalTrailMode.Off;
                    break;
                case OrbitalVisualProfile.Hypnotic:
                    RingAlpha = .9f; TrailAlpha = 1.25f; LinkAlpha = 1.2f;
                    ResonanceFlash = 1.15f; EnemyAlpha = .28f; ProjectileAlpha = .55f;
                    if (Trails.FollowVisualProfile) Trails.Mode = OrbitalTrailMode.Hypnotic;
                    break;
                case OrbitalVisualProfile.Maximum:
                    RingAlpha = 1.35f; TrailAlpha = 1.5f; LinkAlpha = 1.5f;
                    ResonanceFlash = 1.5f; EnemyAlpha = 1f; ProjectileAlpha = 1.35f;
                    if (Trails.FollowVisualProfile) Trails.Mode = OrbitalTrailMode.Hypnotic;
                    break;
                default:
                    RingAlpha = .72f; TrailAlpha = .68f; LinkAlpha = .82f;
                    ResonanceFlash = .95f; EnemyAlpha = 1f; ProjectileAlpha = 1f;
                    if (Trails.FollowVisualProfile) Trails.Mode = OrbitalTrailMode.Off;
                    break;
            }
        }

        public void ApplyPatternFlower()
        {
            ResetLab(5);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain; Links.DealDamage = false; Links.ShowLinks = true;
            Resonance.Enabled = true; Resonance.Mode = OrbitalResonanceMode.Cycle; Resonance.VisualOnly = true;
            for (int r = 0; r < RingCount; r++) { Rings[r].Settings.MaxMounts = 4; AddAt(r, OrbitalMountType.LinkNode, 2); }
            AddAt(0, OrbitalMountType.Gun, 1); AddAt(2, OrbitalMountType.Blade, 1);
            ApplyMovementPreset(OrbitalMovementPreset.Flower);
            ApplyVisualProfile(OrbitalVisualProfile.Hypnotic);
            Trails.Mode = OrbitalTrailMode.Short; Trails.Length = .7f; Trails.Alpha = .18f;
            Crowd.SetCount(120, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyCombatWeb()
        {
            ResetLab(5);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain; Links.DealDamage = true; Links.Damage = 11f;
            Resonance.Enabled = true; Resonance.Mode = OrbitalResonanceMode.Beam; Resonance.VisualOnly = false;
            // The wave starts neighbouring rings 28 degrees apart. A matching tolerance makes
            // COMBAT WEB demonstrate its resonance immediately, then naturally unlatch as the
            // different ring speeds pull the formation out of alignment.
            Resonance.AlignmentTolerance = 30f; Resonance.Cooldown = .9f;
            Trails.Mode = OrbitalTrailMode.Off;
            for (int r = 0; r < RingCount; r++) { Rings[r].Settings.MaxMounts = 5; AddAt(r, OrbitalMountType.LinkNode, 2); }
            AddAt(0, OrbitalMountType.Gun, 2); AddAt(2, OrbitalMountType.Blade, 2); AddAt(4, OrbitalMountType.Pusher, 1);
            ApplyMovementPreset(OrbitalMovementPreset.Wave);
            ApplyVisualProfile(OrbitalVisualProfile.Combat);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(200, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyOrbitalFortress()
        {
            ResetLab(6);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.AllNearby; Links.MaxDistance = 7f; Links.DealDamage = true;
            Resonance.Enabled = true; Resonance.Mode = OrbitalResonanceMode.Cycle; Resonance.VisualOnly = false;
            Trails.Mode = OrbitalTrailMode.Off;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 6;
                Rings[r].Settings.FieldMode = (OrbitalRingFieldMode)(r % 5);
                for (int slot = 0; slot < 6; slot++)
                    CreateMountedAt(Rings[r], slot, (OrbitalMountType)((r + slot) % 4));
            }
            ApplyMovementPreset(OrbitalMovementPreset.Gear);
            ApplyVisualProfile(OrbitalVisualProfile.Combat);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(300, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyHypnosis()
        {
            ResetLab(6);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain; Links.DealDamage = false; Links.ShowLinks = true;
            Resonance.Enabled = true; Resonance.Mode = OrbitalResonanceMode.Cycle; Resonance.VisualOnly = true;
            Trails.Mode = OrbitalTrailMode.Hypnotic; Trails.Length = 1.4f; Trails.Alpha = .62f;
            for (int r = 0; r < RingCount; r++) { Rings[r].Settings.MaxMounts = 5; AddAt(r, OrbitalMountType.LinkNode, 2); AddAt(r, OrbitalMountType.Gun, 1); }
            ApplyMovementPreset(OrbitalMovementPreset.Flower);
            ApplyVisualProfile(OrbitalVisualProfile.Hypnotic);
            Trails.Mode = OrbitalTrailMode.Hypnotic;
            Crowd.SetCount(0, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyDirectedFortress()
        {
            ResetLab(4);
            PatternCombat = true; FreeMountPhase = true;
            Trails.Mode = OrbitalTrailMode.Off;
            for (int r = 0; r < RingCount; r++) Rings[r].Settings.MaxMounts = 6;
            AddAt(0, OrbitalMountType.Pusher, 3);
            AddAt(1, OrbitalMountType.Blade, 4);
            AddAt(2, OrbitalMountType.Gun, 4);
            AddAt(3, OrbitalMountType.LinkNode, 3);
            ApplyMovementPreset(OrbitalMovementPreset.Sync);
            for (int r = 0; r < RingCount; r++) { SelectedRing = r; FrontArcSelected(); }
            ApplyVisualProfile(OrbitalVisualProfile.Combat);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(200, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyMiniWeaponsStart()
        {
            ApplyStartState();
            ApplyWeaponVisualMode(OrbitalWeaponVisualMode.MiniWeapons);
            Trails.Mode = OrbitalTrailMode.Off;
        }

        public void ApplyMiniWeaponsFlower()
        {
            ResetLab(5);
            ApplyWeaponVisualMode(OrbitalWeaponVisualMode.MiniWeapons);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain;
            Links.DealDamage = false;
            Resonance.Enabled = true;
            Resonance.VisualOnly = true;
            for (int r = 0; r < RingCount; r++) Rings[r].Settings.MaxMounts = 5;
            AddAt(0, OrbitalMountType.LinkNode, 2); AddAt(0, OrbitalMountType.Gun, 1); AddAt(0, OrbitalMountType.Pusher, 1);
            AddAt(1, OrbitalMountType.Gun, 2); AddAt(1, OrbitalMountType.Blade, 1);
            AddAt(2, OrbitalMountType.LinkNode, 2); AddAt(2, OrbitalMountType.Blade, 1); AddAt(2, OrbitalMountType.Pusher, 1);
            AddAt(3, OrbitalMountType.Gun, 1); AddAt(3, OrbitalMountType.Blade, 1); AddAt(3, OrbitalMountType.Pusher, 1);
            AddAt(4, OrbitalMountType.LinkNode, 2); AddAt(4, OrbitalMountType.Gun, 1);
            AddAt(4, OrbitalMountType.Blade, 1); AddAt(4, OrbitalMountType.Pusher, 1);
            ApplyMovementPreset(OrbitalMovementPreset.Flower);
            ApplyVisualProfile(OrbitalVisualProfile.Combat);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(120, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyMiniWeaponsFortress()
        {
            ResetLab(6);
            ApplyWeaponVisualMode(OrbitalWeaponVisualMode.MiniWeapons);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.AllNearby;
            Links.MaxDistance = 7f;
            Links.DealDamage = true;
            Resonance.Enabled = true;
            Resonance.VisualOnly = false;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 5;
                Rings[r].Settings.FieldMode = (OrbitalRingFieldMode)(r % 5);
                CreateMountedAt(Rings[r], 0, OrbitalMountType.Gun);
                CreateMountedAt(Rings[r], 1, OrbitalMountType.Blade);
                CreateMountedAt(Rings[r], 2, OrbitalMountType.Pusher);
                CreateMountedAt(Rings[r], 3, OrbitalMountType.LinkNode);
                if (r < 4) CreateMountedAt(Rings[r], 4, (OrbitalMountType)(r % 3));
            }
            ApplyMovementPreset(OrbitalMovementPreset.Gear);
            ApplyVisualProfile(OrbitalVisualProfile.Combat);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(300, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void ApplyLinkHypnosis()
        {
            ResetLab(6);
            PatternCombat = true;
            Links.Mode = OrbitalLinkMode.Chain;
            Links.DealDamage = false;
            Links.ShowLinks = true;
            Resonance.Enabled = true;
            Resonance.Mode = OrbitalResonanceMode.Cycle;
            Resonance.VisualOnly = true;
            for (int r = 0; r < RingCount; r++)
            {
                Rings[r].Settings.MaxMounts = 4;
                AddAt(r, OrbitalMountType.LinkNode, r == 0 || r == 2 || r >= 4 ? 3 : 2);
            }
            ApplyMovementPreset(OrbitalMovementPreset.Flower);
            ApplyVisualProfile(OrbitalVisualProfile.Hypnotic);
            Trails.Mode = OrbitalTrailMode.Off;
            Crowd.SetCount(0, PlayerPosition, OuterRingRadius);
            CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        public void SetSelectedRingMaxMounts(int value)
        {
            if (RingCount == 0) return;
            OrbitalRing ring = Rings[Mathf.Clamp(SelectedRing, 0, RingCount - 1)];
            int next = Mathf.Clamp(value, 1, OrbitalRing.AbsoluteMaxMounts);
            if (next < ring.Settings.MaxMounts)
            {
                for (int slot = OrbitalRing.AbsoluteMaxMounts - 1; slot >= next; slot--)
                    if (ring.Mounts[slot] != null) RemoveMounted(ring.Mounts[slot]);
            }
            ring.Settings.MaxMounts = next;
        }

        public bool ContainsRing(OrbitalRing ring)
        {
            for (int i = 0; i < RingCount; i++) if (Rings[i] == ring) return true;
            return false;
        }

        public bool HasLinkNodeOnRing(OrbitalRing ring)
        {
            if (ring == null) return false;
            for (int i = 0; i < ring.Mounts.Length; i++)
                if (ring.Mounts[i] is OrbitalLinkNode) return true;
            return false;
        }

        public void EmitPulse(Vector2 position, Color color, float finalSize, float duration)
        {
            Pulse pulse = pulses[pulseCursor++];
            if (pulseCursor >= pulses.Length) pulseCursor = 0;
            pulse.Transform.position = position;
            pulse.Transform.localScale = Vector3.one * .05f;
            pulse.Renderer.color = color;
            pulse.Renderer.gameObject.SetActive(true);
            pulse.Born = Time.unscaledTime;
            pulse.Duration = duration;
            pulse.FinalSize = finalSize;
            pulse.Color = color;
            pulse.Active = true;
        }

        public void ImpulseCamera(float amount)
        {
            if (CameraImpulse) CameraRig.AddImpulse(amount);
        }

        private void BuildWorld()
        {
            factory = new OrbitalPrimitiveFactory();
            WorldRoot = new GameObject("ORBITAL COMBAT LAB - Runtime World").transform;
            WorldRoot.SetParent(transform, false);
            if (IntegrationMode)
            {
                player = IntegrationPlayer != null ? IntegrationPlayer : transform;
                previousIntegratedPlayerPosition = player.position;
            }
            else
            {
                GameObject globalLightObject = new("Global Light 2D");
                globalLightObject.transform.SetParent(WorldRoot, false);
                GlobalLight = globalLightObject.AddComponent<Light2D>();
                GlobalLight.lightType = Light2D.LightType.Global;
                GlobalLight.intensity = 1f;
                GlobalLight.color = Color.white;
                SpriteRenderer arena = factory.CreateSprite("Arena", WorldRoot, factory.Square,
                    new Color(.018f, .027f, .045f, 1f), new Vector2(80f, 80f), -20);
                arena.transform.position = Vector3.zero;
                BuildGrid();
                Transform playerRoot = new GameObject("Player").transform;
                playerRoot.SetParent(WorldRoot, false);
                SpriteRenderer playerRenderer = factory.CreateSprite("Fallback", playerRoot, factory.Circle,
                    new Color(.74f, 1f, 1f, 1f), new Vector2(.7f, .7f), 15);
                PlayerVisual = OrbitalActorVisual.CreatePlayer(playerRoot);
                playerRenderer.enabled = !PlayerVisual.IsAvailable;
                player = playerRoot;
            }

            Crowd = new OrbitalEnemyCrowd(WorldRoot, factory, Stats,
                position => EmitPulse(position, new Color(1f, .16f, .12f, .72f), .62f, .16f),
                !IntegrationMode);
            if (IntegrationMode) Crowd.UseExternalEnemies();
            Projectiles = new OrbitalProjectilePool(WorldRoot, factory, Crowd);
            MineSystem = new OrbitalMineSystem(this, WorldRoot, factory);
            ArcSystem = new OrbitalArcSystem(WorldRoot, factory);
            BuildPulses();
            Pattern = new OrbitalPatternCombatSystem(this, WorldRoot, factory);
            CoreSystem = new OrbitalCoreSystem(this, WorldRoot, factory);

            Drag = gameObject.AddComponent<OrbitalLabDragController>();
            Drag.Configure(this);
            CameraRig = gameObject.AddComponent<OrbitalLabCameraRig>();
            CameraRig.Configure(this, IntegrationMode ? IntegrationCamera : null, !IntegrationMode);
            DebugUI = gameObject.AddComponent<OrbitalLabDebugUI>();
            DebugUI.Configure(this);
            GoldenPath = gameObject.AddComponent<OrbitalGoldenPath>();
            GoldenPath.Configure(this);
        }

        private void BuildGrid()
        {
            for (int i = -16; i <= 16; i += 2)
            {
                LineRenderer horizontal = factory.CreateCircleLine($"Grid H {i}", WorldRoot, -19, 2);
                horizontal.loop = false;
                horizontal.startWidth = horizontal.endWidth = .018f;
                horizontal.startColor = horizontal.endColor = new Color(.08f, .19f, .24f, .22f);
                horizontal.SetPosition(0, new Vector3(-40f, i, 0f));
                horizontal.SetPosition(1, new Vector3(40f, i, 0f));
                LineRenderer vertical = factory.CreateCircleLine($"Grid V {i}", WorldRoot, -19, 2);
                vertical.loop = false;
                vertical.startWidth = vertical.endWidth = .018f;
                vertical.startColor = vertical.endColor = new Color(.08f, .19f, .24f, .22f);
                vertical.SetPosition(0, new Vector3(i, -40f, 0f));
                vertical.SetPosition(1, new Vector3(i, 40f, 0f));
            }
        }

        private void BuildPulses()
        {
            Transform root = new GameObject("Impact Pulse Pool").transform;
            root.SetParent(WorldRoot, false);
            for (int i = 0; i < pulses.Length; i++)
            {
                SpriteRenderer renderer = factory.CreateSprite($"Pulse {i + 1:00}", root,
                    factory.Circle, Color.clear, Vector2.one, 12);
                renderer.gameObject.SetActive(false);
                pulses[i] = new Pulse { Transform = renderer.transform, Renderer = renderer };
            }
        }

        private void TickPulses()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < pulses.Length; i++)
            {
                Pulse pulse = pulses[i];
                if (!pulse.Active) continue;
                float t = Mathf.Clamp01((now - pulse.Born) / Mathf.Max(.01f, pulse.Duration));
                pulse.Transform.localScale = Vector3.one * Mathf.Lerp(.05f, pulse.FinalSize, t);
                Color color = pulse.Color;
                color.a *= 1f - t;
                pulse.Renderer.color = color;
                if (t < 1f) continue;
                pulse.Active = false;
                pulse.Renderer.gameObject.SetActive(false);
            }
        }

        private void TickPlayer(float dt)
        {
            if (IntegrationMode)
            {
                Vector2 current = PlayerPosition;
                Vector2 delta = current - previousIntegratedPlayerPosition;
                if (delta.sqrMagnitude > .000001f) LastMoveDirection = delta.normalized;
                previousIntegratedPlayerPosition = current;
                return;
            }
            Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (input.sqrMagnitude > .01f) LastMoveDirection = input.normalized;
            PlayerVisual?.SetMotion(LastMoveDirection, input.sqrMagnitude > .01f);
            if (Drag != null && Drag.IsDragging) return;
            player.position += (Vector3)(input * (3.8f * dt));
        }

        private void TickFps()
        {
            fpsAccumulator += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsAccumulator < .35f) return;
            Stats.SmoothedFps = fpsFrames / Mathf.Max(.001f, fpsAccumulator);
            fpsAccumulator = 0f;
            fpsFrames = 0;
        }

        private void ResetLab(int desiredRings)
        {
            if (!IntegrationMode) Time.timeScale = 1f;
            Drag?.CancelDrag();
            ClearMounted();
            for (int i = RingCount - 1; i >= 0; i--)
            {
                Rings[i].Destroy();
                Rings[i] = null;
            }
            RingCount = 0;
            if (!IntegrationMode && player != null) player.position = Vector3.zero;
            PlayerHp = 100f;
            Stats.Reset();
            ResetPatternDefaults();
            for (int i = 0; i < desiredRings; i++) AddRing();
            SelectedRing = 0;
        }

        private void CameraSnapIfAllowed()
        {
            if (CameraRig != null && (!IntegrationMode || IntegrationCameraOverride))
                CameraRig.Snap(PlayerPosition, OuterRingRadius);
        }

        private void ResetPatternDefaults()
        {
            PatternCombat = false;
            RingEditMode = false;
            PauseSelectedRingWhileEditing = true;
            FreeMountPhase = false;
            movementFrozen = false;
            CurrentMovementPreset = OrbitalMovementPreset.Default;
            Links.Mode = OrbitalLinkMode.Pairs;
            Links.Damage = 8f; Links.HitCooldown = .35f; Links.LineWidth = .055f;
            Links.MaxDistance = 9f; Links.PulseSpeed = 3f; Links.DealDamage = true; Links.ShowLinks = true;
            Links.LineColor = new Color(1f, .06f, .84f, 1f);
            Core.Reset();
            Resonance.Enabled = true; Resonance.AlignmentTolerance = 10f; Resonance.MinimumObjects = 2;
            Resonance.Cooldown = 1.15f; Resonance.Damage = 16f; Resonance.Range = 9f;
            Resonance.Mode = OrbitalResonanceMode.Cycle; Resonance.VisualOnly = false;
            Trails.Mode = OrbitalTrailMode.Off; Trails.Length = .75f; Trails.Width = .08f; Trails.Alpha = .38f;
            Trails.FollowVisualProfile = true;
            WeaponVisuals.Mode = OrbitalWeaponVisualMode.MiniWeapons;
            WeaponVisuals.BladeOrientation = OrbitalBladeOrientation.Tangential;
            WeaponVisuals.PistolScale = 2.15f; WeaponVisuals.LaserSwardScale = 1.85f;
            WeaponVisuals.ImpulsGunScale = 2.15f; WeaponVisuals.LinkNodeScale = 1f;
            WeaponVisuals.PistolRotationOffset = 0f; WeaponVisuals.LaserSwardRotationOffset = 0f;
            WeaponVisuals.ImpulsGunRotationOffset = 0f; WeaponVisuals.SortingOffset = 12;
            WeaponVisuals.EffectsEnabled = true; WeaponVisuals.EffectIntensity = .55f;
            WeaponVisuals.ShowPrototypeColliders = WeaponVisuals.ShowMuzzlePoints =
                WeaponVisuals.ShowVisualForward = WeaponVisuals.ShowMountRoots = false;
            ApplyVisualProfile(OrbitalVisualProfile.Combat);
            SelectedMounted = null;
            Pattern?.Reset();
            CoreSystem?.Reset();
        }

        private void ApplyFormationToSelected(int mode)
        {
            if (RingCount == 0) return;
            OrbitalRing ring = Rings[Mathf.Clamp(SelectedRing, 0, RingCount - 1)];
            int count = 0;
            for (int i = 0; i < ring.Mounts.Length; i++) if (ring.Mounts[i] != null) count++;
            if (count == 0) return;
            float forward = Mathf.Atan2(LastMoveDirection.y, LastMoveDirection.x) * Mathf.Rad2Deg;
            int ordinal = 0;
            for (int slot = 0; slot < ring.Mounts.Length; slot++)
            {
                OrbitalMountedObject mounted = ring.Mounts[slot];
                if (mounted == null) continue;
                float desired;
                if (mode == 0)
                {
                    mounted.PhaseOffset = 0f;
                    ordinal++;
                    continue;
                }
                if (mode == 1)
                    desired = forward + (ordinal - (count - 1) * .5f) * Mathf.Max(MinimumMountSpacing, 15f);
                else if (mode == 2)
                    desired = forward + Mathf.Lerp(-62f, 62f, count <= 1 ? .5f : ordinal / (float)(count - 1));
                else
                {
                    int side = ordinal & 1;
                    int row = ordinal / 2;
                    desired = forward + (side == 0 ? 38f : 218f) + row * 18f;
                }
                mounted.PhaseOffset = Mathf.DeltaAngle(BaseSlotAngle(ring, slot), desired);
                ordinal++;
            }
        }

        private static float BaseSlotAngle(OrbitalRing ring, int slot)
        {
            int count = Mathf.Clamp(ring.Settings.MaxMounts, 1, OrbitalRing.AbsoluteMaxMounts);
            return ring.FormationAngle + slot * 360f / count;
        }

        private void AddAt(int ring, OrbitalMountType type, int count)
        {
            SelectedRing = Mathf.Clamp(ring, 0, RingCount - 1);
            for (int i = 0; i < count; i++) AddMounted(type);
        }

        private bool CreateMountedAt(OrbitalRing ring, int slot, OrbitalMountType type)
        {
            if (ring == null || slot < 0 || slot >= ring.Settings.MaxMounts ||
                ring.Mounts[slot] != null || MountedCount >= MaxMountedObjects) return false;
            OrbitalMountedObject mounted = CreateMountedInstance(type);
            mounted.Attach(ring, slot);
            MountedObjects[MountedCount++] = mounted;
            return true;
        }

        private OrbitalMountedObject CreateMountedInstance(OrbitalMountType type) => type switch
        {
            OrbitalMountType.Gun => new OrbitalGun(this, factory),
            OrbitalMountType.Blade => new OrbitalBlade(this, factory),
            OrbitalMountType.Pusher => new OrbitalPusher(this, factory),
            OrbitalMountType.LinkNode => new OrbitalLinkNode(this, factory),
            OrbitalMountType.MineLayer => new OrbitalMineLayer(this, factory),
            _ => new OrbitalArcEmitter(this, factory)
        };

        private void RemoveMounted(OrbitalMountedObject target)
        {
            if (target == null) return;
            if (target is OrbitalMineLayer) MineSystem?.Clear();
            int index = -1;
            for (int i = 0; i < MountedCount; i++) if (MountedObjects[i] == target) { index = i; break; }
            target.Destroy();
            if (index < 0) return;
            for (int i = index; i < MountedCount - 1; i++) MountedObjects[i] = MountedObjects[i + 1];
            MountedObjects[--MountedCount] = null;
        }

        private void OnDisable()
        {
            if (Drag != null) Drag.CancelDrag();
            if (Time.timeScale != 1f) Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Time.timeScale != 1f) Time.timeScale = 1f;
            factory?.Dispose();
        }
    }
}
