using UnityEngine;
using System.Collections.Generic;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalStationRuntime
    {
        OrbitalAnomalyProjection orbitAnomalyProjection;
        LocalAnomalyZone orbitAnomalyZone;
        System.Random orbitAnomalyRandom;
        float orbitAnomalyElapsed, orbitGlitchClock, orbitWarningFlash;
        readonly Dictionary<LineRenderer,float> orbitWarningLines = new();
        public LocalAnomalyType? ActiveOrbitAnomaly => orbitAnomalyZone != null ? orbitAnomalyZone.AnomalyType : null;
        public bool OrbitModulesSuppressed => ActiveOrbitAnomaly == LocalAnomalyType.ExplosiveZone;
        public bool OrbitGlitchWarning => ActiveOrbitAnomaly == LocalAnomalyType.Glitch && orbitGlitchClock >= 9f;
        float OrbitRotationSpeedFactor => ActiveOrbitAnomaly == LocalAnomalyType.Stasis ? .5f : 1f;
        float OrbitRotationDrainFactor => ActiveOrbitAnomaly == LocalAnomalyType.Berserk ? 1.75f : 1f;

        // Runs before Q/E and the ordinary station step. Centre means the player, including when RMB shifts the station.
        void TickOrbitAnomaly(float deltaTime)
        {
            if (!IsInitialized || !UsesSegmentedOrbit || Owner == null || Owner.IsDead || IsRunEnded ||
                InputOwner == null || InputOwner.IsConstructing || Time.timeScale <= 0f || SceneTransitionOverlay.IsTransitioning)
            { ResetOrbitAnomaly(); return; }
            var controller = LevelAnomalyController.Instance;
            if (controller == null || !controller.isActiveAndEnabled) { ResetOrbitAnomaly(); return; }
            controller.UseSegmentedOrbitAnomalies();
            var next = controller.FindOrbitZone(Owner.Transform.position);
            if (!ReferenceEquals(next,orbitAnomalyZone))
            {
                ResetOrbitAnomaly(); orbitAnomalyZone = next;
                if (next != null && (next.AnomalyType == LocalAnomalyType.Glitch || next.AnomalyType == LocalAnomalyType.Gravity))
                {
                    orbitAnomalyProjection = new OrbitalAnomalyProjection(this,m => OrbitalRunState.IsSegmentedWeaponSlot(m.Ring.State.Order,m.MountIndex));
                    orbitAnomalyProjection.Capture();
                }
            }
            orbitAnomalyElapsed += Mathf.Max(0f,deltaTime);
            if (ActiveOrbitAnomaly == LocalAnomalyType.Glitch)
            {
                orbitGlitchClock += Mathf.Max(0f,deltaTime);
                if (orbitGlitchClock >= 10f)
                {
                    orbitGlitchClock %= 10f;
                    orbitAnomalyRandom ??= new System.Random(42031);
                    orbitAnomalyProjection.Shuffle(orbitAnomalyRandom);
                }
                if (OrbitGlitchWarning && orbitAnomalyElapsed >= orbitWarningFlash)
                {
                    orbitWarningFlash = orbitAnomalyElapsed + .12f;
                    int firstWarning = flashes.Count;
                    foreach (var gun in modules)
                    {
                        if (gun.CurrentMount == null || !gun.CurrentMount.Ring.IsActive) continue;
                        Vector2 point = gun.WorldPosition;
                        Color purple = new(1f,.25f,.9f);
                        FlashLink(point + new Vector2(-.32f,.32f),point + new Vector2(.32f,.32f),purple,.13f,OrbitalFlashFamily.Other,2f);
                        FlashLink(point + new Vector2(-.32f,-.32f),point + new Vector2(.32f,-.32f),purple,.13f,OrbitalFlashFamily.Other,2f);
                        FlashLink(point + new Vector2(-.32f,-.32f),point + new Vector2(-.32f,.32f),purple,.13f,OrbitalFlashFamily.Other,2f);
                        FlashLink(point + new Vector2(.32f,-.32f),point + new Vector2(.32f,.32f),purple,.13f,OrbitalFlashFamily.Other,2f);
                    }
                    for (int i = firstWarning; i < flashes.Count; i++) orbitWarningLines[flashes[i].line] = flashes[i].startedAt;
                }
            }
            orbitAnomalyProjection?.Apply(orbitAnomalyElapsed,ActiveOrbitAnomaly == LocalAnomalyType.Gravity);
        }
        internal void ResetOrbitAnomaly()
        {
            // Warning lifetime uses game time; return only our still-owned flashes before Tab freezes it.
            for (int i = flashes.Count - 1; i >= 0; i--)
                if (orbitWarningLines.TryGetValue(flashes[i].line,out float started) && flashes[i].startedAt == started)
                {
                    if (flashes[i].line != null) { flashes[i].line.enabled = false; idleFlashes.Push(flashes[i].line); }
                    flashes.RemoveAt(i);
                }
            orbitWarningLines.Clear();
            orbitAnomalyProjection?.Restore(); orbitAnomalyProjection = null; orbitAnomalyZone = null;
            orbitAnomalyElapsed = orbitGlitchClock = orbitWarningFlash = 0f;
        }
    }
}
