using Subject42.Combat.Telekinetic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.DebugLabs.Telekinetic
{
    public sealed partial class TelekineticConstructorLab
    {
        private sealed class ImpactCharge
        {
            internal float Damage,Splash,Chain,SplashRadius,ChainRadius,Slow=1,SlowSeconds;
            internal bool SecondarySpent;
            internal readonly HashSet<EnemyHealth> Targets=new();
            internal ImpactCharge Copy() => new() { Damage=Damage,Splash=Splash,Chain=Chain,SplashRadius=SplashRadius,ChainRadius=ChainRadius,Slow=Slow,SlowSeconds=SlowSeconds };
            internal bool HasImpact => Splash>0 || Chain>0 || Slow<1;
        }
        private sealed class CapturedAttack
        {
            internal TelekineticItem Item;
            internal OrbitalWeaponSettings Profile;
            internal float Damage;
            internal int Ricochets,Volleys,Echoes;
            internal ImpactCharge Impact;
        }
        private sealed class DelayedEcho { internal CapturedAttack Attack; internal float Due; }
        private sealed class SlowState { internal EnemyMovement Movement; internal GameObject Marker; internal float Until,Multiplier; }
        private readonly Dictionary<long,ImpactCharge> projectileCharges=new();
        private readonly Dictionary<EnemyHealth,SlowState> slowed=new();
        private readonly List<EnemyHealth> expiredSlows=new();
        private readonly List<DelayedEcho> echoes=new();
        private readonly List<GameObject> effectVisuals=new();
        private ImpactCharge emittingImpact;
        private EnemyHealth lastBodyTarget;
        public int SplashBursts { get; private set; }
        public int ChainJumps { get; private set; }
        public int EchoShots { get; private set; }
        public int ActiveSlows => slowed.Count;
        public float SlowFactor(EnemyHealth target) => slowed.TryGetValue(target,out var state)?state.Multiplier:1;
        public IEnumerable<TelekineticPart> PendingEffects(TelekineticItem item) => item.Effects.Values.Select(e=>e.Kind).Distinct();
        public float PendingStrength(TelekineticItem item,TelekineticPart kind)
        { float sum=0; foreach (var e in item.Effects.Values) if (e.Kind==kind) sum+=e.Strength; return sum; }
        public float CriticalFactor(TelekineticItem item) => 1+(Modules.CritMultiplier-1)*PendingStrength(item,TelekineticPart.Crit);
        public static bool Affects(TelekineticPart module,TelekineticItem weapon) => weapon.IsWeapon &&
            (weapon.Kind!=TelekineticPart.Saw || module!=TelekineticPart.RIC && module!=TelekineticPart.Pierce);

        private void BindEffectCallbacks()
        {
            combat.Hit+=RememberBodyTarget;
            combat.LabProjectileSpawned+=CaptureProjectile;
            combat.LabProjectileContact+=ProjectileContact;
            combat.LabProjectileEnded+=ProjectileEnded;
        }
        private void RememberBodyTarget(EnemyHealth target,float damage) => lastBodyTarget=target;
        private void CaptureProjectile(long id,int weapon,Vector2 direction)
        { if (emittingImpact!=null && emittingImpact.HasImpact) projectileCharges.Add(id,emittingImpact); }
        private void ProjectileEnded(long id,bool cancelled) => projectileCharges.Remove(id);
        private void ProjectileContact(long id,float damage,bool killed,bool shield)
        {
            // The shared adapter raises Hit immediately before its sequence-tagged body contact.
            // Shield contacts raise no Hit; rejected damage must never use a stale target.
            if (shield || damage<=0 || !projectileCharges.TryGetValue(id,out var charge) || lastBodyTarget==null) return;
            var target=lastBodyTarget;
            ApplyImpact(charge,target,target.transform.position);
        }
        private CapturedAttack CaptureAttack(TelekineticItem item)
        {
            var s=Settings(item);
            float pierce=PendingStrength(item,TelekineticPart.Pierce),knock=PendingStrength(item,TelekineticPart.Knock);
            bool shred=PendingStrength(item,TelekineticPart.Shred)>0;
            // Copy only this attack. Shared authored settings and live projectiles are never changed.
            var profile=new OrbitalWeaponSettings
            {
                Kind=s.Kind,Category=s.Category,WeaponVisual=s.WeaponVisual,ProjectileVisual=s.ProjectileVisual,
                Presentation=s.Presentation,ProjectileVisualScale=s.ProjectileVisualScale,ProjectileTrailSeconds=s.ProjectileTrailSeconds,
                Damage=s.Damage,FireInterval=s.FireInterval,Range=s.Range,ProjectileSpeed=s.ProjectileSpeed,ProjectileRadius=s.ProjectileRadius,
                BurstShots=s.BurstShots,BurstInterval=s.BurstInterval,Pellets=s.Pellets,SpreadDegrees=s.SpreadDegrees,
                BodyPierces=s.BodyPierces+Mathf.RoundToInt(Modules.PierceBodies*pierce),BypassShields=s.BypassShields || shred,
                Knockback=s.Knockback+Modules.KnockBonus*knock,Color=s.Color,ContactRadius=s.ContactRadius,
                ContactHitInterval=s.ContactHitInterval,SpinDegreesPerSecond=s.SpinDegreesPerSecond
            };
            float damage=s.Damage*DamageFactor(item)*CriticalFactor(item);
            float slow=PendingStrength(item,TelekineticPart.Slow);
            return new CapturedAttack
            {
                Item=item,Profile=profile,Damage=damage,Ricochets=Ricochets(item),Volleys=1+ExtraShots(item),
                Echoes=item.Effects.Values.Count(e=>e.Kind==TelekineticPart.Echo),
                Impact=new ImpactCharge { Damage=damage,Splash=Modules.SplashDamageFraction*PendingStrength(item,TelekineticPart.Splash),
                    Chain=Modules.ChainDamageFraction*PendingStrength(item,TelekineticPart.Chain),
                    SplashRadius=Modules.SplashRadius,ChainRadius=Modules.ChainRadius,
                    Slow=slow>0 ? Mathf.Max(.1f,1-(1-Modules.SlowMultiplier)*slow) : 1,SlowSeconds=Modules.SlowSeconds }
            };
        }
        private bool GatherContactTargets(Vector2 point)
        {
            contactTargets.Clear(); overlaps.Clear();
            Physics2D.OverlapCircle(point,Saw.ContactRadius,ContactFilter2D.noFilter,overlaps);
            foreach (var hit in overlaps)
            {
                var target=hit.GetComponentInParent<EnemyHealth>();
                if (target!=null && !target.IsDead && target.isActiveAndEnabled && target.gameObject.scene==gameObject.scene) contactTargets.Add(target);
            }
            return contactTargets.Count>0;
        }
        private bool EmitCaptured(CapturedAttack attack,int volleys,bool contactsReady=false)
        {
            var item=attack.Item; var profile=attack.Profile;
            Vector2 point=views[item.Id].transform.position;
            Vector2 direction=Construction.rotation*item.Center.normalized;
            if (item.Kind==TelekineticPart.Saw)
            {
                if (!contactsReady && !GatherContactTargets(point)) return false;
                var impact=attack.Impact.Copy();
                foreach (var target in contactTargets)
                {
                    if (target==null || target.IsDead) continue;
                    if (!profile.BypassShields && target.TryGetComponent<EnemyDirectionalShield>(out var shield) && shield.BlockDiskContact(point,Saw.ContactRadius)) continue;
                    float before=target.CurrentHealth;
                    for (int shot=0;shot<volleys && !target.IsDead;shot++)
                    {
                        if (profile.BypassShields) target.TakeDamage(attack.Damage,point); else combat.ApplyDamage(target,attack.Damage,point);
                    }
                    if (target.CurrentHealth<before) ApplyImpact(impact,target,point);
                    if (target.CurrentHealth<before && profile.Knockback>0 && !target.IsDead && target.TryGetComponent<EnemyMovement>(out var movement))
                        movement.ApplyKnockback(((Vector2)target.transform.position-point).normalized,Mathf.Min(5,profile.Knockback));
                }
                if (Time.time>=nextContactAudio) { audioSource.PlayOneShot(SawClip,.16f); nextContactAudio=Time.time+.08f; }
                return true; // A blocked contact still spends the normal saw interval and charges.
            }
            for (int shot=0;shot<volleys;shot++)
            {
                var volley=new Subject42.Combat.OrbitalStation.ProductionOrbitalCombatAdapter.ProjectileVolley(++volleyId);
                emittingImpact=attack.Impact.HasImpact?attack.Impact.Copy():null;
                for (int i=0;i<profile.Pellets;i++)
                {
                    float angle=profile.Pellets>1 ? Mathf.Lerp(-.5f,.5f,(float)i/(profile.Pellets-1))*profile.SpreadDegrees : 0;
                    Vector2 flight=Quaternion.Euler(0,0,angle)*direction;
                    Vector2 origin=point+direction*.3f+new Vector2(-direction.y,direction.x)*shot*.09f;
                    combat.SpawnLabDirectionalProjectile(origin,flight,profile,volley,attack.Damage,item.Id,attack.Ricochets);
                }
                emittingImpact=null;
            }
            if (ShotClip!=null) audioSource.PlayOneShot(ShotClip,.08f);
            return true;
        }
        private void ApplyImpact(ImpactCharge charge,EnemyHealth target,Vector2 point)
        {
            if (!charge.Targets.Add(target)) return;
            if (charge.Slow<1 && target!=null && !target.IsDead) ApplySlow(target,charge.Slow,charge.SlowSeconds);
            if (charge.SecondarySpent) return;
            charge.SecondarySpent=true; // Secondary damage cannot trigger another charge or a chain network.
            if (charge.Splash>0)
            {
                SplashBursts++; RingEffect(point,charge.SplashRadius,PartColor(TelekineticPart.Splash));
                foreach (var other in combat.FindNearestMany(point,charge.SplashRadius,int.MaxValue))
                    if (other!=target && other.gameObject.scene==gameObject.scene) combat.ApplyDamage(other,charge.Damage*charge.Splash,point);
            }
            if (charge.Chain>0)
            {
                foreach (var other in combat.FindNearestMany(point,charge.ChainRadius,int.MaxValue))
                {
                    if (other==target || other.gameObject.scene!=gameObject.scene) continue;
                    if (combat.ApplyDamage(other,charge.Damage*charge.Chain,point))
                    { ChainJumps++; LineEffect(point,other.transform.position,PartColor(TelekineticPart.Chain)); }
                    break;
                }
            }
        }
        private void ApplySlow(EnemyHealth target,float multiplier,float seconds)
        {
            if (!target.TryGetComponent<EnemyMovement>(out var movement)) return;
            if (!slowed.TryGetValue(target,out var state))
            {
                var marker=new GameObject("Lab slow status"); marker.transform.SetParent(target.transform,false); marker.transform.localPosition=Vector3.up*.7f;
                var sprite=marker.AddComponent<SpriteRenderer>(); ApplyIcon(sprite,TelekineticPart.Slow);
                sprite.sortingLayerName="Player"; sprite.sortingOrder=60;
                marker.transform.localScale=Vector3.one*.3f;
                state=new SlowState { Movement=movement,Marker=marker,Multiplier=1 }; slowed.Add(target,state);
            }
            state.Multiplier=Mathf.Min(state.Multiplier,multiplier); state.Until=CombatSeconds+seconds;
            movement.SetAnomalySpeedMultiplier(state.Multiplier);
        }
        private void TickEffectTimers()
        {
            for (int i=echoes.Count-1;i>=0;i--)
            {
                var echo=echoes[i]; if (echo.Due>CombatSeconds) continue;
                echoes.RemoveAt(i);
                if (EmitCaptured(echo.Attack,1)) { EchoShots++; echo.Attack.Item.Shots++; ShotsFired++; }
            }
            expiredSlows.Clear();
            foreach (var pair in slowed)
            {
                if (pair.Key!=null && !pair.Key.IsDead && pair.Value.Until>CombatSeconds) continue;
                if (pair.Value.Movement!=null) pair.Value.Movement.SetAnomalySpeedMultiplier(1);
                if (pair.Value.Marker!=null) Destroy(pair.Value.Marker);
                expiredSlows.Add(pair.Key);
            }
            foreach (var target in expiredSlows) slowed.Remove(target);
            effectVisuals.RemoveAll(go=>go==null);
        }
        private LineRenderer EffectLine(Color color)
        {
            var go=new GameObject("Lab effect pulse"); go.transform.SetParent(transform,false); effectVisuals.Add(go);
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=SpriteMaterial; line.startColor=line.endColor=color;
            line.widthMultiplier=.035f; line.sortingLayerName="Player"; line.sortingOrder=65;
            Destroy(go,.18f); return line;
        }
        private void LineEffect(Vector2 from,Vector2 to,Color color)
        { var line=EffectLine(color); line.positionCount=3; line.SetPosition(0,from); line.SetPosition(1,(from+to)*.5f+Vector2.up*.13f); line.SetPosition(2,to); }
        private void RingEffect(Vector2 point,float radius,Color color)
        {
            var line=EffectLine(color); line.positionCount=17;
            for (int i=0;i<=16;i++) { float a=i*Mathf.PI/8; line.SetPosition(i,point+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius); }
        }
        private void ClearEffectState()
        {
            echoes.Clear(); projectileCharges.Clear(); emittingImpact=null; lastBodyTarget=null;
            foreach (var state in slowed.Values)
            { if (state.Movement!=null) state.Movement.SetAnomalySpeedMultiplier(1); if (state.Marker!=null) Destroy(state.Marker); }
            slowed.Clear();
            foreach (var visual in effectVisuals) if (visual!=null) { visual.SetActive(false); Destroy(visual); }
            effectVisuals.Clear(); SplashBursts=ChainJumps=EchoShots=0;
        }
    }
}
#endif
