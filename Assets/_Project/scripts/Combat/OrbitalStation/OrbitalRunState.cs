using System;
using System.Collections.Generic;
using System.Linq;

namespace Subject42.Combat.OrbitalStation
{
    [Serializable]
    public sealed class OrbitalCoreState
    {
        public int Level;
        public float DamageMultiplier = 1f;
        public float CooldownMultiplier = 1f;
        public int PulseUpgradeLevel;
        public int CascadeUpgradeLevel;
        public int LinkMatrixUpgradeLevel;
    }

    [Serializable]
    public sealed class OrbitalRingState
    {
        public int StableRingId;
        public int Order;
        public float Radius;
        public float BaseRotationSpeed;
        public int Direction;
        public float CurrentPhase;
        public float PhaseOffset;
        public int MountCapacity;
        public float PowerMultiplier = 1f;
        public int SpeedUpgradeLevel;
        public int PowerUpgradeLevel;
        public int MountUpgradeLevel;
        public int VisualUpgradeLevel;
    }

    [Serializable]
    public sealed class OrbitalModuleState
    {
        public int StableModuleId;
        public OrbitalModuleKind ModuleType;
        public int StableRingId;
        public int MountIndex;
    }

    [Serializable]
    public sealed class OrbitalRunState
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public bool IsInitialized;
        public int RunId;
        public OrbitalCoreState CoreState = new();
        public List<OrbitalRingState> Rings = new();
        public List<OrbitalModuleState> Modules = new();
        public int NextStableRingId = 1;
        public int NextStableModuleId = 1;
        public int Revision;
        public int RestoreCount;
        public int LastProcessedPlayerLevel = 1;

        public static OrbitalRunState CreateDefault(int runId)
        {
            OrbitalRunState state = new()
            {
                IsInitialized = true,
                RunId = runId
            };
            OrbitalRingState ring = state.AddRing();
            state.InstallModule(OrbitalModuleKind.Pistol,
                ring.StableRingId, 0, out _);
            return state;
        }

        public OrbitalRingState AddRing()
        {
            int order = Rings.Count;
            OrbitalRingState ring = new()
            {
                StableRingId = NextStableRingId++,
                Order = order,
                Radius = 1.25f + order * 0.72f,
                BaseRotationSpeed = 42f / (1f + order * 0.16f),
                Direction = order % 2 == 0 ? 1 : -1,
                CurrentPhase = order * 23f,
                PhaseOffset = order * 23f,
                MountCapacity = 3
            };
            Rings.Add(ring);
            Revision++;
            return ring;
        }

        public bool RemoveRing(int stableRingId, out string error)
        {
            OrbitalRingState ring = FindRing(stableRingId);
            if (ring == null)
            {
                error = $"ring {stableRingId} does not exist";
                return false;
            }
            Modules.RemoveAll(module => module.StableRingId == stableRingId);
            Rings.Remove(ring);
            ReorderRings();
            Revision++;
            error = null;
            return true;
        }

        public bool AddMount(int stableRingId, out string error)
        {
            OrbitalRingState ring = FindRing(stableRingId);
            if (ring == null)
            {
                error = $"ring {stableRingId} does not exist";
                return false;
            }
            if (ring.MountCapacity >=
                OrbitalProgressionConfig.Default.MaxMountsPerRing)
            {
                error = $"ring {stableRingId} reached mount capacity limit";
                return false;
            }
            ring.MountCapacity++;
            ring.MountUpgradeLevel++;
            ring.VisualUpgradeLevel++;
            Revision++;
            error = null;
            return true;
        }

        public bool InstallModule(OrbitalModuleKind type, int stableRingId,
            int mountIndex, out OrbitalModuleState module)
        {
            module = null;
            if (!CanOccupy(stableRingId, mountIndex, 0, out _))
                return false;
            module = new OrbitalModuleState
            {
                StableModuleId = NextStableModuleId++,
                ModuleType = type,
                StableRingId = stableRingId,
                MountIndex = mountIndex
            };
            Modules.Add(module);
            Revision++;
            return true;
        }

        public bool MoveModule(int stableModuleId, int targetRingId,
            int targetMountIndex, out string error)
        {
            OrbitalModuleState module = Modules.Find(value =>
                value.StableModuleId == stableModuleId);
            if (module == null)
            {
                error = $"module {stableModuleId} does not exist";
                return false;
            }
            if (!CanOccupy(targetRingId, targetMountIndex, stableModuleId, out error))
                return false;
            module.StableRingId = targetRingId;
            module.MountIndex = targetMountIndex;
            Revision++;
            return true;
        }

        public bool RemoveModule(int stableModuleId)
        {
            int removed = Modules.RemoveAll(value =>
                value.StableModuleId == stableModuleId);
            if (removed > 0)
                Revision++;
            return removed > 0;
        }

        public bool UpgradeRingSpeed(int stableRingId)
        {
            OrbitalRingState ring = FindRing(stableRingId);
            if (ring == null)
                return false;
            if (ring.SpeedUpgradeLevel >=
                OrbitalProgressionConfig.Default.MaxSpeedUpgradeLevel)
                return false;
            ring.SpeedUpgradeLevel++;
            ring.VisualUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool UpgradeRingPower(int stableRingId)
        {
            OrbitalRingState ring = FindRing(stableRingId);
            if (ring == null)
                return false;
            if (ring.PowerUpgradeLevel >=
                OrbitalProgressionConfig.Default.MaxPowerUpgradeLevel)
                return false;
            ring.PowerUpgradeLevel++;
            ring.PowerMultiplier *=
                1f + OrbitalProgressionConfig.Default.PowerIncrement;
            ring.VisualUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool UpgradeCore()
        {
            if (CoreState.Level >= OrbitalProgressionConfig.Default.MaxCoreLevel)
                return false;
            CoreState.Level++;
            CoreState.DamageMultiplier = 1f + CoreState.Level * 0.12f;
            CoreState.CooldownMultiplier = 1f / (1f + CoreState.Level * 0.08f);
            CoreState.PulseUpgradeLevel++;
            CoreState.CascadeUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool UpgradeLinkMatrix()
        {
            if (CoreState.LinkMatrixUpgradeLevel >=
                OrbitalProgressionConfig.Default.MaxLinkMatrixLevel ||
                Modules.Count(value =>
                    value.ModuleType == OrbitalModuleKind.LinkNode) < 2)
                return false;
            CoreState.LinkMatrixUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool MarkPlayerLevelProcessed(int playerLevel)
        {
            if (playerLevel <= LastProcessedPlayerLevel)
                return false;
            LastProcessedPlayerLevel = playerLevel;
            Revision++;
            return true;
        }

        public void SetPhase(int stableRingId, float phase)
        {
            OrbitalRingState ring = FindRing(stableRingId);
            if (ring != null)
                ring.CurrentPhase = phase;
        }

        public void MarkRestored()
        {
            RestoreCount++;
            Revision++;
        }

        public OrbitalRingState FindRing(int stableRingId) => Rings.Find(value =>
            value.StableRingId == stableRingId);

        public bool Validate(out string error)
        {
            if (Version != CurrentVersion)
            {
                error = $"unsupported version {Version}";
                return false;
            }
            if (!IsInitialized)
            {
                error = "state is not initialized";
                return false;
            }
            if (CoreState == null || CoreState.Level < 0 ||
                !IsFinitePositive(CoreState.DamageMultiplier) ||
                !IsFinitePositive(CoreState.CooldownMultiplier))
            {
                error = "invalid Core state";
                return false;
            }
            if (Rings == null || Modules == null)
            {
                error = "ring/module collection is null";
                return false;
            }
            if (Rings.Select(value => value.StableRingId).Distinct().Count() != Rings.Count)
            {
                error = "duplicate ring ID";
                return false;
            }
            if (Modules.Select(value => value.StableModuleId).Distinct().Count() != Modules.Count)
            {
                error = "duplicate module ID";
                return false;
            }
            HashSet<(int ring, int mount)> occupied = new();
            for (int i = 0; i < Rings.Count; i++)
            {
                OrbitalRingState ring = Rings[i];
                if (ring == null || ring.StableRingId <= 0 || ring.Order != i ||
                    ring.MountCapacity < 1 || ring.MountCapacity >
                        OrbitalProgressionConfig.Default.MaxMountsPerRing ||
                    !IsFinitePositive(ring.Radius) ||
                    !IsFiniteNonNegative(ring.BaseRotationSpeed) ||
                    float.IsNaN(ring.CurrentPhase) || float.IsInfinity(ring.CurrentPhase) ||
                    (ring.Direction != 1 && ring.Direction != -1) ||
                    !IsFinitePositive(ring.PowerMultiplier))
                {
                    error = $"invalid ring at order {i}";
                    return false;
                }
            }
            for (int i = 0; i < Modules.Count; i++)
            {
                OrbitalModuleState module = Modules[i];
                if (module == null || module.StableModuleId <= 0 ||
                    !Enum.IsDefined(typeof(OrbitalModuleKind),
                        module.ModuleType))
                {
                    error = $"unknown module type at index {i}";
                    return false;
                }
                OrbitalRingState ring = FindRing(module.StableRingId);
                if (ring == null)
                {
                    error = $"module {module.StableModuleId} references missing ring {module.StableRingId}";
                    return false;
                }
                if (module.MountIndex < 0 || module.MountIndex >= ring.MountCapacity)
                {
                    error = $"module {module.StableModuleId} mount {module.MountIndex} outside capacity {ring.MountCapacity}";
                    return false;
                }
                if (!occupied.Add((module.StableRingId, module.MountIndex)))
                {
                    error = $"duplicate mount occupancy {module.StableRingId}:{module.MountIndex}";
                    return false;
                }
            }
            int highestRingId = Rings.Count == 0 ? 0 :
                Rings.Max(value => value.StableRingId);
            int highestModuleId = Modules.Count == 0 ? 0 :
                Modules.Max(value => value.StableModuleId);
            if (NextStableRingId <= highestRingId ||
                NextStableModuleId <= highestModuleId)
            {
                error = "stable ID allocator is behind existing IDs";
                return false;
            }
            error = "OK";
            return true;
        }

        public string ToCompactString(int currentSector)
        {
            string rings = string.Join(",", Rings.OrderBy(value => value.Order)
                .Select(value =>
                    $"R{value.StableRingId}[m{value.MountCapacity},s{value.SpeedUpgradeLevel},p{value.PowerUpgradeLevel},a{value.CurrentPhase:0.0}]"));
            string modules = string.Join(",", Modules.OrderBy(value => value.StableModuleId)
                .Select(value =>
                    $"M{value.StableModuleId}:{value.ModuleType}@R{value.StableRingId}.{value.MountIndex}"));
            return $"ORBITAL_STATE v={Version} run={RunId} rev={Revision} sector={currentSector} playerLevel={LastProcessedPlayerLevel} core={CoreState.Level} restore={RestoreCount} rings=[{rings}] modules=[{modules}]";
        }

        private bool CanOccupy(int ringId, int mountIndex, int ignoredModuleId,
            out string error)
        {
            OrbitalRingState ring = FindRing(ringId);
            if (ring == null)
            {
                error = $"ring {ringId} does not exist";
                return false;
            }
            if (mountIndex < 0 || mountIndex >= ring.MountCapacity)
            {
                error = $"mount {mountIndex} outside ring {ringId} capacity";
                return false;
            }
            if (Modules.Any(value => value.StableModuleId != ignoredModuleId &&
                    value.StableRingId == ringId && value.MountIndex == mountIndex))
            {
                error = $"mount {ringId}:{mountIndex} is occupied";
                return false;
            }
            error = null;
            return true;
        }

        private void ReorderRings()
        {
            for (int i = 0; i < Rings.Count; i++)
                Rings[i].Order = i;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFiniteNonNegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
