using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalRing
    {
        public const int AbsoluteMaxMounts = 12;
        public readonly OrbitalRingSettings Settings = new();
        public readonly OrbitalRingUpgradeState Upgrades = new();
        public readonly OrbitalMountedObject[] Mounts = new OrbitalMountedObject[AbsoluteMaxMounts];
        public readonly int Index;
        public float RotationAngle;
        public float PhaseOffset;
        public float PreviewRotationMultiplier = 1f;
        public float FormationAngle => Mathf.Repeat(RotationAngle + PhaseOffset, 360f);
        public float MaximumVisualRadius => Settings.Radius +
            (Settings.Shape == OrbitalShape.Ellipse ? Settings.Radius * Mathf.Max(0f, Settings.AspectRatio - 1f) :
            Settings.Shape == OrbitalShape.Breathing ? Mathf.Abs(Settings.BreathingAmplitude) :
            Settings.Shape == OrbitalShape.Wobble ? Mathf.Abs(Settings.WobbleAmplitude) : 0f);

        private readonly LineRenderer line;
        private readonly SpriteRenderer[] points = new SpriteRenderer[AbsoluteMaxMounts];
        private readonly SpriteRenderer[] powerSegments = new SpriteRenderer[4];
        private readonly Transform root;
        private readonly Color baseColor;
        private float fieldFlashUntil;
        private float upgradeFlashUntil;

        public OrbitalRing(int index, Transform parent, OrbitalPrimitiveFactory factory)
        {
            Index = index;
            root = new GameObject($"Ring {index + 1}").transform;
            root.SetParent(parent, false);
            int segments = index < 8 ? 112 : index < 16 ? 72 : 48;
            line = factory.CreateCircleLine("Orbit", root, 2 + Mathf.Min(index / 8, 3), segments);
            baseColor = Color.HSVToRGB((.51f + index * .105f) % 1f, .72f, 1f);
            baseColor.a = .38f;
            Settings.Color = baseColor;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = factory.CreateSprite($"Mount {i + 1}", root, factory.Circle,
                    new Color(.7f, .78f, .82f, .34f), new Vector2(.14f, .14f), 4);
            }
            for (int i = 0; i < powerSegments.Length; i++)
                powerSegments[i] = factory.CreateSprite($"Power Segment {i + 1}", root, factory.Square,
                    new Color(.45f, 1f, 1f, .78f), new Vector2(.08f, .32f), 5);
        }

        public void ApplyDefaults(float radius, float speed, bool clockwise, int mounts)
        {
            Settings.Radius = radius;
            Settings.RotationSpeed = speed;
            Settings.Clockwise = clockwise;
            Settings.MaxMounts = mounts;
        }

        public void Tick(Vector2 center, float deltaTime, bool showRings, bool showMounts,
            bool dropHighlight, bool selected, bool hovered, bool editPaused,
            int previewSlot, float ringAlpha, bool upgradeVisuals = true)
            => Tick(center, deltaTime, showRings, showMounts, dropHighlight, selected, hovered,
                editPaused, previewSlot, ringAlpha, upgradeVisuals, false, false);

        public void Tick(Vector2 center, float deltaTime, bool showRings, bool showMounts,
            bool dropHighlight, bool selected, bool hovered, bool editPaused,
            int previewSlot, float ringAlpha, bool upgradeVisuals, bool dimForSelection,
            bool invalidSelection = false)
        {
            if (!Settings.Paused && !editPaused)
                RotationAngle = Mathf.Repeat(RotationAngle + Settings.RotationSpeed *
                    Upgrades.RotationSpeedMultiplier * PreviewRotationMultiplier *
                    (Settings.Clockwise ? -1f : 1f) * deltaTime, 360f);
            line.enabled = showRings && Settings.Visible;
            if (line.enabled)
            {
                UpdateLine(center);
                bool fieldFlash = Time.unscaledTime < fieldFlashUntil;
                bool upgradeFlash = Time.unscaledTime < upgradeFlashUntil;
                Color color = invalidSelection ? new Color(1f, .08f, .12f, .98f) :
                    dropHighlight ? new Color(.2f, 1f, .45f, .9f) :
                    selected ? new Color(.35f, .96f, 1f, .95f) :
                    hovered ? new Color(.62f, .9f, 1f, .76f) :
                    upgradeFlash ? new Color(.72f, 1f, 1f, .98f) :
                    fieldFlash ? new Color(1f, .28f, .92f, .92f) : Settings.Color;
                if (upgradeVisuals && Upgrades.Level > 0 && !selected && !hovered)
                    color = Color.Lerp(color, new Color(.3f, .92f, 1f, color.a), Mathf.Min(.34f, Upgrades.Level * .055f));
                if (dimForSelection && !dropHighlight && !selected && !hovered) color.a *= .24f;
                color.a *= Mathf.Clamp01(ringAlpha * Settings.GeneratedLineAlpha);
                line.startColor = line.endColor = color;
                line.startWidth = line.endWidth = invalidSelection ? Settings.LineWidth * 3f :
                    dropHighlight || selected
                    ? Settings.LineWidth * 2.6f : hovered ? Settings.LineWidth * 1.75f :
                    Settings.LineWidth * (upgradeVisuals ? 1f + Mathf.Min(.5f, Upgrades.Level * .07f) : 1f);
            }

            int powerLevel = DamageUpgradeLevel;
            float powerPulse = .76f + Mathf.Sin(Time.unscaledTime * 4.2f + Index) * .16f;
            for (int i = 0; i < powerSegments.Length; i++)
            {
                bool active = showRings && upgradeVisuals && i < powerLevel;
                powerSegments[i].gameObject.SetActive(active);
                if (!active) continue;
                float angle = FormationAngle + 22f + i * 13f;
                powerSegments[i].transform.position = GetPositionForAngle(center, angle);
                powerSegments[i].transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
                Color segmentColor = new(.42f, 1f, 1f, powerPulse * (dimForSelection && !selected && !hovered ? .28f : 1f));
                powerSegments[i].color = segmentColor;
            }

            int activeSlots = Mathf.Clamp(Settings.MaxMounts, 1, AbsoluteMaxMounts);
            for (int i = 0; i < points.Length; i++)
            {
                bool active = showMounts && i < activeSlots;
                points[i].gameObject.SetActive(active);
                if (!active) continue;
                points[i].transform.position = GetSlotPosition(center, i);
                bool occupied = Mounts[i] != null;
                points[i].color = i == previewSlot
                    ? new Color(.2f, 1f, .42f, .95f)
                    : occupied ? new Color(1f, .24f, .22f, .42f)
                    : new Color(.72f, .8f, .85f, .36f);
                points[i].transform.localScale = Vector3.one * (i == previewSlot ? .25f : .14f);
            }
        }

        public Vector2 GetSlotPosition(Vector2 center, int slot)
        {
            int count = Mathf.Clamp(Settings.MaxMounts, 1, AbsoluteMaxMounts);
            float degrees = FormationAngle + slot * 360f / count;
            return GetPositionForAngle(center, degrees);
        }

        public Vector2 GetMountedPosition(Vector2 center, OrbitalMountedObject mounted) =>
            GetPositionForAngle(center, GetMountedAngle(mounted));

        public float GetMountedAngle(OrbitalMountedObject mounted)
        {
            int count = Mathf.Clamp(Settings.MaxMounts, 1, AbsoluteMaxMounts);
            return Mathf.Repeat(FormationAngle + mounted.Slot * 360f / count + mounted.PhaseOffset, 360f);
        }

        public Vector2 GetPositionForAngle(Vector2 center, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float radius = Settings.Radius;
            if (Settings.Shape == OrbitalShape.Breathing)
                radius += Mathf.Sin(Time.unscaledTime * Settings.BreathingFrequency * Mathf.PI * 2f +
                    Settings.BreathingPhase * Mathf.Deg2Rad) * Settings.BreathingAmplitude;
            else if (Settings.Shape == OrbitalShape.Wobble)
                radius += Mathf.Sin(radians * Mathf.Max(1, Settings.WobbleLobes) +
                    Time.unscaledTime * Settings.WobbleSpeed) * Settings.WobbleAmplitude;

            Vector2 local = new(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
            if (Settings.Shape == OrbitalShape.Ellipse)
            {
                local.x *= Mathf.Max(.35f, Settings.AspectRatio);
                local = Quaternion.Euler(0f, 0f, Settings.ShapeRotation) * local;
            }
            return center + local;
        }

        public float DistanceToPath(Vector2 center, Vector2 position)
        {
            Vector2 delta = position - center;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            return Vector2.Distance(position, GetPositionForAngle(center, angle));
        }

        public void FlashField(float duration) =>
            fieldFlashUntil = Mathf.Max(fieldFlashUntil, Time.unscaledTime + duration);

        public void FlashUpgrade(float duration = .65f) =>
            upgradeFlashUntil = Mathf.Max(upgradeFlashUntil, Time.unscaledTime + duration);

        public float EffectiveRotationSpeed => Settings.RotationSpeed * Upgrades.RotationSpeedMultiplier;
        public int DamageUpgradeLevel => Mathf.Clamp(Mathf.RoundToInt(
            Mathf.Log(Mathf.Max(1f, Upgrades.DamageMultiplier)) / Mathf.Log(1.25f)), 0, powerSegments.Length);

        private void UpdateLine(Vector2 center)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = GetPositionForAngle(center, i * 360f / count);
                line.SetPosition(i, new Vector3(position.x, position.y, 0f));
            }
        }

        public int FindFreeSlot(Vector2 center, Vector2 nearPosition)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            int count = Mathf.Clamp(Settings.MaxMounts, 1, AbsoluteMaxMounts);
            for (int i = 0; i < count; i++)
            {
                if (Mounts[i] != null) continue;
                float sqr = (GetSlotPosition(center, i) - nearPosition).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        public void Destroy()
        {
            Object.Destroy(root.gameObject);
        }
    }
}
