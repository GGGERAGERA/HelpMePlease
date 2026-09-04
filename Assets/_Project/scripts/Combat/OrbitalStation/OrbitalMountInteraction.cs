using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    /// <summary>
    /// Shared screen-space mount selection and presentation for rewards and relocation.
    /// </summary>
    internal sealed class OrbitalMountHoverResolver
    {
        public OrbitalMountRuntime Resolve(OrbitalStationRuntime station,
            Camera camera, Vector2 pointerScreen, OrbitalMountRuntime current)
        {
            if (station == null || camera == null)
                return null;
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            float radius = config.MountSelectionRadiusPixels;
            OrbitalMountRuntime best = null;
            float bestDistance = float.MaxValue;
            int bestRank = int.MinValue;
            for (int r = 0; r < station.Rings.Count; r++)
            {
                OrbitalRingRuntime ring = station.Rings[r];
                for (int m = 0; m < ring.Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = ring.Mounts[m];
                    if (mount.Transform == null)
                        continue;
                    Vector3 screen = camera.WorldToScreenPoint(mount.Transform.position);
                    if (screen.z < 0f)
                        continue;
                    float distance = Vector2.Distance(pointerScreen,
                        new Vector2(screen.x, screen.y));
                    int rank = r * 1000 + m;
                    if (distance < bestDistance - 0.01f ||
                        (Mathf.Abs(distance - bestDistance) <= 0.01f &&
                         rank > bestRank))
                    {
                        best = mount;
                        bestDistance = distance;
                        bestRank = rank;
                    }
                }
            }

            if (current != null && current.Transform != null)
            {
                Vector3 currentScreen = camera.WorldToScreenPoint(
                    current.Transform.position);
                float currentDistance = Vector2.Distance(pointerScreen,
                    new Vector2(currentScreen.x, currentScreen.y));
                bool insideStickyRadius = currentDistance <= radius +
                    config.MountHoverHysteresisPixels;
                bool challengerClearlyCloser = best != null && best != current &&
                    bestDistance + config.MountSwitchAdvantagePixels < currentDistance;
                if (insideStickyRadius && !challengerClearlyCloser)
                    return current;
            }
            return bestDistance <= radius ? best : null;
        }
    }

    internal static class OrbitalMountInteractionPresentation
    {
        public static void Apply(OrbitalStationRuntime station,
            OrbitalMountRuntime hovered, OrbitalMountRuntime reservedSource = null)
        {
            if (station == null)
                return;
            OrbitalRingRuntime hoveredRing = hovered?.Ring;
            for (int r = 0; r < station.Rings.Count; r++)
            {
                OrbitalRingRuntime ring = station.Rings[r];
                bool isHoveredRing = ring == hoveredRing;
                ring.SetSelected(false);
                ring.SetInteractionState(isHoveredRing, isHoveredRing,
                    hoveredRing != null && !isHoveredRing);
                for (int m = 0; m < ring.Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = ring.Mounts[m];
                    OrbitalMountRuntime.VisualState state;
                    if (mount == hovered)
                        state = !station.IsMountFree(mount) || mount == reservedSource
                            ? OrbitalMountRuntime.VisualState.Invalid
                            : OrbitalMountRuntime.VisualState.ValidHover;
                    else if (!station.IsMountFree(mount) || mount == reservedSource)
                        state = OrbitalMountRuntime.VisualState.Occupied;
                    else
                        state = OrbitalMountRuntime.VisualState.Valid;
                    mount.SetVisualState(state);
                }
            }
        }

        public static void Clear(OrbitalStationRuntime station)
        {
            if (station == null)
                return;
            for (int r = 0; r < station.Rings.Count; r++)
            {
                OrbitalRingRuntime ring = station.Rings[r];
                ring.SetSelected(false);
                ring.SetInteractionState(false, false);
                for (int m = 0; m < ring.Mounts.Count; m++)
                {
                    OrbitalMountRuntime mount = ring.Mounts[m];
                    mount.SetVisualState(!station.IsMountFree(mount)
                        ? OrbitalMountRuntime.VisualState.Occupied
                        : OrbitalMountRuntime.VisualState.Normal);
                }
            }
        }
    }
}
