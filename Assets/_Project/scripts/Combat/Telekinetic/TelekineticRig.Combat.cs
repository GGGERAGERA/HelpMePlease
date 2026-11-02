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
        public float DamageFactor(TelekineticItem c) => Suppressed ? 1 : 1+c.Damage.Values.Sum();
        public float RateFactor(TelekineticItem c) => Suppressed ? 1 : 1+c.Rapid.Values.Sum();
        public int ExtraShots(TelekineticItem c) => Suppressed ? 0 : c.Extra.Values.Sum();
        public int Ricochets(TelekineticItem c) => Suppressed ? 0 : c.Ricochet.Values.Sum();
        public int LinkedWeapons(TelekineticItem c) => Build.Neighbours(c).Count(n=>Affects(c.Kind,n));

        private void TickModules(float dt)
        {
            foreach (var weapon in Build.Items)
            {
                if (!weapon.IsWeapon) continue;
                foreach (int source in weapon.RapidUntil.Where(p=>p.Value<=CombatSeconds).Select(p=>p.Key).ToArray())
                { weapon.RapidUntil.Remove(source); weapon.Rapid.Remove(source); }
            }
            foreach (var c in Build.Items)
            {
                if (!c.IsModule) continue;
                float cooldown=Modules.Cooldown(c,Build);
                if (!Mathf.Approximately(c.LastCooldown,cooldown))
                { c.Remaining=c.LastCooldown>0 ? cooldown*Mathf.Clamp01(c.Remaining/c.LastCooldown) : cooldown; c.LastCooldown=cooldown; }
                c.Remaining-=dt;
                if (c.Remaining>0) continue;
                c.Remaining=Modules.Cooldown(c,Build); c.Activations++; ModulePulses++;
                foreach (var w in Build.Neighbours(c))
                {
                    if (!Affects(c.Kind,w)) continue;
                    switch (c.Kind)
                    {
                        case TelekineticPart.DMG: w.Damage[c.Id]=(Modules.DamageMultiplier-1)*Modules.Strength(c); break;
                        case TelekineticPart.FR:
                            w.Rapid[c.Id]=(Modules.RapidMultiplier-1)*Modules.Strength(c);
                            w.RapidUntil[c.Id]=CombatSeconds+Modules.RapidDuration; break;
                        case TelekineticPart.Double: w.Extra[c.Id]=1; break;
                        case TelekineticPart.RIC: if (w.Kind!=TelekineticPart.Saw) w.Ricochet[c.Id]=c.Level; break;
                        default: w.Effects[c.Id]=new TelekineticCharge(c.Kind,Modules.Strength(c)); break;
                    }
                }
            }
        }
        private void TickWeapons(float dt)
        {
            foreach (var item in Build.Items)
            {
                if (!item.IsWeapon) continue;
                var view=views[item.Id];
                if (item.Kind==TelekineticPart.Saw) view.transform.localScale=Vector3.one*station.State.ContactSizeMultiplier;
                if (item.Kind==TelekineticPart.Saw) view.transform.GetChild(0).Rotate(0,0,Saw.SpinDegreesPerSecond*dt,Space.Self);
                item.ShotRemaining-=dt*RateFactor(item);
                if (item.ShotRemaining>0) continue;
                bool saw=item.Kind==TelekineticPart.Saw;
                if (saw && !GatherContactTargets(view.transform.position)) continue;
                var attack=CaptureAttack(item);
                if (!EmitCaptured(attack,attack.Volleys,saw)) continue;
                item.ShotRemaining=saw?Saw.ContactHitInterval:Settings(item).FireInterval / station.State.ShootingRateMultiplier;
                item.Shots+=attack.Volleys; ShotsFired+=attack.Volleys;
                for (int i=0;i<attack.Echoes;i++) echoes.Add(new DelayedEcho { Attack=attack,Due=CombatSeconds+Modules.EchoDelay+i*.04f });
                item.Damage.Clear(); item.Extra.Clear(); item.Ricochet.Clear(); item.Effects.Clear();
            }
        }
    }
}
