using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{

    public sealed class OrbitalRingRuntime
    {
        private readonly OrbitalRingView view;
        private readonly bool segmented;
        public bool IsActive { get; private set; }
        private float pulse;
        private float spawnScale = 1f;
        private bool selected;
        private bool interactionEligible;
        private bool interactionHovered;
        private bool interactionDimmed;
        private float interactionEmphasis = 1f;

        public OrbitalPathGeometry Geometry { get; }
        public OrbitalRingState State { get; }

        public int RingId => State.StableRingId;
        public int RuntimeDirectionMultiplier { get; set; } = 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float LabRotationMultiplier { get; set; } = 1f;
        public System.Func<int, float, float> LabMountPhase { get; set; }
#endif
        public float Radius => Geometry.Type == OrbitalPathType.Custom ? 1f :
            State.Radius + OrbitalPresentationConfig.Active.RingRadiusPadding;
        public float RotationSpeed => State.BaseRotationSpeed *
            Mathf.Pow(1f + OrbitalProgressionConfig.Default.SpeedIncrement,
                State.SpeedUpgradeLevel);
        public int Direction => State.Direction * RuntimeDirectionMultiplier;
        public float Phase => State.CurrentPhase;
        public int MountCapacity => State.MountCapacity;
        public int MountCount => State.MountCount;
        public float PowerMultiplier => State.PowerMultiplier;
        public int VisualTier => State.VisualTier;
        public Color AccentColor => view.AccentColor;
        public List<OrbitalMountRuntime> Mounts { get; } = new();

        public OrbitalRingRuntime(OrbitalRingState state,
            Transform root, Material material, Sprite sprite,
            bool animateSpawn = false, OrbitalPathGeometry geometry = null, bool segmented = false, bool active = true)
        {
            State = state;
            this.segmented = segmented;
            IsActive = active;
            Geometry = geometry ?? OrbitalPathGeometry.Circle;
            view = Object.Instantiate(OrbitalPresentationConfig.Active.RingPrefab, root, false);
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(view.gameObject, OrbitalPresentationConfig.Active.RingPrefab.gameObject);
            if (!view.IsValid) throw new System.InvalidOperationException("authored ring references missing");
            spawnScale = animateSpawn ? 0.05f : 1f;
            view.InitializeGeometry(Geometry);
            view.InitializeTier(VisualTier);
            view.UpdateTierAppearance(VisualTier, Radius * spawnScale, 0f, 0f, false,
                Geometry.Type == OrbitalPathType.FigureEight || State.Order == 0, 0f);
            for (int i = 0; i < State.MountCount; i++)
                Mounts.Add(new OrbitalMountRuntime(this, i, view.MountsRoot, sprite));
            RebalanceMounts();
            view.gameObject.SetActive(active);
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            float angularDelta = RotationSpeed * Direction * deltaTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            angularDelta *= LabRotationMultiplier;
#endif
            State.CurrentPhase = Mathf.Repeat(
                State.CurrentPhase + angularDelta, 360f);
            pulse = Mathf.MoveTowards(pulse, 0f, deltaTime * 2.5f);
            spawnScale = Mathf.MoveTowards(spawnScale, 1f,
                deltaTime * 2.8f);
            float interactionPulse = interactionEligible
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f)
                : 0f;
            float highlight = (selected ? 1f : 0f) +
                ((interactionEligible ? .5f + interactionPulse * .5f : 0f) +
                 (interactionHovered ? 1f : 0f)) * interactionEmphasis;
            view.UpdateTierAppearance(VisualTier, Radius * spawnScale, pulse, highlight,
                interactionDimmed, Geometry.Type == OrbitalPathType.FigureEight || State.Order == 0, deltaTime, Time.unscaledDeltaTime);
            for (int i = 0; i < Mounts.Count; i++)
                Mounts[i].UpdatePosition(State.CurrentPhase, Radius * spawnScale);
        }

        public void SetActive(bool active, bool constructionPreview)
        {
            IsActive = active;
            bool visible = active || constructionPreview;
            if (view.gameObject.activeSelf == visible) return;
            view.gameObject.SetActive(visible);
            if (visible)
                foreach (var mount in Mounts) mount.UpdatePosition(Phase, Radius);
        }

        public OrbitalMountRuntime AddMount(Transform root, Sprite sprite)
        {
            OrbitalMountRuntime mount = new(this, Mounts.Count, view.MountsRoot, sprite);
            Mounts.Add(mount);
            RebalanceMounts();
            return mount;
        }


        // Visual feedback only; unlike Pulse this does not activate modules.
        public void PulseVisual() => pulse = 1f;

        public void SetSelected(bool selected)
        {
            this.selected = selected;
        }

        public void SetInteractionState(bool eligible, bool hovered,
            bool dimmed = false, float emphasis = 1f, bool showSelectionMarkers = false)
        {
            interactionEligible = eligible;
            interactionHovered = eligible && hovered;
            interactionDimmed = dimmed;
            interactionEmphasis = emphasis;
            view.SetSelectionMarkers(showSelectionMarkers && eligible, hovered);
        }

        public void Teardown()
        {
            for (int i = 0; i < Mounts.Count; i++)
                Mounts[i].Teardown();
            Mounts.Clear();
            if (view != null)
                Object.Destroy(view.gameObject);
        }

        private void RebalanceMounts()
        {
            for (int i = 0; i < Mounts.Count; i++)
            {
                float localPhase = segmented ? OrbitalRunState.SegmentedMountAngle(State.Order, i)
                    : i * 360f / Mounts.Count;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (LabMountPhase != null) localPhase = LabMountPhase(i, localPhase);
#endif
                Mounts[i].SetLocalPhase(localPhase);
                Mounts[i].UpdatePosition(State.CurrentPhase, Radius * spawnScale);
            }
        }
    }

    public sealed class OrbitalMountRuntime
    {
        public enum VisualState
        {
            Normal, Occupied, Hover, Valid, ValidHover, Invalid, Preview
        }

        private readonly Transform root;
        private readonly OrbitalMountView view;
        private readonly SpriteRenderer marker;
        private readonly SpriteRenderer halo;
        private VisualState visualState;
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
            view = Object.Instantiate(OrbitalPresentationConfig.Active.MountPrefab, root, false);
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(view.gameObject, OrbitalPresentationConfig.Active.MountPrefab.gameObject);
            if (!view.IsValid) throw new System.InvalidOperationException("authored mount references missing");
            this.root = view.transform;
            marker = view.Marker;
            halo = view.Halo;
            SetVisualState(VisualState.Normal);
        }

        public void SetLocalPhase(float value) => LocalPhase = value;

        public void UpdatePosition(float ringPhase, float radius)
        {
            if (root == null)
                return;
            root.localPosition = Ring.Geometry.PositionDegrees(ringPhase + LocalPhase, radius);
            if (Ring.Geometry.Type == OrbitalPathType.FigureEight)
                view.UpdateFigureEightDepth(Ring.Geometry.IsBehind(root.localPosition));
            else
                // Continuous circle lines use Player sorting; keep every weapon above them.
                view.UpdateDepth(root.localPosition.y, false);
            Module?.UpdateVisualRotation(Ring.Geometry.Rotation(ringPhase + LocalPhase));
            SetVisualState(visualState);
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
            visualState = state;
            Color color = Ring.AccentColor;
            color.a = config.NormalAlpha;
            float size = config.NormalMountSize;
            bool showHalo = false;
            switch (state)
            {
                case VisualState.Occupied:
                    break;
                case VisualState.Hover:
                    color.a = config.HoverAlpha;
                    size = config.SelectionMountSize;
                    showHalo = true;
                    break;
                case VisualState.Valid:
                    color = new Color(0.38f, 1f, 0.72f, 0.95f);
                    size = config.SelectionMountSize * 0.82f *
                        (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 6f));
                    showHalo = true;
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
            marker.enabled = state != VisualState.Occupied;
            marker.color = color;
            halo.enabled = showHalo;
            halo.transform.localScale = Vector3.one * config.HaloSize;
            halo.color = new Color(color.r, color.g, color.b,
                state == VisualState.Valid ? 0.12f : 0.2f);
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
