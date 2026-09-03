using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalWorldTelekinesisController : MonoBehaviour
    {
        private const int ThrowSampleCapacity = 8;
        private const float ThrowSampleWindow = 0.14f;
        private const float ThrowDeadZone = 0.65f;
        private const float StopSpeed = 0.06f;
        private const float CollisionSkin = 0.015f;
        private const float XpPlayerHitRadius = 0.25f;
        private static readonly Collider2D[] HitBuffer = new Collider2D[24];
        private static readonly RaycastHit2D[] CastBuffer = new RaycastHit2D[16];
        private static readonly ContactFilter2D HitFilter =
            ContactFilter2D.noFilter;
        private static readonly ContactFilter2D SolidFilter = CreateSolidFilter();
        private readonly List<SpriteRenderer> highlightedRenderers = new();
        private readonly List<Color> highlightedColors = new();
        private readonly List<FlightState> flights = new();
        private readonly Vector2[] cursorSamplePositions =
            new Vector2[ThrowSampleCapacity];
        private readonly float[] cursorSampleTimes =
            new float[ThrowSampleCapacity];
        private OrbitalStationRuntime station;
        private Component hovered;
        private Component held;
        private Vector2 pullVelocity;
        private int cursorSampleCount;
        private LineRenderer line;

        public bool IsHolding => held != null;

        public void Bind(OrbitalStationRuntime runtime)
        {
            CancelInteraction();
            StopAllFlights();
            station = runtime;
        }

        private void Update()
        {
            if (!CanInteract())
            {
                if (held != null || hovered != null || line != null ||
                    highlightedRenderers.Count > 0)
                {
                    bool orbitalOwnsInput = station != null &&
                        (station.IsRelocating || station.IsDebugPlacementActive ||
                         station.RewardFlow?.PendingReward != null);
                    CancelInteraction(!orbitalOwnsInput);
                }
                StopAllFlights();
                return;
            }

            UpdateFlights();
            if (held != null)
            {
                UpdateHeld();
                return;
            }
            UpdateHover();
        }

        private bool CanInteract()
        {
            if (station == null || !station.IsInitialized || Camera.main == null ||
                Time.timeScale <= 0f || station.Owner == null ||
                station.Owner.IsDead || station.IsRelocating ||
                station.IsDebugPlacementActive ||
                station.RewardFlow?.PendingReward != null ||
                Subject42DebugMenu.IsDebugMenuOpen)
                return false;
            PlayerHealth health = station.GetComponent<PlayerHealth>();
            return health == null || !health.IsDead;
        }

        private void UpdateHover()
        {
            Component next = PointerOverInteractiveUi() ? null :
                FindTarget(MouseWorld());
            if (next != hovered)
            {
                RestoreHighlight();
                hovered = next;
                if (hovered != null)
                {
                    ApplyHighlight(hovered);
                    station.Interaction?.ShowHint(string.Empty,
                        "ПКМ — ТЕЛЕКИНЕЗ");
                    station.Interaction?.SetCursor(OrbitalCursorState.Grabbable);
                }
                else
                {
                    station.Interaction?.ClearHint();
                    station.Interaction?.SetCursor(OrbitalCursorState.Normal);
                }
            }
            if (hovered != null && Input.GetMouseButtonDown(1))
                BeginHold(hovered);
        }

        private void BeginHold(Component target)
        {
            RemoveFlight(target);
            held = target;
            hovered = null;
            pullVelocity = Vector2.zero;
            ResetCursorSamples(MouseWorld());
            EnsureLine();
            station.Interaction?.ClearHint();
            station.Interaction?.SetCursor(OrbitalCursorState.Dragging);
        }

        private void UpdateHeld()
        {
            if (!IsValid(held))
            {
                CancelInteraction();
                return;
            }
            if (!Input.GetMouseButton(1))
            {
                ReleaseHeld();
                return;
            }
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            Vector2 core = station.transform.position;
            Vector2 desired = MouseWorld();
            Vector2 offset = desired - core;
            if (offset.sqrMagnitude > config.TelekinesisGrabRange *
                config.TelekinesisGrabRange)
                desired = core + offset.normalized * config.TelekinesisGrabRange;
            RecordCursorSample(desired);
            Transform target = held.transform;
            target.position = Vector2.SmoothDamp(target.position, desired,
                ref pullVelocity, config.TelekinesisFollowSmoothness,
                config.TelekinesisPullSpeed, Time.deltaTime);
            if (line != null)
            {
                line.SetPosition(0, core);
                line.SetPosition(1, target.position);
            }
        }

        private void ReleaseHeld()
        {
            Component released = held;
            Vector2 velocity = CalculateThrowVelocity();
            ClearHeldVisuals(true);

            if (IsValid(released) && velocity.sqrMagnitude > 0f)
            {
                flights.Add(new FlightState(
                    released,
                    velocity,
                    released is WorldBreakable));
            }
        }

        private void UpdateFlights()
        {
            if (flights.Count == 0)
                return;

            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            float deltaTime = Time.deltaTime;
            Transform player = station != null && station.Owner != null
                ? station.transform
                : null;
            Physics2D.SyncTransforms();

            for (int i = flights.Count - 1; i >= 0; i--)
            {
                FlightState flight = flights[i];
                if (!IsValid(flight.Target))
                {
                    flights.RemoveAt(i);
                    continue;
                }

                Vector2 start = flight.Target.transform.position;
                Vector2 delta = flight.Velocity * deltaTime;
                Vector2 destination = start + delta;

                if (flight.Target is ExperiencePickup && player != null &&
                    SegmentPassesNear(start, destination, player.position,
                        XpPlayerHitRadius))
                {
                    destination = player.position;
                    flight.Velocity = Vector2.zero;
                }
                else if (flight.IsBreakable && delta.sqrMagnitude > 0f &&
                    TryFindSolidCollision(flight, delta, out float distance))
                {
                    float travel = Mathf.Max(0f, distance - CollisionSkin);
                    destination = start + delta.normalized * travel;
                    flight.Velocity = Vector2.zero;
                }

                flight.Target.transform.position = destination;
                flight.Velocity *= Mathf.Exp(-Mathf.Max(0f,
                    config.TelekinesisThrowDrag) * deltaTime);

                if (flight.Velocity.sqrMagnitude <= StopSpeed * StopSpeed)
                {
                    flights.RemoveAt(i);
                    continue;
                }

                flights[i] = flight;
            }

            Physics2D.SyncTransforms();
        }

        private static bool TryFindSolidCollision(
            FlightState flight,
            Vector2 delta,
            out float distance)
        {
            distance = float.MaxValue;
            Collider2D movingCollider = flight.Collider;
            if (movingCollider == null || !movingCollider.enabled)
                return false;

            int count = movingCollider.Cast(delta.normalized, SolidFilter,
                CastBuffer, delta.magnitude + CollisionSkin);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = CastBuffer[i];
                CastBuffer[i] = default;
                Collider2D collider = hit.collider;
                if (collider == null ||
                    collider.transform.IsChildOf(flight.Target.transform) ||
                    collider.GetComponentInParent<PlayerHealth>() != null)
                    continue;

                if (hit.distance < distance)
                {
                    distance = hit.distance;
                    found = true;
                }
            }
            return found;
        }

        private static bool SegmentPassesNear(
            Vector2 start,
            Vector2 end,
            Vector2 point,
            float radius)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(point - start, segment) /
                    lengthSquared)
                : 0f;
            return ((start + segment * t) - point).sqrMagnitude <=
                radius * radius;
        }

        private void ResetCursorSamples(Vector2 position)
        {
            cursorSampleCount = 0;
            RecordCursorSample(position, true);
        }

        private void RecordCursorSample(Vector2 position, bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && cursorSampleCount > 0 &&
                now - cursorSampleTimes[cursorSampleCount - 1] < 0.006f)
                return;

            if (cursorSampleCount == ThrowSampleCapacity)
            {
                for (int i = 1; i < ThrowSampleCapacity; i++)
                {
                    cursorSamplePositions[i - 1] = cursorSamplePositions[i];
                    cursorSampleTimes[i - 1] = cursorSampleTimes[i];
                }
                cursorSampleCount--;
            }

            cursorSamplePositions[cursorSampleCount] = position;
            cursorSampleTimes[cursorSampleCount] = now;
            cursorSampleCount++;
        }

        private Vector2 CalculateThrowVelocity()
        {
            RecordCursorSample(MouseWorld(), true);
            if (cursorSampleCount < 2)
                return Vector2.zero;

            float newestTime = cursorSampleTimes[cursorSampleCount - 1];
            int first = cursorSampleCount - 1;
            while (first > 0 && newestTime - cursorSampleTimes[first - 1] <=
                ThrowSampleWindow)
                first--;
            if (cursorSampleCount - first < 2)
                return Vector2.zero;

            float meanTime = 0f;
            Vector2 meanPosition = Vector2.zero;
            int count = cursorSampleCount - first;
            for (int i = first; i < cursorSampleCount; i++)
            {
                meanTime += cursorSampleTimes[i];
                meanPosition += cursorSamplePositions[i];
            }
            meanTime /= count;
            meanPosition /= count;

            float timeVariance = 0f;
            Vector2 covariance = Vector2.zero;
            for (int i = first; i < cursorSampleCount; i++)
            {
                float timeOffset = cursorSampleTimes[i] - meanTime;
                timeVariance += timeOffset * timeOffset;
                covariance += (cursorSamplePositions[i] - meanPosition) *
                    timeOffset;
            }
            if (timeVariance <= 0.000001f)
                return Vector2.zero;

            Vector2 cursorVelocity = covariance / timeVariance;
            if (cursorVelocity.magnitude < ThrowDeadZone)
                return Vector2.zero;

            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            return Vector2.ClampMagnitude(cursorVelocity *
                Mathf.Max(0f, config.TelekinesisThrowStrength),
                Mathf.Max(0f, config.TelekinesisMaxThrowSpeed));
        }

        private void RemoveFlight(Component target)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
                if (flights[i].Target == target)
                    flights.RemoveAt(i);
        }

        private void StopAllFlights() => flights.Clear();

        private Component FindTarget(Vector2 point)
        {
            int count = Physics2D.OverlapCircle(point, 0.32f, HitFilter, HitBuffer);
            Component best = null;
            float bestDistance = float.MaxValue;
            float range = OrbitalPresentationConfig.Active.TelekinesisGrabRange;
            Vector2 core = station.transform.position;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = HitBuffer[i];
                HitBuffer[i] = null;
                if (hit == null)
                    continue;
                Component candidate = GetValidTarget(hit);
                if (candidate == null ||
                    Vector2.Distance(core, candidate.transform.position) > range)
                    continue;
                float distance = ((Vector2)candidate.transform.position - point)
                    .sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private static Component GetValidTarget(Collider2D hit)
        {
            ExperiencePickup xp = hit.GetComponentInParent<ExperiencePickup>();
            if (xp != null && xp.isActiveAndEnabled)
                return xp;
            WorldBreakable breakable = hit.GetComponentInParent<WorldBreakable>();
            if (breakable == null || !breakable.isActiveAndEnabled ||
                breakable.IsBroken || breakable.LootProfile != null &&
                breakable.LootProfile.RewardKind ==
                    WorldBreakableRewardKind.WorldEventUpgradeChoices)
                return null;
            return breakable;
        }

        private static bool IsValid(Component target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;
            if (target is ExperiencePickup xp)
                return xp.isActiveAndEnabled;
            if (target is WorldBreakable breakable)
                return breakable.isActiveAndEnabled && !breakable.IsBroken &&
                    (breakable.LootProfile == null ||
                     breakable.LootProfile.RewardKind !=
                        WorldBreakableRewardKind.WorldEventUpgradeChoices);
            return false;
        }

        private void ApplyHighlight(Component target)
        {
            SpriteRenderer[] renderers =
                target.GetComponentsInChildren<SpriteRenderer>(true);
            Color cyan = new(0.2f, 1f, 0.92f, 1f);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                    continue;
                highlightedRenderers.Add(renderer);
                highlightedColors.Add(renderer.color);
                Color tint = Color.Lerp(renderer.color, cyan, 0.58f);
                tint.a = renderer.color.a;
                renderer.color = tint;
            }
        }

        private void RestoreHighlight()
        {
            for (int i = 0; i < highlightedRenderers.Count; i++)
                if (highlightedRenderers[i] != null)
                    highlightedRenderers[i].color = highlightedColors[i];
            highlightedRenderers.Clear();
            highlightedColors.Clear();
        }

        private void EnsureLine()
        {
            if (line != null)
                return;
            GameObject visual = new("Orbital Telekinesis Line");
            visual.transform.SetParent(station.RuntimeRoot, true);
            line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.028f;
            line.sharedMaterial = station.VisualMaterial;
            line.sortingLayerName = "Player";
            line.sortingOrder = 12;
            Color color = new(0.2f, 1f, 0.92f, 0.72f);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.25f);
        }

        public void CancelInteraction(bool clearUi = true)
        {
            ClearHeldVisuals(clearUi);
            hovered = null;
            RestoreHighlight();
            if (clearUi)
            {
                station?.Interaction?.ClearHint();
                station?.Interaction?.SetCursor(OrbitalCursorState.Normal);
            }
        }

        private void ClearHeldVisuals(bool clearUi)
        {
            held = null;
            pullVelocity = Vector2.zero;
            cursorSampleCount = 0;
            RestoreHighlight();
            if (line != null)
                Destroy(line.gameObject);
            line = null;
            if (clearUi)
            {
                station?.Interaction?.ClearHint();
                station?.Interaction?.SetCursor(OrbitalCursorState.Normal);
            }
        }

        private static ContactFilter2D CreateSolidFilter()
        {
            ContactFilter2D filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            return filter;
        }

        private struct FlightState
        {
            public readonly Component Target;
            public readonly bool IsBreakable;
            public readonly Collider2D Collider;
            public Vector2 Velocity;

            public FlightState(
                Component target,
                Vector2 velocity,
                bool isBreakable)
            {
                Target = target;
                Velocity = velocity;
                IsBreakable = isBreakable;
                Collider = target != null
                    ? target.GetComponentInChildren<Collider2D>()
                    : null;
            }
        }

        private static Vector2 MouseWorld()
        {
            Vector3 point = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(point.x, point.y);
        }

        private static bool PointerOverInteractiveUi()
        {
            if (EventSystem.current == null)
                return false;
            PointerEventData pointer = new(EventSystem.current)
            {
                position = Input.mousePosition
            };
            List<RaycastResult> hits = new();
            EventSystem.current.RaycastAll(pointer, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                GameObject hit = hits[i].gameObject;
                if (hit != null && (hit.GetComponentInParent<Selectable>() != null ||
                    hit.GetComponentInParent<ScrollRect>() != null))
                    return true;
            }
            return false;
        }

        private void OnDisable() => CancelInteraction();
        private void OnDestroy() => CancelInteraction();
    }
}
