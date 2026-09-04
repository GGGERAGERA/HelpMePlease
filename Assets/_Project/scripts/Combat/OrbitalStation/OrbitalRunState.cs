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
        public int DamageLevel;
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
            if (!CanAddRing(out _)) return null;
            return CommitAddRing();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public OrbitalRingState DebugAddRingBeyondCap()
        {
            if (!CanCommit(out _) || NextStableRingId == int.MaxValue) return null;
            return CommitAddRing();
        }
#endif

        private OrbitalRingState CommitAddRing(bool incrementRevision = true)
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
            if (incrementRevision) Revision++;
            return ring;
        }

        public bool RemoveRing(int stableRingId, out string error)
        {
            if (!CanRemoveRing(stableRingId, out error)) return false;
            OrbitalRingState ring = FindRing(stableRingId);
            Modules.RemoveAll(module => module.StableRingId == stableRingId);
            Rings.Remove(ring);
            ReorderRings();
            Revision++;
            error = null;
            return true;
        }

        public bool AddMount(int stableRingId, out string error)
        {
            if (!CanAddMount(stableRingId, out error)) return false;
            OrbitalRingState ring = FindRing(stableRingId);
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
            if (!CanInstallModule(type, stableRingId, mountIndex, out _))
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

        public bool CanInstallLinkPair(int ringA, int mountA, int ringB, int mountB,
            out string error) => CanInstallModule(OrbitalModuleKind.LinkNode, ringA, mountA, out error) &&
            CanInstallModule(OrbitalModuleKind.LinkNode, ringB, mountB, out error) &&
            Rule(ringA != ringB || mountA != mountB, "Link targets must be distinct", out error) &&
            Rule(NextStableModuleId <= int.MaxValue - 2, "Link ID allocator exhausted", out error);

        public bool InstallLinkPair(int ringA, int mountA, int ringB, int mountB,
            out OrbitalModuleState first, out OrbitalModuleState second, out string error)
        {
            first = second = null;
            if (!CanInstallLinkPair(ringA, mountA, ringB, mountB, out error)) return false;
            first = new OrbitalModuleState { StableModuleId = NextStableModuleId,
                ModuleType = OrbitalModuleKind.LinkNode, StableRingId = ringA, MountIndex = mountA };
            second = new OrbitalModuleState { StableModuleId = NextStableModuleId + 1,
                ModuleType = OrbitalModuleKind.LinkNode, StableRingId = ringB, MountIndex = mountB };
            Modules.AddRange(new[] { first, second });
            NextStableModuleId += 2;
            Revision++;
            return true;
        }

        // Compatibility contract: filtered insertion order, including re-pairing after removal.
        public IEnumerable<(int First, int Second)> ResolveLinkPairs()
        {
            int pending = 0;
            foreach (OrbitalModuleState module in Modules)
            {
                if (module.ModuleType != OrbitalModuleKind.LinkNode) continue;
                if (pending == 0) pending = module.StableModuleId;
                else { yield return (pending, module.StableModuleId); pending = 0; }
            }
        }

        public int FindLinkPartner(int id)
        {
            foreach (var pair in ResolveLinkPairs())
            {
                if (pair.First == id) return pair.Second;
                if (pair.Second == id) return pair.First;
            }
            return 0;
        }

        public bool MoveModule(int stableModuleId, int targetRingId,
            int targetMountIndex, out string error)
        {
            if (!CanMoveModule(stableModuleId, targetRingId, targetMountIndex, out error)) return false;
            OrbitalModuleState module = FindModule(stableModuleId);
            module.StableRingId = targetRingId;
            module.MountIndex = targetMountIndex;
            Revision++;
            return true;
        }

        public bool RemoveModule(int stableModuleId)
        {
            if (!CanCommit(out _) || FindModule(stableModuleId) == null) return false;
            int removed = Modules.RemoveAll(value =>
                value.StableModuleId == stableModuleId);
            if (removed > 0)
                Revision++;
            return removed > 0;
        }

        public bool UpgradeModuleDamage(int stableModuleId)
        {
            if (!CanUpgradeModuleDamage(stableModuleId, out _)) return false;
            OrbitalModuleState module = FindModule(stableModuleId);
            module.DamageLevel++;
            Revision++;
            return true;
        }

        public bool UpgradeRingSpeed(int stableRingId)
        {
            if (!CanUpgradeRingSpeed(stableRingId, out _)) return false;
            OrbitalRingState ring = FindRing(stableRingId);
            ring.SpeedUpgradeLevel++;
            ring.VisualUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool UpgradeRingPower(int stableRingId)
        {
            if (!CanUpgradeRingPower(stableRingId, out _)) return false;
            OrbitalRingState ring = FindRing(stableRingId);
            ring.PowerUpgradeLevel++;
            ring.PowerMultiplier *=
                1f + OrbitalProgressionConfig.Default.PowerIncrement;
            ring.VisualUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool UpgradeCore()
        {
            if (!CanUpgradeCore(out _)) return false;
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
            if (!CanUpgradeLinkMatrix(out _)) return false;
            CoreState.LinkMatrixUpgradeLevel++;
            Revision++;
            return true;
        }

        public bool MarkPlayerLevelProcessed(int playerLevel)
        {
            if (!CanCommit(out _) || playerLevel <= LastProcessedPlayerLevel)
                return false;
            LastProcessedPlayerLevel = playerLevel;
            Revision++;
            return true;
        }

        public bool ProcessPlayerLevelMilestone(int playerLevel, out OrbitalRingState addedRing)
        {
            addedRing = null;
            if (!CanCommit(out _) || playerLevel <= LastProcessedPlayerLevel) return false;
            bool addRing = OrbitalProgressionConfig.Default.IsRingMilestone(playerLevel) &&
                Rings.Count < OrbitalProgressionConfig.Default.MaxNormalRings;
            if (addRing && !CanAddRing(out _)) return false;
            MarkPlayerLevelProcessed(playerLevel);
            if (addRing) addedRing = CommitAddRing(false);
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

        public OrbitalRingState FindRing(int stableRingId) => Rings?.Find(value =>
            value != null && value.StableRingId == stableRingId);

        public OrbitalModuleState FindModule(int stableModuleId) => Modules?.Find(value =>
            value != null && value.StableModuleId == stableModuleId);

        public bool IsMountFree(int ringId, int mountIndex) => CanOccupy(ringId, mountIndex, 0, out _);

        private bool CanCommit(out string error)
        {
            if (!Validate(out error)) return false;
            return Rule(Revision < int.MaxValue, "revision exhausted", out error);
        }

        private static bool Rule(bool allowed, string reason, out string error)
        {
            error = allowed ? null : reason;
            return allowed;
        }

        public bool HasFreeMount(int ringId) => CanRemoveRing(ringId, out _) &&
            Modules.Count(m => m.StableRingId == ringId) < FindRing(ringId).MountCapacity;

        public bool CanAddRing(out string error) => CanCommit(out error) &&
            Rule(Rings.Count < OrbitalProgressionConfig.Default.MaxNormalRings && NextStableRingId < int.MaxValue,
                "ring cap or ID limit reached", out error);

        public bool CanRemoveRing(int id, out string error) => CanCommit(out error) &&
            Rule(FindRing(id) != null, $"ring {id} is missing", out error);

        public bool CanAddMount(int id, out string error) => CanRemoveRing(id, out error) &&
            Rule(FindRing(id).MountCapacity < OrbitalProgressionConfig.Default.MaxMountsPerRing &&
                FindRing(id).MountUpgradeLevel < int.MaxValue && FindRing(id).VisualUpgradeLevel < int.MaxValue,
                $"ring {id} reached mount capacity limit", out error);

        public bool CanInstallModule(OrbitalModuleKind kind, int ringId, int mountIndex, out string error) =>
            CanCommit(out error) && Rule(Enum.IsDefined(typeof(OrbitalModuleKind), kind) && NextStableModuleId < int.MaxValue,
                "unknown module kind or exhausted ID allocator", out error) && CanOccupy(ringId, mountIndex, 0, out error);

        public bool CanMoveModule(int id, int ringId, int mountIndex, out string error) =>
            CanCommit(out error) && Rule(FindModule(id) != null, $"module {id} is missing", out error) &&
            CanOccupy(ringId, mountIndex, id, out error);

        public bool CanUpgradeModuleDamage(int id, out string error) => CanCommit(out error) &&
            Rule(FindModule(id) != null && FindModule(id).ModuleType != OrbitalModuleKind.LinkNode &&
                FindModule(id).DamageLevel < int.MaxValue, $"module {id} cannot upgrade damage", out error);

        public bool CanUpgradeRingSpeed(int id, out string error) => CanRemoveRing(id, out error) &&
            Rule(FindRing(id).SpeedUpgradeLevel < OrbitalProgressionConfig.Default.MaxSpeedUpgradeLevel &&
                FindRing(id).VisualUpgradeLevel < int.MaxValue,
                $"ring {id} reached speed cap", out error);

        public bool CanUpgradeRingPower(int id, out string error) => CanRemoveRing(id, out error) &&
            Rule(FindRing(id).PowerUpgradeLevel < OrbitalProgressionConfig.Default.MaxPowerUpgradeLevel &&
                FindRing(id).VisualUpgradeLevel < int.MaxValue &&
                IsFinitePositive(FindRing(id).PowerMultiplier * (1f + OrbitalProgressionConfig.Default.PowerIncrement)),
                $"ring {id} reached power cap", out error);

        public bool CanUpgradeCore(out string error) => CanCommit(out error) &&
            Rule(CoreState.Level < OrbitalProgressionConfig.Default.MaxCoreLevel &&
                CoreState.PulseUpgradeLevel < int.MaxValue && CoreState.CascadeUpgradeLevel < int.MaxValue, "core cap reached", out error);

        public bool CanUpgradeLinkMatrix(out string error) => CanCommit(out error) &&
            Rule(CoreState.LinkMatrixUpgradeLevel < OrbitalProgressionConfig.Default.MaxLinkMatrixLevel &&
                ResolveLinkPairs().Any(),
                "link matrix cap reached or missing endpoints", out error);

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
                CoreState.Level > OrbitalProgressionConfig.Default.MaxCoreLevel ||
                CoreState.PulseUpgradeLevel < 0 || CoreState.CascadeUpgradeLevel < 0 ||
                CoreState.LinkMatrixUpgradeLevel < 0 ||
                CoreState.LinkMatrixUpgradeLevel > OrbitalProgressionConfig.Default.MaxLinkMatrixLevel ||
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
            HashSet<int> ringIds = new();
            HashSet<int> moduleIds = new();
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
                    !IsFinitePositive(ring.PowerMultiplier) ||
                    float.IsNaN(ring.PhaseOffset) || float.IsInfinity(ring.PhaseOffset) ||
                    ring.SpeedUpgradeLevel < 0 || ring.PowerUpgradeLevel < 0 ||
                    ring.SpeedUpgradeLevel > OrbitalProgressionConfig.Default.MaxSpeedUpgradeLevel ||
                    ring.PowerUpgradeLevel > OrbitalProgressionConfig.Default.MaxPowerUpgradeLevel ||
                    ring.MountUpgradeLevel < 0 || ring.VisualUpgradeLevel < 0)
                {
                    error = $"invalid ring at order {i}";
                    return false;
                }
                if (!ringIds.Add(ring.StableRingId))
                {
                    error = $"duplicate ring ID {ring.StableRingId}";
                    return false;
                }
            }
            for (int i = 0; i < Modules.Count; i++)
            {
                OrbitalModuleState module = Modules[i];
                if (module == null || module.StableModuleId <= 0 ||
                    module.DamageLevel < 0 ||
                    !Enum.IsDefined(typeof(OrbitalModuleKind),
                        module.ModuleType))
                {
                    error = $"invalid module (null, ID, upgrade or type) at index {i}";
                    return false;
                }
                if (!moduleIds.Add(module.StableModuleId))
                {
                    error = $"duplicate module ID {module.StableModuleId}";
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
                    $"M{value.StableModuleId}:{value.ModuleType}[d{value.DamageLevel}]@R{value.StableRingId}.{value.MountIndex}"));
            return $"ORBITAL_STATE v={Version} run={RunId} rev={Revision} sector={currentSector} playerLevel={LastProcessedPlayerLevel} core={CoreState.Level} restore={RestoreCount} rings=[{rings}] modules=[{modules}]";
        }

        private bool CanOccupy(int ringId, int mountIndex, int ignoredModuleId,
            out string error)
        {
            if (!CanCommit(out error)) return false;
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
