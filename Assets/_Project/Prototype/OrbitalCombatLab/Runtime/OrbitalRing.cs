using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalRing
    {
        public const int AbsoluteMaxMounts = 8;
        public readonly OrbitalRingSettings Settings = new();
        public readonly OrbitalMountedObject[] Mounts = new OrbitalMountedObject[AbsoluteMaxMounts];
        public readonly int Index;
        public float Angle;
        public float MaximumVisualRadius => Settings.Radius +
            (Settings.Shape == OrbitalShape.Ellipse ? Settings.Radius * Mathf.Max(0f, Settings.AspectRatio - 1f) :
            Settings.Shape == OrbitalShape.Breathing ? Mathf.Abs(Settings.BreathingAmplitude) :
            Settings.Shape == OrbitalShape.Wobble ? Mathf.Abs(Settings.WobbleAmplitude) : 0f);

        private readonly LineRenderer line;
        private readonly SpriteRenderer[] points = new SpriteRenderer[AbsoluteMaxMounts];
        private readonly Transform root;
        private readonly Color baseColor;
        private float fieldFlashUntil;

        public OrbitalRing(int index, Transform parent, OrbitalPrimitiveFactory factory)
        {
            Index = index;
            root = new GameObject($"Ring {index + 1}").transform;
            root.SetParent(parent, false);
            line = factory.CreateCircleLine("Orbit", root, 2, 112);
            baseColor = Color.HSVToRGB((.51f + index * .105f) % 1f, .72f, 1f);
            baseColor.a = .38f;
            Settings.Color = baseColor;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = factory.CreateSprite($"Mount {i + 1}", root, factory.Circle,
                    new Color(.7f, .78f, .82f, .34f), new Vector2(.14f, .14f), 4);
            }
        }

        public void ApplyDefaults(float radius, float speed, bool clockwise, int mounts)
        {
            Settings.Radius = radius;
            Settings.RotationSpeed = speed;
            Settings.Clockwise = clockwise;
            Settings.MaxMounts = mounts;
        }

        public void Tick(Vector2 center, float deltaTime, bool showRings, bool showMounts,
            bool dropHighlight, bool selected, int previewSlot, float ringAlpha)
        {
            if (!Settings.Paused)
                Angle = Mathf.Repeat(Angle + Settings.RotationSpeed *
                    (Settings.Clockwise ? -1f : 1f) * deltaTime, 360f);
            line.enabled = showRings && Settings.Visible;
            if (line.enabled)
            {
                UpdateLine(center);
                bool fieldFlash = Time.unscaledTime < fieldFlashUntil;
                Color color = dropHighlight ? new Color(.2f, 1f, .45f, .9f) :
                    fieldFlash ? new Color(1f, .28f, .92f, .92f) : Settings.Color;
                color.a *= Mathf.Clamp01(ringAlpha) * (selected ? 1.65f : 1f);
                line.startColor = line.endColor = color;
                line.startWidth = line.endWidth = (dropHighlight || selected)
                    ? Settings.LineWidth * 2f : Settings.LineWidth;
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
            float degrees = Angle + slot * 360f / count;
            return GetPositionForAngle(center, degrees);
        }

        public Vector2 GetMountedPosition(Vector2 center, OrbitalMountedObject mounted) =>
            GetPositionForAngle(center, GetMountedAngle(mounted));

        public float GetMountedAngle(OrbitalMountedObject mounted)
        {
            int count = Mathf.Clamp(Settings.MaxMounts, 1, AbsoluteMaxMounts);
            return Mathf.Repeat(Angle + mounted.Slot * 360f / count + mounted.PhaseOffset, 360f);
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
