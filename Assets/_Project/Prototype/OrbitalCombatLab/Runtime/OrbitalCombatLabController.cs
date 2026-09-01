using UnityEngine;

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
                Rings[i].Tick(PlayerPosition, dt, ShowRings, ShowMounts,
                    highlighted, previewSlot);
            }
            for (int i = 0; i < MountedCount; i++)
            {
                OrbitalMountedObject mounted = MountedObjects[i];
                if (mounted == null) continue;
                mounted.SetRangesVisible(ShowAttackRanges);
                mounted.Tick(dt);
            }

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
                    radius = Mathf.Max(radius, Rings[i].Settings.Radius);
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
            SpriteRenderer arena = factory.CreateSprite("Arena", WorldRoot, factory.Square,
                new Color(.018f, .027f, .045f, 1f), new Vector2(80f, 80f), -20);
            arena.transform.position = Vector3.zero;
            BuildGrid();
            SpriteRenderer playerRenderer = factory.CreateSprite("Player", WorldRoot, factory.Circle,
                new Color(.74f, 1f, 1f, 1f), new Vector2(.7f, .7f), 15);
            player = playerRenderer.transform;

            Crowd = new OrbitalEnemyCrowd(WorldRoot, factory, Stats,
                position => EmitPulse(position, new Color(1f, .16f, .12f, .72f), .62f, .16f));
            Projectiles = new OrbitalProjectilePool(WorldRoot, factory, Crowd);
            BuildPulses();

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
            if (Drag != null && Drag.IsDragging) return;
            Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
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
            for (int i = 0; i < desiredRings; i++) AddRing();
            SelectedRing = 0;
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
                _ => new OrbitalPusher(this, factory)
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
