using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Subject42.Prototype.OrbitalCombatLab
{
    [DisallowMultipleComponent]
    public sealed class OrbitalCombatLabController : MonoBehaviour
    {
        public const int MaxRings = 6;
        public const int MaxMountedObjects = 48;
        private static readonly float[] DefaultRadii = { 1.5f, 2.5f, 3.7f, 5f, 6.5f, 8.2f };
        private static readonly float[] DefaultSpeeds = { 105f, 72f, 57f, 43f, 34f, 27f };

        public readonly GunSettings Gun = new();
        public readonly BladeSettings Blade = new();
        public readonly PusherSettings Pusher = new();
        public readonly LinkSettings Links = new();
        public readonly ResonanceSettings Resonance = new();
        public readonly TrailSettings Trails = new();
        public readonly WeaponVisualSettings WeaponVisuals = new();
        public readonly OrbitalLabStats Stats = new();
        public readonly OrbitalRing[] Rings = new OrbitalRing[MaxRings];
        public readonly OrbitalMountedObject[] MountedObjects = new OrbitalMountedObject[MaxMountedObjects];

        public Transform WorldRoot { get; private set; }
        public Vector2 PlayerPosition => player != null ? player.position : Vector2.zero;
        public OrbitalEnemyCrowd Crowd { get; private set; }
        public OrbitalProjectilePool Projectiles { get; private set; }
        public OrbitalLabDragController Drag { get; private set; }
        public OrbitalLabCameraRig CameraRig { get; private set; }
        public OrbitalLabDebugUI DebugUI { get; private set; }
        public OrbitalPatternCombatSystem Pattern { get; private set; }
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

        private void Awake()
        {
            BuildWorld();
            ApplyStartState();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickFps();
            TickPlayer(dt);

            for (int i = 0; i < RingCount; i++)
            {
                bool highlighted = Drag != null && Drag.CandidateRing == i;
                int previewSlot = highlighted ? Drag.CandidateSlot : -1;
                bool selectedForEdit = RingEditMode && SelectedRing == i;
                bool hoveredForEdit = RingEditMode && DebugUI != null && DebugUI.HoveredRing == i;
                Rings[i].Tick(PlayerPosition, dt, ShowRings, ShowMounts || RingEditMode,
                    highlighted, selectedForEdit, hoveredForEdit,
                    selectedForEdit && PauseSelectedRingWhileEditing, previewSlot, RingAlpha);
            }
            for (int i = 0; i < MountedCount; i++)
            {
                OrbitalMountedObject mounted = MountedObjects[i];
                if (mounted == null) continue;
                mounted.SetRangesVisible(ShowAttackRanges);
                mounted.Tick(dt);
            }

            Pattern.Tick(dt);
            Crowd.VisualAlpha = EnemyAlpha;
            Projectiles.VisualAlpha = ProjectileAlpha;
            Crowd.Tick(PlayerPosition, OuterRingRadius, dt, PlayerImmortal, ref PlayerHp);
            Crowd.ApplyRingContact(PlayerPosition, Rings, RingCount,
                RingContactDamage, RingContactPush, dt);
            Projectiles.Tick(dt);
            TickPulses();
            Drag.Tick();
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
            if (RingCount >= MaxRings) return false;
            int index = RingCount;
            OrbitalRing ring = new(index, WorldRoot, factory);
            ring.ApplyDefaults(DefaultRadii[index], DefaultSpeeds[index], index % 2 == 1,
                index < 2 ? 4 : 6);
            Rings[RingCount++] = ring;
            SelectedRing = RingCount - 1;
            return true;
        }

        public bool RemoveRing()
        {
            if (RingCount <= 1) return false;
            Drag.CancelDrag();
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

        public void ClearMounted()
        {
            Drag.CancelDrag();
            for (int i = MountedCount - 1; i >= 0; i--)
                MountedObjects[i]?.Destroy();
            for (int i = 0; i < MountedObjects.Length; i++) MountedObjects[i] = null;
            MountedCount = 0;
            Projectiles.Clear();
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
                        Rings[i].Settings.RotationSpeed = speeds[i];
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
                        Rings[i].Settings.RotationSpeed = speeds[i];
                        Rings[i].Settings.Clockwise = i == 1 || i == 4 || i == 5;
                        Rings[i].RotationAngle = 0f;
                        Rings[i].PhaseOffset = (i * 67f + 19f) % 360f;
                    }
                    break;
                }
                default:
                    for (int i = 0; i < RingCount; i++)
                    {
                        Rings[i].Settings.Radius = DefaultRadii[i];
                        Rings[i].Settings.RotationSpeed = DefaultSpeeds[i];
                        Rings[i].Settings.Clockwise = i % 2 == 1;
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

            Crowd = new OrbitalEnemyCrowd(WorldRoot, factory, Stats,
                position => EmitPulse(position, new Color(1f, .16f, .12f, .72f), .62f, .16f));
            Projectiles = new OrbitalProjectilePool(WorldRoot, factory, Crowd);
            BuildPulses();
            Pattern = new OrbitalPatternCombatSystem(this, WorldRoot, factory);

            Drag = gameObject.AddComponent<OrbitalLabDragController>();
            Drag.Configure(this);
            CameraRig = gameObject.AddComponent<OrbitalLabCameraRig>();
            CameraRig.Configure(this);
            DebugUI = gameObject.AddComponent<OrbitalLabDebugUI>();
            DebugUI.Configure(this);
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
            Time.timeScale = 1f;
            Drag?.CancelDrag();
            ClearMounted();
            for (int i = RingCount - 1; i >= 0; i--)
            {
                Rings[i].Destroy();
                Rings[i] = null;
            }
            RingCount = 0;
            player.position = Vector3.zero;
            PlayerHp = 100f;
            Stats.Reset();
            ResetPatternDefaults();
            for (int i = 0; i < desiredRings; i++) AddRing();
            SelectedRing = 0;
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
            OrbitalMountedObject mounted = type switch
            {
                OrbitalMountType.Gun => new OrbitalGun(this, factory),
                OrbitalMountType.Blade => new OrbitalBlade(this, factory),
                OrbitalMountType.Pusher => new OrbitalPusher(this, factory),
                _ => new OrbitalLinkNode(this, factory)
            };
            mounted.Attach(ring, slot);
            MountedObjects[MountedCount++] = mounted;
            return true;
        }

        private void RemoveMounted(OrbitalMountedObject target)
        {
            if (target == null) return;
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
