#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

[CreateAssetMenu(menuName = "Subject42/Development/Trailer Capture", fileName = "TrailerCapturePresets")]
public sealed class TrailerCaptureConfig : ScriptableObject
{
    public enum Shot { Beginning, Developed, Intense, RocketStorm, Boss }
    [Serializable] public struct Module
    {
        public int ring;
        public int mount;
        public OrbitalModuleKind kind;
    }
    [Serializable] public sealed class Preset
    {
        public Shot shot;
        public string label;
        public string description;
        [Range(1, 4)] public int rings = 1;
        [Range(1, 3)] public int mountsPerRing = 3;
        [Range(0, 2)] public int coreLevel;
        [Range(0, 60)] public float threat;
        [Range(0, 10)] public int openingEnemies;
        public Module[] modules = Array.Empty<Module>();

        // Validate the whole candidate before replacing the live run. No partial grants.
        public bool TryBuild(int runId, out OrbitalRunState state, out string error)
        {
            state = null;
            error = "Пресет должен содержать 1–4 кольца и 1–3 mounts на кольцо";
            if (rings < 1 || rings > 4 || mountsPerRing < 1 || mountsPerRing > 3 ||
                coreLevel < 0 || coreLevel > 2 || !float.IsFinite(threat) || threat < 0 || threat > 60) return false;
            var candidate = OrbitalRunState.CreateDefault(runId);
            while (candidate.Rings.Count < rings)
                if (!candidate.TryAddRing(out _, out error)) return false;
            foreach (var ring in candidate.Rings)
                while (ring.MountCount < mountsPerRing)
                    if (!candidate.TryAddMount(ring.StableRingId, out error)) return false;
            Module? firstLink = null;
            foreach (var module in modules ?? Array.Empty<Module>())
            {
                if (module.ring < 0 || module.ring >= rings)
                { error = "Модуль ссылается на отсутствующее кольцо"; return false; }
                int id = candidate.Rings[module.ring].StableRingId;
                if (module.kind == OrbitalModuleKind.LinkNode)
                {
                    if (!firstLink.HasValue) firstLink = module;
                    else
                    {
                        var first = firstLink.Value;
                        if (!candidate.TryInstallLinkPair(candidate.Rings[first.ring].StableRingId, first.mount,
                                id, module.mount, out _, out _, out error)) return false;
                        firstLink = null;
                    }
                }
                else if (!candidate.TryInstallModule(module.kind, id, module.mount, out _, out error)) return false;
            }
            if (firstLink.HasValue) { error = "Для Link Node нужна пара"; return false; }
            for (int i = 0; i < coreLevel; i++)
                if (!candidate.TryUpgradeCore(out error)) return false;
            if (!candidate.Validate(out error)) return false;
            state = candidate;
            return true;
        }
    }
    public RocketStormEvent rocketStormPrefab;
    public WorldRuleData[] worldRules = Array.Empty<WorldRuleData>();
    public Preset[] presets = Array.Empty<Preset>();
}
#endif
