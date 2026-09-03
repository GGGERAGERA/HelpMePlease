using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public sealed class OrbitalCoreRuntime
    {
        private readonly OrbitalCoreState state;
        private float pulseTimer;
        private int cascadeIndex = -1;
        private float cascadeTimer;

        public int Level => state.Level;
        public float DamageMultiplier => state.DamageMultiplier;
        public float CooldownMultiplier => state.CooldownMultiplier;
        public bool CascadeActive => Level > 0 && cascadeIndex >= 0;

        public OrbitalCoreRuntime(OrbitalCoreState coreState)
        {
            state = coreState;
        }

        public void Tick(float deltaTime, IReadOnlyList<OrbitalRingRuntime> rings)
        {
            if (Level <= 0 || rings.Count == 0)
                return;
            pulseTimer += deltaTime;
            if (cascadeIndex < 0 && pulseTimer >= Mathf.Max(2.8f, 6f - Level * 0.35f))
            {
                pulseTimer = 0f;
                cascadeIndex = 0;
                cascadeTimer = 0f;
            }
            if (cascadeIndex < 0)
                return;
            cascadeTimer -= deltaTime;
            if (cascadeTimer > 0f)
                return;
            OrbitalRingRuntime ring = rings[cascadeIndex];
            ring.Pulse();
            cascadeIndex++;
            cascadeTimer = 0.12f;
            if (cascadeIndex >= rings.Count)
                cascadeIndex = -1;
        }

        public void Reset()
        {
            pulseTimer = 0f;
            cascadeIndex = -1;
            cascadeTimer = 0f;
        }
    }

    public sealed class OrbitalRingRuntime
    {
        private readonly LineRenderer line;
        private readonly Color baseColor;
        private float pulse;
        private float spawnScale = 1f;
        private bool selected;
        private bool interactionEligible;
        private bool interactionHovered;
        private bool interactionDimmed;

        public OrbitalRingState State { get; }

        public int RingId => State.StableRingId;
        public float Radius => State.Radius;
        public float RotationSpeed => State.BaseRotationSpeed *
            Mathf.Pow(1f + OrbitalProgressionConfig.Default.SpeedIncrement,
                State.SpeedUpgradeLevel);
        public int Direction => State.Direction;
        public float Phase => State.CurrentPhase;
        public int MountCapacity => Mounts.Count;
        public float PowerMultiplier => State.PowerMultiplier;
        public int VisualLevel => State.VisualUpgradeLevel;
        public List<OrbitalMountRuntime> Mounts { get; } = new();

        public OrbitalRingRuntime(OrbitalRingState state,
            Transform root, Material material, Sprite sprite,
            bool animateSpawn = false)
        {
            State = state;
            GameObject ringObject = new($"Orbital Ring {RingId}");
            ringObject.transform.SetParent(root, false);
            line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 112;
            line.widthMultiplier = 0.045f;
            line.sharedMaterial = material;
            line.sortingLayerName = "Player";
            line.sortingOrder = 6;
            float ringAlpha = OrbitalPresentationConfig.Active.RingLineAlpha;
            baseColor = Color.HSVToRGB((0.51f + State.Order * 0.105f) % 1f,
                0.72f, 1f);
            baseColor.a = ringAlpha;
            line.startColor = line.endColor = baseColor;
            spawnScale = animateSpawn ? 0.05f : 1f;
            line.transform.localScale = Vector3.one * spawnScale;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius);
            }
            for (int i = 0; i < Mathf.Max(1, State.MountCapacity); i++)
                Mounts.Add(new OrbitalMountRuntime(this, i, root, sprite));
            RebalanceMounts();
        }

        public void Tick(float deltaTime)
        {
            State.CurrentPhase = Mathf.Repeat(
                State.CurrentPhase + RotationSpeed * Direction * deltaTime, 360f);
            pulse = Mathf.MoveTowards(pulse, 0f, deltaTime * 2.5f);
            spawnScale = Mathf.MoveTowards(spawnScale, 1f,
                Time.unscaledDeltaTime * 2.8f);
            line.transform.localScale = Vector3.one * spawnScale;
            float interactionPulse = interactionEligible
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f)
                : 0f;
            line.widthMultiplier = 0.045f + pulse * 0.07f +
                (selected ? 0.03f : 0f) +
                (interactionEligible ? 0.012f + interactionPulse * 0.008f : 0f) +
                (interactionHovered ? 0.025f : 0f);
            Color displayColor = Color.Lerp(baseColor,
                new Color(0.85f, 0.3f, 1f, 0.95f), pulse);
            if (interactionEligible)
                displayColor = Color.Lerp(displayColor,
                    new Color(0.25f, 1f, 0.62f, 0.92f),
                    interactionHovered ? 0.9f : 0.38f + interactionPulse * 0.18f);
            if (interactionDimmed)
            {
                displayColor *= new Color(0.55f, 0.62f, 0.68f, 0.48f);
                displayColor.a = baseColor.a * 0.32f;
            }
            if (selected)
                displayColor = Color.Lerp(displayColor,
                    new Color(0.45f, 0.92f, 1f, 0.95f), 0.68f);
            line.startColor = line.endColor = displayColor;
            for (int i = 0; i < Mounts.Count; i++)
                Mounts[i].UpdatePosition(State.CurrentPhase, Radius * spawnScale);
        }

        public OrbitalMountRuntime AddMount(Transform root, Sprite sprite)
        {
            OrbitalMountRuntime mount = new(this, Mounts.Count, root, sprite);
            Mounts.Add(mount);
            RebalanceMounts();
            return mount;
        }

        public void Pulse()
        {
            pulse = 1f;
            for (int i = 0; i < Mounts.Count; i++)
                Mounts[i].Module?.OnCorePulse();
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;
        }

        public void SetInteractionState(bool eligible, bool hovered,
            bool dimmed = false)
        {
            interactionEligible = eligible;
            interactionHovered = eligible && hovered;
            interactionDimmed = dimmed;
        }

        public void Teardown()
        {
            for (int i = 0; i < Mounts.Count; i++)
                Mounts[i].Teardown();
            Mounts.Clear();
            if (line != null)
                Object.Destroy(line.gameObject);
        }

        private void RebalanceMounts()
        {
            for (int i = 0; i < Mounts.Count; i++)
                Mounts[i].SetLocalPhase(i * 360f / Mounts.Count);
        }
    }

    public sealed class OrbitalMountRuntime
    {
        public enum VisualState
        {
            Normal, Occupied, Hover, Valid, ValidHover, Invalid, Preview
        }

        private readonly Transform root;
        private readonly SpriteRenderer marker;
        private readonly SpriteRenderer halo;
        public OrbitalRingRuntime Ring { get; }
        public int MountIndex { get; }
        public float LocalPhase { get; private set; }
        public bool Occupied => Module != null;
        public OrbitalModuleRuntime Module { get; private set; }
        public Transform Transform => root;

        public OrbitalMountRuntime(OrbitalRingRuntime ring, int index,
            Transform root, Sprite sprite)
        {
            Ring = ring;
            MountIndex = index;
            GameObject gameObject = new($"Mount {ring.RingId}.{index + 1}");
            gameObject.transform.SetParent(root, false);
            gameObject.transform.localScale = Vector3.one;
            this.root = gameObject.transform;
            GameObject markerObject = new("Marker");
            markerObject.transform.SetParent(this.root, false);
            marker = markerObject.AddComponent<SpriteRenderer>();
            marker.sprite = sprite;
            marker.sortingLayerName = "Player";
            marker.sortingOrder = 9;
            GameObject haloObject = new("Halo");
            haloObject.transform.SetParent(this.root, false);
            halo = haloObject.AddComponent<SpriteRenderer>();
            halo.sprite = sprite;
            halo.sortingLayerName = "Player";
            halo.sortingOrder = 8;
            SetVisualState(VisualState.Normal);
        }

        public void SetLocalPhase(float value) => LocalPhase = value;

        public void UpdatePosition(float ringPhase, float radius)
        {
            if (root == null)
                return;
            float radians = (ringPhase + LocalPhase) * Mathf.Deg2Rad;
            root.localPosition = new Vector3(
                Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f);
            Module?.UpdateVisualRotation(radians);
        }

        public bool Attach(OrbitalModuleRuntime module)
        {
            if (module == null || Occupied)
                return false;
            Module = module;
            SetVisualState(VisualState.Occupied);
            module.Attach(this);
            return true;
        }

        public void Detach()
        {
            Module?.Detach();
            Module = null;
            if (marker != null)
                SetVisualState(VisualState.Normal);
        }

        public void SetVisualState(VisualState state)
        {
            if (marker == null || halo == null)
                return;
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            Color color = new(0.72f, 0.8f, 0.85f, config.NormalAlpha);
            float size = config.NormalMountSize;
            bool showHalo = false;
            switch (state)
            {
                case VisualState.Occupied:
                    color = new Color(1f, 0.24f, 0.22f, 0.22f);
                    size *= 0.7f;
                    break;
                case VisualState.Hover:
                    color.a = config.HoverAlpha;
                    size = config.SelectionMountSize;
                    showHalo = true;
                    break;
                case VisualState.Valid:
                    color = new Color(0.28f, 0.92f, 0.62f, 0.8f);
                    size = config.SelectionMountSize * 0.82f *
                        (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 6f));
                    showHalo = false;
                    break;
                case VisualState.ValidHover:
                    color = new Color(0.32f, 1f, 0.48f, 1f);
                    size = config.SelectionMountSize * 1.18f;
                    showHalo = true;
                    break;
                case VisualState.Invalid:
                    color = new Color(1f, 0.22f, 0.25f, 0.65f);
                    size = config.SelectionMountSize;
                    showHalo = true;
                    break;
                case VisualState.Preview:
                    color = new Color(0.45f, 0.95f, 1f, 0.38f);
                    showHalo = true;
                    break;
            }
            marker.transform.localScale = Vector3.one * size;
            marker.color = color;
            halo.enabled = showHalo;
            halo.transform.localScale = Vector3.one * config.HaloSize;
            halo.color = new Color(color.r, color.g, color.b, 0.2f);
        }

        public void Teardown()
        {
            Module?.Teardown();
            Module = null;
            if (marker != null)
                Object.Destroy(root.gameObject);
        }
    }
}
