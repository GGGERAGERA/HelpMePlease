using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalRewardProvider
    {
        // Includes the conditional new-ring offer and excludes legacy lab-only rewards.
        public IReadOnlyList<OrbitalRewardData> GetActiveCatalogue(OrbitalRunState state)
        {
            RefreshPresentation(state);
            return definitions.Where(d => d.InNormalPool || d.RewardKind == OrbitalRewardKind.NewRing).ToArray();
        }

        public string GetUnavailableReason(OrbitalRewardKind kind, OrbitalRunState state, RunItemSlots slots)
        {
            var data = definitions.Find(d => d.RewardKind == kind);
            if (data == null || (!data.InNormalPool && kind != OrbitalRewardKind.NewRing)) return "Карточка не входит в действующий пул";
            if (state == null || !state.IsInitialized) return "Сначала начните ран";
            if (!state.Validate(out _)) return "Состояние станции некорректно";
            if (kind == OrbitalRewardKind.CoreUpgrade && state.Rings.Count < config.MinRingsForCoreOffer)
                return $"Нужно орбит: {config.MinRingsForCoreOffer}";
            if (GetEligibleDefinitions(state, slots, true).Any(d => d.RewardKind == kind)) return null;
            if (GetModuleKind(kind) is OrbitalModuleKind module)
                return module == OrbitalModuleKind.LinkNode ? "Нужны две свободные построенные точки" : "Нет свободных построенных точек";
            return kind switch
            {
                OrbitalRewardKind.NewRing => $"Достигнут предел орбит: {config.MaxNormalRings}",
                OrbitalRewardKind.CoreUpgrade => $"Достигнут уровень ядра: {config.MaxCoreLevel}",
                OrbitalRewardKind.RingSpeed => $"Все орбиты достигли предела скорости: {config.MaxSpeedUpgradeLevel}",
                OrbitalRewardKind.RingPower => $"Все орбиты достигли предела мощности: {config.MaxPowerUpgradeLevel}",
                OrbitalRewardKind.AddMount => "На всех орбитах заполнена текущая вместимость; сначала увеличьте её",
                OrbitalRewardKind.RingCapacity => $"Сначала постройте все точки; предел вместимости — {config.MaxMountsPerRing}",
                OrbitalRewardKind.MaxHealth or OrbitalRewardKind.MoveSpeed => data.BodyUpgrade == null ? "Не назначено базовое улучшение" :
                    slots != null && slots.GetLevel(data.BodyUpgrade) >= RunItemSlots.MaxItemLevel ? $"Достигнут уровень {RunItemSlots.MaxItemLevel}" : "Нет свободного слота персонажа",
                _ => "Достигнут допустимый предел эффекта"
            };
        }

        public string GetLevelSummary(OrbitalRewardData data, OrbitalRunState state, RunItemSlots slots)
        {
            if (data == null || state == null) return "Ран не начат";
            if (GetModuleKind(data.RewardKind) is OrbitalModuleKind module)
                return $"Установлено: {state.Modules.Count(m => m.ModuleType == module)} · свободных точек: {state.FreeBuiltMounts}";
            if (data.BodyUpgrade != null) return $"Уровень: {slots?.GetLevel(data.BodyUpgrade) ?? 0}/{RunItemSlots.MaxItemLevel}";
            return data.RewardKind switch
            {
                OrbitalRewardKind.CoreUpgrade => $"Ядро: {state.CoreState.Level}/{config.MaxCoreLevel}",
                OrbitalRewardKind.NewRing => $"Орбит: {state.Rings.Count}/{config.MaxNormalRings}",
                OrbitalRewardKind.ShootingDamage => CategoryLevel(state.ShootingDamageMultiplier, 1.2f),
                OrbitalRewardKind.ShootingRate => CategoryLevel(state.ShootingRateMultiplier, 1.15f),
                OrbitalRewardKind.ContactDamage => CategoryLevel(state.ContactDamageMultiplier, 1.2f),
                OrbitalRewardKind.ContactSize => CategoryLevel(state.ContactSizeMultiplier, 1.15f),
                _ => string.Join(" · ", state.Rings.Select(r => data.RewardKind switch
                {
                    OrbitalRewardKind.RingSpeed => $"Орбита {r.Order + 1}: {r.SpeedUpgradeLevel}/{config.MaxSpeedUpgradeLevel}",
                    OrbitalRewardKind.RingPower => $"Орбита {r.Order + 1}: {r.PowerUpgradeLevel}/{config.MaxPowerUpgradeLevel}",
                    _ => $"Орбита {r.Order + 1}: {r.MountCount}/{r.MountCapacity} точек (предел {config.MaxMountsPerRing})"
                }))
            };
        }
        private static string CategoryLevel(float multiplier, float step) =>
            $"Уровень: {Mathf.RoundToInt(Mathf.Log(Mathf.Max(1f, multiplier)) / Mathf.Log(step))} · сейчас ×{multiplier:0.##} · без отдельного лимита уровней";
    }
}
