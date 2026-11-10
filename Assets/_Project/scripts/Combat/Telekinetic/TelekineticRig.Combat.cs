using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        private readonly List<Collider2D> overlaps=new();
        private readonly HashSet<EnemyHealth> contactTargets=new();
        private long volleyId;
        public OrbitalWeaponSettings Settings(TelekineticItem c) => c.Kind==TelekineticPart.Pistol ? Pistol : c.Kind==TelekineticPart.Shotgun ? Shotgun : c.Kind==TelekineticPart.Sniper ? Sniper : Saw;
        public float DamageFactor(TelekineticItem item) => ActiveModifiers(item).DamageMultiplier;
        public float RateFactor(TelekineticItem item) => ActiveModifiers(item).RateMultiplier;
        public int ExtraShots(TelekineticItem item) => ActiveModifiers(item).ExtraAttacks;
        public int Ricochets(TelekineticItem item) => ActiveModifiers(item).Ricochets;
        public int LinkedWeapons(TelekineticItem module) => Build.Neighbours(module).Count(weapon => FeedsWeapon(module, weapon));

        private float BaseAttackInterval(TelekineticItem item) => item.Kind == TelekineticPart.Saw
            ? Saw.ContactHitInterval : Settings(item).FireInterval / station.State.ShootingRateMultiplier;

        private static bool FeedsWeapon(TelekineticItem module, TelekineticItem weapon) =>
            module.IsModule && Affects(module.Kind, weapon);

        private static bool EnergyAffects(TelekineticItem module) => module.IsModule &&
            module.Kind is not (TelekineticPart.Double or TelekineticPart.Shred or TelekineticPart.Echo);

        private float ModuleEnergyBonus(TelekineticItem module) => EnergyAffects(module) && Build.Items.Contains(module)
            ? Build.Neighbours(module).Where(item => item.Kind == TelekineticPart.Energy).Sum(item => Modules.EnergyBonus * item.Level)
            : 0f;

        private IEnumerable<TelekineticItem> LinkedModules(TelekineticItem weapon)
        {
            if (!Build.Items.Contains(weapon)) yield break;
            foreach (var module in Build.Neighbours(weapon))
                if (FeedsWeapon(module, weapon)) yield return module;
        }

        private struct WeaponModifiers
        {
            public float DamageBonus, RateBonus, CritBonus, PierceStrength, SplashFraction, ChainFraction, SlowReduction, Knockback;
            public int ExtraAttacks, Ricochets, Echoes;
            public bool BypassShields;
            public float DamageMultiplier => 1 + DamageBonus;
            public float RateMultiplier => 1 + RateBonus;
            public float CritMultiplier => 1 + CritBonus;
        }

        // The current layout is the source of both weapon stats and constructor information.
        private WeaponModifiers ActiveModifiers(TelekineticItem weapon)
        {
            var result = new WeaponModifiers();
            if (!Suppressed)
                foreach (var module in LinkedModules(weapon))
                    ApplyModule(ref result, module);
            return result;
        }

        private void ApplyModule(ref WeaponModifiers result, TelekineticItem module)
        {
            float strength = (module.Kind == TelekineticPart.RIC ? module.Level : Modules.Strength(module))
                * (1 + ModuleEnergyBonus(module));
            switch (module.Kind)
            {
                case TelekineticPart.DMG: result.DamageBonus += (Modules.DamageMultiplier - 1) * strength; break;
                case TelekineticPart.FR: result.RateBonus += (Modules.RapidMultiplier - 1) * strength; break;
                case TelekineticPart.Double: result.ExtraAttacks++; break;
                case TelekineticPart.RIC: result.Ricochets += Mathf.RoundToInt(strength); break;
                case TelekineticPart.Pierce: result.PierceStrength += strength; break;
                case TelekineticPart.Splash: result.SplashFraction += Modules.SplashDamageFraction * strength; break;
                case TelekineticPart.Chain: result.ChainFraction += Modules.ChainDamageFraction * strength; break;
                case TelekineticPart.Crit: result.CritBonus += (Modules.CritMultiplier - 1) * strength; break;
                case TelekineticPart.Slow: result.SlowReduction += (1 - Modules.SlowMultiplier) * strength; break;
                case TelekineticPart.Shred: result.BypassShields = true; break;
                case TelekineticPart.Knock: result.Knockback += Modules.KnockBonus * strength; break;
                case TelekineticPart.Echo: result.Echoes++; break;
            }
        }

        private void TickWeapons(float dt)
        {
            foreach (var item in Build.Items)
            {
                if (!item.IsWeapon) continue;
                var view=views[item.Id].transform;
                if (item.Kind==TelekineticPart.Saw) view.localScale=Vector3.one*station.State.ContactSizeMultiplier;
                if (item.Kind==TelekineticPart.Saw) view.GetChild(0).Rotate(0,0,Saw.SpinDegreesPerSecond*dt,Space.Self);
                item.ShotRemaining-=dt*RateFactor(item);
                if (item.ShotRemaining>0) continue;
                bool saw=item.Kind==TelekineticPart.Saw;
                Vector2 point = view.position;
                Vector2 direction = view.right;
                if (saw && !GatherContactTargets(point)) continue;
                var attack=CaptureAttack(item);
                if (!EmitCaptured(attack,attack.Volleys,point,direction,saw)) continue;
                if (!saw) TriggerWeaponEffects(item);
                item.ShotRemaining = BaseAttackInterval(item);
                item.Shots+=attack.Volleys; ShotsFired+=attack.Volleys;
                for (int i=0;i<attack.Echoes;i++)
                    echoes.Add(new DelayedEcho { Attack=attack,Point=point,Direction=direction,Due=CombatSeconds+Modules.EchoDelay+i*.04f });
            }
        }
    }
}
