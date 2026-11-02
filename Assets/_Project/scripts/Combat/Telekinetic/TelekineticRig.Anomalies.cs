using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        private LocalAnomalyZone zone;
        private float anomalyElapsed, glitchClock;
        private readonly System.Random glitchRandom=new(42031);
        private readonly Dictionary<TelekineticItem,Vector2Int> originalPositions=new();
        public LocalAnomalyType? ActiveAnomaly => zone!=null?zone.AnomalyType:null;
        public bool Suppressed => ActiveAnomaly==LocalAnomalyType.ExplosiveZone;
        public bool GlitchWarning => ActiveAnomaly==LocalAnomalyType.Glitch && glitchClock>=9;
        private void TickAnomaly(float dt)
        {
            var controller=LevelAnomalyController.Instance;
            controller?.UseSegmentedOrbitAnomalies();
            var next=controller!=null && controller.isActiveAndEnabled ? controller.FindOrbitZone(station.Owner.Transform.position) : null;
            if (!ReferenceEquals(zone,next))
            {
                ResetAnomaly(); zone=next;
                if (ActiveAnomaly==LocalAnomalyType.Glitch)
                    foreach (var item in Build.Items) originalPositions.Add(item,item.Position);
                if (Suppressed) ClearEffectState();
            }
            anomalyElapsed+=Mathf.Max(0,dt);
            if (ActiveAnomaly!=LocalAnomalyType.Glitch) return;
            glitchClock+=Mathf.Max(0,dt);
            if (glitchClock<10) return;
            glitchClock%=10;
            if (Build.TryGlitch(glitchRandom)) { InvalidateCharges(); SyncViews(); }
        }
        public void ResetAnomaly()
        {
            if (originalPositions.Count>0)
            {
                foreach (var pair in originalPositions) pair.Key.Position=pair.Value;
                originalPositions.Clear(); InvalidateCharges(); SyncViews();
            }
            zone=null; anomalyElapsed=glitchClock=0;
        }
    }
}
