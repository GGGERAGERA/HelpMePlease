using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    // Shared with the completed lab: only runtime mount phases change, never the loadout/model.
    public sealed class OrbitalAnomalyProjection
    {
        readonly OrbitalStationRuntime station;
        readonly Func<OrbitalMountRuntime, bool> available;
        readonly Dictionary<OrbitalMountRuntime, float> original = new(), shuffled = new();
        public OrbitalAnomalyProjection(OrbitalStationRuntime station, Func<OrbitalMountRuntime, bool> available)
        { this.station = station; this.available = available; }
        public float OriginalPhase(OrbitalMountRuntime mount) => original.TryGetValue(mount,out float phase) ? phase : mount.LocalPhase;
        public void Capture()
        {
            foreach (var ring in station.Rings)
                if (ring.IsActive) foreach (var mount in ring.Mounts) original[mount] = mount.LocalPhase;
        }
        public void Shuffle(System.Random random)
        {
            foreach (var ring in station.Rings)
            {
                if (!ring.IsActive) continue;
                var mounts = ring.Mounts.Where(m => available(m) && original.ContainsKey(m)).ToArray();
                if (mounts.Length < 2) continue;
                var targets = mounts.Select(m => original[m]).ToArray();
                for (int i = targets.Length - 1; i > 0; i--)
                { int j = random.Next(i + 1); (targets[i],targets[j]) = (targets[j],targets[i]); }
                if (mounts.Where(m => m.Occupied).All(m => Mathf.Approximately(targets[Array.IndexOf(mounts,m)],
                    shuffled.TryGetValue(m,out float phase) ? phase : original[m])))
                { float first = targets[0]; Array.Copy(targets,1,targets,0,targets.Length - 1); targets[^1] = first; }
                for (int i = 0; i < mounts.Length; i++) shuffled[mounts[i]] = targets[i];
            }
        }
        public void Apply(float elapsed, bool interference, float levelDeviation = 12f, float weaponDeviation = 5f,
            float levelPeriod = 6f, float weaponPeriod = 2.3f, float fadeIn = .5f)
        {
            foreach (var pair in original)
            {
                var mount = pair.Key;
                float phase = shuffled.TryGetValue(mount,out float target) ? target : pair.Value;
                if (interference)
                {
                    float envelope = Mathf.Clamp01(elapsed / Mathf.Max(.01f,fadeIn));
                    phase += envelope * levelDeviation * Mathf.Sin(elapsed * 2f * Mathf.PI / Mathf.Max(.1f,levelPeriod) + mount.Ring.State.Order * 2.0944f);
                    if (mount.Module != null)
                        phase += envelope * weaponDeviation * Mathf.Sin(elapsed * 2f * Mathf.PI / Mathf.Max(.1f,weaponPeriod) + mount.Module.StableModuleId * 1.7f);
                }
                mount.SetLocalPhase(phase);
            }
            RefreshMounts();
        }
        public void Restore()
        {
            bool changed = original.Count > 0;
            foreach (var pair in original) pair.Key.SetLocalPhase(pair.Value);
            original.Clear(); shuffled.Clear();
            if (changed) RefreshMounts();
        }
        void RefreshMounts()
        {
            if (station != null && station.IsInitialized)
                foreach (var ring in station.Rings) ring.Tick(0f);
        }
    }
}
