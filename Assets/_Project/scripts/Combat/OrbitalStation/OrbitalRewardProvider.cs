using System;
using System.Collections.Generic;
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
        ModuleDamage
    }

    public sealed class OrbitalRewardData : UpgradeData
    {
        public OrbitalRewardKind RewardKind;
        public UpgradeData BodyUpgrade;
        public float Weight;
        public bool RequiresArenaSelection;
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

        public List<UpgradeData> BuildChoices(int count)
        {
            RefreshPresentation();
            List<OrbitalRewardData> pool = GetEligibleDefinitions();
            List<UpgradeData> result = new();
            while (result.Count < count && pool.Count > 0)
            {
                float total = pool.Sum(value => Mathf.Max(0.01f, value.Weight));
                float roll = UnityEngine.Random.value * total;
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

        public bool IsEligible(OrbitalRewardKind kind) =>
            GetEligibleDefinitions().Any(value => value.RewardKind == kind);

        public OrbitalRewardData GetDefinition(OrbitalRewardKind kind)
        {
            RefreshPresentation();
            return definitions.Find(value => value.RewardKind == kind);
        }

        public IReadOnlyList<OrbitalRewardKind> GetEligibleKinds() =>
            GetEligibleDefinitions().Select(value => value.RewardKind).ToArray();

        public string GetEligibilitySummary()
        {
            OrbitalRunState state = RunStateManager.Instance?.OrbitalStationState;
            int free = state == null ? 0 : state.Rings.Sum(ring =>
                ring.MountCapacity - state.Modules.Count(module =>
                    module.StableRingId == ring.StableRingId));
            return $"free={free}; eligible=[{string.Join(",", GetEligibleKinds())}]";
        }

        public void Dispose()
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null)
                    UnityEngine.Object.Destroy(definitions[i]);
            definitions.Clear();
        }

        private List<OrbitalRewardData> GetEligibleDefinitions()
        {
            OrbitalRunState state = RunStateManager.Instance?.OrbitalStationState;
            if (state == null || !state.Validate(out _))
                return new List<OrbitalRewardData>();
            int freeMounts = state.Rings.Sum(ring => ring.MountCapacity) -
                state.Modules.Count;
            int linkNodes = state.Modules.Count(module =>
                module.ModuleType == OrbitalModuleKind.LinkNode);
            RunItemSlots slots = RunStateManager.Instance?.ItemSlots;
            List<OrbitalRewardData> result = new();
            for (int i = 0; i < definitions.Count; i++)
            {
                OrbitalRewardData reward = definitions[i];
                bool eligible = reward.RewardKind switch
                {
                    OrbitalRewardKind.Pistol or
                    OrbitalRewardKind.ArcEmitter => freeMounts >= 1,
                    OrbitalRewardKind.LaserSword => freeMounts >= 1 &&
                        GetWeaponStationLevel() >= 2,
                    OrbitalRewardKind.ImpulseGun => freeMounts >= 1 &&
                        GetWeaponStationLevel() >= 3,
                    OrbitalRewardKind.LinkPair => freeMounts >= 2,
                    OrbitalRewardKind.ModuleDamage => state.Modules.Any(module =>
                        module.ModuleType != OrbitalModuleKind.LinkNode),
                    OrbitalRewardKind.RingSpeed => state.Rings.Any(ring =>
                        ring.SpeedUpgradeLevel < config.MaxSpeedUpgradeLevel),
                    OrbitalRewardKind.RingPower => state.Rings.Any(ring =>
                        ring.PowerUpgradeLevel < config.MaxPowerUpgradeLevel),
                    OrbitalRewardKind.AddMount => state.Rings.Any(ring =>
                        ring.MountCapacity < config.MaxMountsPerRing),
                    OrbitalRewardKind.CoreUpgrade =>
                        state.CoreState.Level < config.MaxCoreLevel,
                    OrbitalRewardKind.LinkMatrix => linkNodes >= 2 &&
                        state.CoreState.LinkMatrixUpgradeLevel <
                        config.MaxLinkMatrixLevel,
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
            Add(OrbitalRewardKind.Pistol, "НОВЫЙ PISTOL",
                "МОДУЛЬ\nУстановите Pistol на свободное крепление.",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.LaserSword, "LASER SWORD",
                "МОДУЛЬ\nКонтактный клинок для выбранной орбиты. Укажите крепление.",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.ImpulseGun, "IMPULSE GUN",
                "МОДУЛЬ\nВыстрел наносит урон и отталкивает цель. Укажите крепление.",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.ArcEmitter, "ARC EMITTER",
                "МОДУЛЬ\nФиолетовый разряд перескакивает между целями. Укажите крепление.",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.LinkPair, "LINK PAIR",
                "МОДУЛЬ\nДва узла создают повреждающую энергетическую связь. Установите оба узла.",
                config.LinkPairWeight, true);
            Add(OrbitalRewardKind.ModuleDamage, "УСИЛИТЬ МОДУЛЬ",
                "МОДУЛЬ\nВыберите установленное оружие. Damage Level +1 даёт +25% базового урона.",
                config.ModuleWeight, true);
            Add(OrbitalRewardKind.RingSpeed, "ПЕРЕГРУЗКА КОЛЬЦА",
                "КОЛЬЦО\nВыбранная орбита вращается на 25% быстрее. После выбора укажите кольцо.",
                config.RingWeight, true);
            Add(OrbitalRewardKind.RingPower, "УСИЛИТЕЛЬ КОЛЬЦА",
                "КОЛЬЦО\nСила объектов выбранной орбиты увеличивается на 25%. Сила связи — среднее двух колец.",
                config.RingWeight, true);
            Add(OrbitalRewardKind.AddMount, "НОВОЕ КРЕПЛЕНИЕ",
                "КОЛЬЦО\nДобавляет одну рабочую точку на выбранную орбиту.",
                config.RingWeight, true);
            Add(OrbitalRewardKind.CoreUpgrade, "АКТИВИРОВАТЬ ЯДРО",
                "ЯДРО\nЗапускает каскад импульсов по орбитам.",
                config.CoreWeight, false);
            Add(OrbitalRewardKind.LinkMatrix, "LINK MATRIX",
                "ЯДРО\nУсиливает урон существующей энергетической сети.",
                config.CoreWeight, false);
            Add(OrbitalRewardKind.MaxHealth, "MAX HP",
                "SUBJECT\nУвеличивает максимальный запас здоровья.",
                config.SubjectWeight, false, maxHealthUpgrade);
            Add(OrbitalRewardKind.MoveSpeed, "MOVE SPEED",
                "SUBJECT\nУвеличивает скорость перемещения Subject.",
                config.SubjectWeight, false, moveSpeedUpgrade);
        }

        private void RefreshPresentation()
        {
            OrbitalCoreState core =
                RunStateManager.Instance?.OrbitalStationState?.CoreState;
            OrbitalRewardData reward = definitions.Find(value =>
                value.RewardKind == OrbitalRewardKind.CoreUpgrade);
            if (reward == null || core == null)
                return;
            if (core.Level <= 0)
            {
                reward.upgradeName = "АКТИВИРОВАТЬ ЯДРО";
                reward.description = "ЯДРО\nЗапускает каскад импульсов по орбитам.";
            }
            else if (core.Level == 1)
            {
                reward.upgradeName = "CORE II: УСКОРЕНИЕ КАСКАДА";
                reward.description = "ЯДРО\nКаскад срабатывает чаще, а модули наносят больше урона.";
            }
            else
            {
                reward.upgradeName = "CORE III: УСИЛЕННЫЙ ИМПУЛЬС";
                reward.description = "ЯДРО\nФинальное усиление силы и частоты каскада.";
            }
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

        private static int GetWeaponStationLevel() =>
            BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Weapon);
    }
}
