using System;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using RewardRandom = BotRunSeed.RewardRandom;
#else
using RewardRandom = UnityEngine.Random;
#endif
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalRewardKind
    {
        Pistol,
        LaserSword,
        ImpulseGun,
        ArcEmitter,
        LinkPair,
        RingSpeed,
        RingPower,
        AddMount,
        CoreUpgrade,
        LinkMatrix,
        MaxHealth,
        MoveSpeed,
        ModuleDamage,
        NewRing,
        RingCapacity
    }

    public sealed class OrbitalRewardData : UpgradeData
    {
        public OrbitalRewardKind RewardKind;
        public UpgradeData BodyUpgrade;
        public float Weight;
        public bool RequiresArenaSelection;
        internal OrbitalRewardProvider PresentationOwner;
    }

    public sealed class OrbitalRewardProvider : IDisposable
    {
        private readonly OrbitalProgressionConfig config;
        private readonly List<OrbitalRewardData> definitions = new();
        private readonly UpgradeData maxHealthUpgrade;
        private readonly UpgradeData moveSpeedUpgrade;

        public OrbitalRewardProvider(UpgradeData[] legacyUpgrades,
            OrbitalProgressionConfig progressionConfig = null)
        {
            config = progressionConfig ?? OrbitalProgressionConfig.Default;
            maxHealthUpgrade = Find(legacyUpgrades, UpgradeType.MaxHealthFlat);
            moveSpeedUpgrade = Find(legacyUpgrades, UpgradeType.MoveSpeedPercent);
            CreateDefinitions();
        }

        public List<UpgradeData> BuildChoices(int count, bool offerNewRing = false)
            => BuildChoices(count, RunStateManager.Instance?.OrbitalStationState,
                RunStateManager.Instance?.ItemSlots, offerNewRing);

        public List<UpgradeData> BuildChoices(int count, OrbitalRunState state,
            RunItemSlots slots, bool offerNewRing = false)
        {
            RefreshPresentation(state);
            List<OrbitalRewardData> pool = GetEligibleDefinitions(state, slots, demoOnly: true)
                .Where(value => value.RewardKind != OrbitalRewardKind.NewRing)
                .ToList();
            List<UpgradeData> result = new();
            if (count > 0 && offerNewRing && state != null && state.CanAddRing(out _))
                result.Add(definitions.Find(d => d.RewardKind == OrbitalRewardKind.NewRing));
            while (result.Count < count && pool.Count > 0)
            {
                float total = pool.Sum(value => Mathf.Max(0.01f, value.Weight));
                float roll = RewardRandom.value * total;
                int selectedIndex = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= Mathf.Max(0.01f, pool[i].Weight);
                    if (roll <= 0f)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                result.Add(pool[selectedIndex]);
                pool.RemoveAt(selectedIndex);
            }
            return result;
        }

        public static bool IsDemoReward(OrbitalRewardKind kind) => kind is
            OrbitalRewardKind.Pistol or OrbitalRewardKind.LaserSword or
            OrbitalRewardKind.ImpulseGun or OrbitalRewardKind.ArcEmitter or
            OrbitalRewardKind.NewRing or OrbitalRewardKind.RingPower or
            OrbitalRewardKind.RingSpeed or OrbitalRewardKind.AddMount or
            OrbitalRewardKind.CoreUpgrade or OrbitalRewardKind.RingCapacity or
            OrbitalRewardKind.LinkPair or OrbitalRewardKind.MaxHealth or OrbitalRewardKind.MoveSpeed;

        public bool IsEligible(OrbitalRewardKind kind) =>
            IsEligible(kind, RunStateManager.Instance?.OrbitalStationState, RunStateManager.Instance?.ItemSlots);

        public bool IsEligible(OrbitalRewardKind kind, OrbitalRunState state, RunItemSlots slots) =>
            GetEligibleDefinitions(state, slots).Any(value => value.RewardKind == kind);

        public OrbitalRewardData GetDefinition(OrbitalRewardKind kind) =>
            GetDefinition(kind, RunStateManager.Instance?.OrbitalStationState);

        public OrbitalRewardData GetDefinition(OrbitalRewardKind kind, OrbitalRunState state)
        {
            RefreshPresentation(state);
            return definitions.Find(value => value.RewardKind == kind);
        }

        public IReadOnlyList<OrbitalRewardKind> GetEligibleKinds() =>
            GetEligibleDefinitions(RunStateManager.Instance?.OrbitalStationState,
                RunStateManager.Instance?.ItemSlots, demoOnly: true).Select(value => value.RewardKind).ToArray();

        public IReadOnlyList<UpgradeData> GetEligibleNormalRewards()
        {
            OrbitalRunState state = RunStateManager.Instance?.OrbitalStationState;
            RefreshPresentation(state);
            return GetEligibleDefinitions(
                    state,
                    RunStateManager.Instance?.ItemSlots,
                    demoOnly: true)
                .Where(value => value.RewardKind != OrbitalRewardKind.NewRing)
                .Cast<UpgradeData>()
                .ToArray();
        }

        public string GetEligibilitySummary()
        {
            OrbitalRunState state = RunStateManager.Instance?.OrbitalStationState;
            int free = state?.FreeBuiltMounts ?? 0;
            return $"free={free}; eligible=[{string.Join(",", GetEligibleKinds())}]";
        }

        public void Dispose()
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(definitions[i]);
                    else UnityEngine.Object.DestroyImmediate(definitions[i]);
                }
            definitions.Clear();
        }

        private List<OrbitalRewardData> GetEligibleDefinitions(OrbitalRunState state,
            RunItemSlots slots, bool demoOnly = false)
        {
            if (state == null || !state.Validate(out _))
                return new List<OrbitalRewardData>();
            int freeMounts = state.FreeBuiltMounts;
            List<OrbitalRewardData> result = new();
            for (int i = 0; i < definitions.Count; i++)
            {
                OrbitalRewardData reward = definitions[i];
                if (demoOnly && (!IsDemoReward(reward.RewardKind) ||
                    (reward.RewardKind == OrbitalRewardKind.CoreUpgrade &&
                     state.Rings.Count < config.MinRingsForCoreOffer))) continue;
                bool eligible = reward.RewardKind switch
                {
                    OrbitalRewardKind.NewRing => state.CanAddRing(out _),
                    OrbitalRewardKind.Pistol => freeMounts >= 1,
                    OrbitalRewardKind.ArcEmitter => freeMounts >= 1,
                    OrbitalRewardKind.LaserSword => freeMounts >= 1,
                    OrbitalRewardKind.ImpulseGun => freeMounts >= 1,
                    OrbitalRewardKind.LinkPair => freeMounts >= 2,
                    OrbitalRewardKind.ModuleDamage => state.Modules.Any(module =>
                        state.CanUpgradeModuleDamage(module.StableModuleId, out _)),
                    OrbitalRewardKind.RingSpeed => state.Rings.Any(ring =>
                        state.CanTargetRingReward(OrbitalRewardKind.RingSpeed, ring.StableRingId)),
                    OrbitalRewardKind.RingPower => state.Rings.Any(ring =>
                        state.CanTargetRingReward(OrbitalRewardKind.RingPower, ring.StableRingId)),
                    OrbitalRewardKind.AddMount => state.Rings.Any(ring =>
                        state.CanTargetRingReward(OrbitalRewardKind.AddMount, ring.StableRingId)),
                    OrbitalRewardKind.RingCapacity => state.Rings.Any(ring =>
                        state.CanTargetRingReward(OrbitalRewardKind.RingCapacity, ring.StableRingId)),
                    OrbitalRewardKind.CoreUpgrade =>
                        state.CanUpgradeCore(out _),
                    OrbitalRewardKind.LinkMatrix => state.CanUpgradeLinkMatrix(out _),
                    OrbitalRewardKind.MaxHealth => CanTakeBody(
                        reward.BodyUpgrade, slots),
                    OrbitalRewardKind.MoveSpeed => CanTakeBody(
                        reward.BodyUpgrade, slots),
                    _ => false
                };
                if (eligible)
                    result.Add(reward);
            }
            return result;
        }

        private void CreateDefinitions()
        {
            Add(OrbitalRewardKind.NewRing, "reward.newRing",
                "reward.newRingDescription", 0f, false);
            Add(OrbitalRewardKind.Pistol, "reward.gun",
                "reward.gunDescription",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.LaserSword, "reward.sword",
                "reward.swordDescription",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.ImpulseGun, "reward.impulse",
                "reward.impulseDescription",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.ArcEmitter, "reward.arc",
                "reward.arcDescription",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.LinkPair, "reward.link",
                "reward.linkDescription",
                config.LinkPairWeight, true);
            Add(OrbitalRewardKind.ModuleDamage, "reward.damage",
                "reward.damageDescription",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.RingSpeed, "reward.speed",
                "reward.speedDescription",
                config.RingWeight, true);
            Add(OrbitalRewardKind.RingPower, "reward.power",
                "reward.powerDescription",
                config.RingWeight, true);
            Add(OrbitalRewardKind.AddMount, "reward.mount",
                "reward.mountDescription",
                config.RingWeight, true);
            Add(OrbitalRewardKind.RingCapacity, "reward.capacity",
                "reward.capacityDescription", config.RingWeight, true);
            Add(OrbitalRewardKind.CoreUpgrade, "reward.core1",
                "reward.coreWave1",
                config.CoreWeight, false);
            Add(OrbitalRewardKind.LinkMatrix, "reward.matrix",
                "reward.matrixDescription",
                config.CoreWeight, false);
            Add(OrbitalRewardKind.MaxHealth, "reward.health",
                string.Format(LocalizationService.EnsureExists().Get("reward.healthDescription"), ProductionUpgradeProfiles.MaxHealthBonus(1), RunItemSlots.MaxItemLevel),
                config.SubjectWeight, false, maxHealthUpgrade);
            Add(OrbitalRewardKind.MoveSpeed, "reward.movement",
                string.Format(LocalizationService.EnsureExists().Get("reward.movementDescription"), (ProductionUpgradeProfiles.MoveSpeedMultiplier(1) - 1f) * 100f, RunItemSlots.MaxItemLevel),
                config.SubjectWeight, false, moveSpeedUpgrade);
        }

        internal void RefreshPresentation(OrbitalRunState state)
        {
            foreach (var definition in definitions)
            {
                if (definition.RewardKind == OrbitalRewardKind.MaxHealth)
                    definition.description = string.Format(LocalizationService.EnsureExists().Get("reward.healthDescription"), ProductionUpgradeProfiles.MaxHealthBonus(1), RunItemSlots.MaxItemLevel);
                if (definition.RewardKind == OrbitalRewardKind.MoveSpeed)
                    definition.description = string.Format(LocalizationService.EnsureExists().Get("reward.movementDescription"), (ProductionUpgradeProfiles.MoveSpeedMultiplier(1) - 1f) * 100f, RunItemSlots.MaxItemLevel);
                if (definition.RewardKind is not (OrbitalRewardKind.RingPower or OrbitalRewardKind.RingSpeed or
                    OrbitalRewardKind.AddMount or OrbitalRewardKind.RingCapacity)) continue;
                var ring = state?.Rings.FirstOrDefault(r => state.CanTargetRingReward(definition.RewardKind, r.StableRingId));
                string target = ring == null ? LocalizationService.EnsureExists().Get("reward.selectRing") : string.Format(LocalizationService.EnsureExists().Get("reward.ringTarget"), ring.Order + 1);
                definition.description = definition.RewardKind switch
                {
                    OrbitalRewardKind.AddMount => LocalizationService.EnsureExists().Get("reward.mountDetail") + target +
                        (ring == null ? "" : $"{ring.MountCount}/{ring.MountCapacity} → {ring.MountCount + 1}/{ring.MountCapacity}"),
                    OrbitalRewardKind.RingCapacity => LocalizationService.EnsureExists().Get("reward.capacityDetail") + target +
                        (ring == null ? "" : $"{ring.MountCapacity} → {ring.MountCapacity + 1}"),
                    OrbitalRewardKind.RingPower => LocalizationService.EnsureExists().Get("reward.powerDetail") + target +
                        (ring == null ? "" : $"×{ring.PowerMultiplier:0.00} → ×{ring.PowerMultiplier * (1f + config.PowerIncrement):0.00}"),
                    _ => LocalizationService.EnsureExists().Get("reward.speedDetail") + target +
                        (ring == null ? "" : $"×{Mathf.Pow(1f + config.SpeedIncrement, ring.SpeedUpgradeLevel):0.00} → ×{Mathf.Pow(1f + config.SpeedIncrement, ring.SpeedUpgradeLevel + 1):0.00}")
                };
            }
            OrbitalCoreState core = state?.CoreState;
            OrbitalRewardData reward = definitions.Find(value =>
                value.RewardKind == OrbitalRewardKind.CoreUpgrade);
            if (reward == null || core == null)
                return;
            int next = Mathf.Min(3, core.Level + 1);
            reward.upgradeName = next switch { 1 => "reward.core1", 2 => "reward.core2", _ => "reward.core3" };
            reward.description = next switch
            {
                1 => "reward.coreWave1",
                2 => "reward.coreWave2",
                _ => "reward.coreWave3"
            };
        }

        private void Add(OrbitalRewardKind kind, string title,
            string description, float weight, bool arena,
            UpgradeData bodyUpgrade = null)
        {
            OrbitalRewardData data =
                ScriptableObject.CreateInstance<OrbitalRewardData>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.name = $"Orbital Reward {kind}";
            data.upgradeName = title;
            data.description = description;
            data.category = arena ? UpgradeCategory.Behavior : UpgradeCategory.Numeric;
            data.upgradeType = UpgradeType.OrbitalReward;
            data.RewardKind = kind;
            data.PresentationOwner = this;
            data.BodyUpgrade = bodyUpgrade;
            data.Weight = weight;
            data.RequiresArenaSelection = arena;
            definitions.Add(data);
        }

        private static UpgradeData Find(UpgradeData[] source, UpgradeType type)
        {
            if (source == null)
                return null;
            return Array.Find(source, value => value != null &&
                value.upgradeType == type);
        }

        private static bool CanTakeBody(UpgradeData upgrade, RunItemSlots slots)
        {
            return upgrade != null && (slots == null || slots.CanAccept(upgrade));
        }

        public static bool IsModuleUnlocked(OrbitalModuleKind kind) =>
            BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Weapon) >=
            GetRequiredWeaponStationLevel(kind);

        public static int GetRequiredWeaponStationLevel(
            OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.LaserSword => 2,
            OrbitalModuleKind.ImpulseGun => 3,
            _ => 1
        };
    }
}
