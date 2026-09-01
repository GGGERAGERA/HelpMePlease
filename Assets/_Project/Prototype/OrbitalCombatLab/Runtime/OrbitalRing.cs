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

        private readonly LineRenderer line;
        private readonly SpriteRenderer[] points = new SpriteRenderer[AbsoluteMaxMounts];
        private readonly Transform root;
        private readonly Color baseColor;

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
            bool highlight, int previewSlot)
        {
            Angle = Mathf.Repeat(Angle + Settings.RotationSpeed *
                (Settings.Clockwise ? -1f : 1f) * deltaTime, 360f);
            line.enabled = showRings && Settings.Visible;
            if (line.enabled)
            {
                OrbitalPrimitiveFactory.SetCircle(line, center, Settings.Radius);
                Color color = highlight ? new Color(.2f, 1f, .45f, .9f) : Settings.Color;
                line.startColor = line.endColor = color;
                line.startWidth = line.endWidth = highlight ? Settings.LineWidth * 2f : Settings.LineWidth;
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
            float radians = degrees * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Settings.Radius;
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
