using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.Telekinetic;

namespace Subject42.Combat.OrbitalStation
{
    public sealed partial class OrbitalRewardProvider
    {
        internal static TelekineticPart? GetRigPart(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Pistol => TelekineticPart.Pistol,
            OrbitalModuleKind.Shotgun => TelekineticPart.Shotgun,
            OrbitalModuleKind.Saw => TelekineticPart.Saw,
            OrbitalModuleKind.Sniper => TelekineticPart.Sniper,
            _ => null
        };

        public static TelekineticPart? GetRigPart(OrbitalRewardKind kind) =>
            GetModuleKind(kind) is OrbitalModuleKind module ? GetRigPart(module) : kind switch
        {
            OrbitalRewardKind.DamageLink=>TelekineticPart.DMG, OrbitalRewardKind.FireRateLink=>TelekineticPart.FR,
            OrbitalRewardKind.ExtraShotLink=>TelekineticPart.Double, OrbitalRewardKind.RicochetLink=>TelekineticPart.RIC,
            OrbitalRewardKind.RigEnergy=>TelekineticPart.Energy,
            OrbitalRewardKind.RigPierce=>TelekineticPart.Pierce, OrbitalRewardKind.RigSplash=>TelekineticPart.Splash,
            OrbitalRewardKind.RigChain=>TelekineticPart.Chain, OrbitalRewardKind.RigCrit=>TelekineticPart.Crit,
            OrbitalRewardKind.RigSlow=>TelekineticPart.Slow, OrbitalRewardKind.RigShred=>TelekineticPart.Shred,
            OrbitalRewardKind.RigKnock=>TelekineticPart.Knock, OrbitalRewardKind.RigEcho=>TelekineticPart.Echo,
            _=>null
        };
        private static bool RigSupported(OrbitalRewardKind kind) => GetRigPart(kind).HasValue || kind is
            OrbitalRewardKind.RigExpansion or OrbitalRewardKind.MaxHealth or OrbitalRewardKind.MoveSpeed or
            OrbitalRewardKind.ContactDamage or OrbitalRewardKind.ContactSize;

        private static IOrderedEnumerable<OrbitalRewardData> OrderRigCards(IEnumerable<OrbitalRewardData> rewards) =>
            rewards.OrderBy(reward => reward.CardType).ThenBy(reward =>
                reward.CardType == RigRewardCardType.Weapon ? GetRigPart(reward.RewardKind) : null);

        private void CreateRigDefinitions()
        {
            Add(OrbitalRewardKind.RigExpansion, "reward.rig.card.expansionTitle", "reward.rig.card.expansion", config.RingWeight, false);
            foreach (OrbitalRewardKind kind in Enum.GetValues(typeof(OrbitalRewardKind)))
            {
                if (kind < OrbitalRewardKind.RigEnergy || GetRigPart(kind) is not TelekineticPart part) continue;
                Add(kind, TelekineticBuild.Label(part), "reward.rig.card." + part, config.ModuleWeight, false);
            }
        }

        private static void RefreshRigCard(OrbitalRewardData reward, TelekineticBuild build)
        {
            var localization = LocalizationService.EnsureExists();
            if (GetRigPart(reward.RewardKind) is TelekineticPart part)
            {
                bool weapon = TelekineticRig.WeaponKind(part);
                reward.upgradeName = TelekineticBuild.Label(part);
                reward.CompactDescription = GetPartDescription(part, localization);
                reward.RequiresArenaSelection = weapon;
                reward.CardType = weapon ? RigRewardCardType.Weapon : RigRewardCardType.Module;
                return;
            }

            if (reward.RewardKind == OrbitalRewardKind.RigExpansion)
            {
                int size = build.AvailableRadius * 2 + 1;
                int next = Math.Min(TelekineticBuild.Radius, build.AvailableRadius + 1) * 2 + 1;
                reward.upgradeName = "reward.rig.card.expansionTitle";
                reward.CompactDescription = string.Format(localization.Get("reward.rig.card.expansion"), size, next);
                reward.CardType = RigRewardCardType.Construction;
                return;
            }

            reward.CompactDescription = reward.RewardKind switch
            {
                OrbitalRewardKind.MaxHealth => string.Format(localization.Get("reward.rig.card.health"),
                    ProductionUpgradeProfiles.MaxHealthBonus(1)),
                OrbitalRewardKind.MoveSpeed => string.Format(localization.Get("reward.rig.card.movement"),
                    (ProductionUpgradeProfiles.MoveSpeedMultiplier(1) - 1f) * 100f),
                OrbitalRewardKind.ContactDamage => localization.Get("reward.rig.card.contactDamage"),
                OrbitalRewardKind.ContactSize => localization.Get("reward.rig.card.contactSize"),
                _ => null
            };
            if (reward.CompactDescription == null) return;

            reward.CardType = RigRewardCardType.Upgrade;
            if ((reward.RewardKind is OrbitalRewardKind.ContactDamage or OrbitalRewardKind.ContactSize) &&
                !build.Items.Concat(build.Reserve).Any(item => item.Kind == TelekineticPart.Saw))
                reward.CardDetail = localization.Get("reward.rig.card.noContactWeapon");
        }

        private static string GetPartDescription(TelekineticPart part, LocalizationService localization)
        {
            var text = localization.Get("reward.rig.card." + part);
            var rig = OrbitalPresentationConfig.Active.TelekineticRigPrefab;
            var tuning = rig.Modules;
            return part switch
            {
                TelekineticPart.Shotgun => string.Format(text, rig.Shotgun.Pellets),
                TelekineticPart.DMG => string.Format(text, (tuning.DamageMultiplier - 1f) * 100f),
                TelekineticPart.FR => string.Format(text, (tuning.RapidMultiplier - 1f) * 100f),
                TelekineticPart.Energy => string.Format(text, 1f + tuning.EnergyBonus),
                TelekineticPart.Pierce => string.Format(text, tuning.PierceBodies),
                TelekineticPart.Splash => string.Format(text, tuning.SplashDamageFraction * 100f),
                TelekineticPart.Chain => string.Format(text, TelekineticRig.ChainMaxJumps),
                TelekineticPart.Crit => string.Format(text, tuning.CritMultiplier),
                TelekineticPart.Slow => string.Format(text, tuning.SlowSeconds),
                TelekineticPart.Echo => string.Format(text, tuning.EchoDelay),
                _ => text
            };
        }
    }
}
