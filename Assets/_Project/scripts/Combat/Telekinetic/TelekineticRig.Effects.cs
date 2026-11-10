using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        internal const int ChainMaxJumps = 3;
        private const float ChainTravelSeconds = .15f;
        private const float ChainFadeSeconds = .14f;
        private const float ChainArcHeight = 1.1f;
        private static readonly Color ChainTail = new(.62f, .49f, 1f, .96f);
        private static readonly Color ChainHead = new(.82f, .99f, 1f);

        private sealed class ImpactCharge
        {
            internal float SplashDamage,ChainDamage,SplashRadius,ChainRadius,Slow=1,SlowSeconds;
            internal bool SplashSpent, ChainSpent;
            internal readonly HashSet<EnemyHealth> SlowedTargets=new();
            internal ImpactCharge Copy() => new() { SplashDamage=SplashDamage,ChainDamage=ChainDamage,SplashRadius=SplashRadius,ChainRadius=ChainRadius,Slow=Slow,SlowSeconds=SlowSeconds };
            internal bool HasImpact => SplashDamage>0 || ChainDamage>0 || Slow<1;
        }
        private sealed class CapturedAttack
        {
            internal TelekineticItem Item;
            internal OrbitalWeaponSettings Profile;
            internal float Damage, AttacksPerSecond;
            internal int Ricochets,Volleys,Echoes;
            internal ImpactCharge Impact;
        }
        private sealed class DelayedEcho
        {
            internal CapturedAttack Attack;
            internal Vector2 Point, Direction;
            internal float Due;
        }
        private sealed class SlowState { internal EnemyAnomalyEffects Effects; internal GameObject Marker; internal float Until,Multiplier; }
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
        public float CriticalFactor(TelekineticItem item) => ActiveModifiers(item).CritMultiplier;
        public float BaseDamage(TelekineticItem item) => Settings(item).Damage*station.GetModuleMetaDamageMultiplier(Settings(item).Kind)*
            (item.Kind==TelekineticPart.Saw ? station.State.ContactDamageMultiplier : station.State.ShootingDamageMultiplier)*(1+.25f*(item.Level-1));
        public static bool Affects(TelekineticPart module,TelekineticItem weapon) => weapon.IsWeapon &&
            (weapon.Kind!=TelekineticPart.Saw || module!=TelekineticPart.RIC && module!=TelekineticPart.Pierce);

        private void BindEffectCallbacks()
        {
            combat.Hit+=RememberBodyTarget;
            combat.ProjectileSpawned+=CaptureProjectile;
            combat.ProjectileContact+=ProjectileContact;
            combat.ProjectileEnded+=ProjectileEnded;
        }
        private void RememberBodyTarget(EnemyHealth target,float damage) => lastBodyTarget=target;
        private void CaptureProjectile(long id,int weapon,Vector2 direction)
        { if (emittingImpact!=null && emittingImpact.HasImpact) projectileCharges.Add(id,emittingImpact); }
        private void ProjectileEnded(long id,bool cancelled) => projectileCharges.Remove(id);
        private void ProjectileContact(long id,float damage,bool killed,bool shield)
        {
            // The shared adapter raises Hit immediately before its sequence-tagged body contact.
            // Shield contacts raise no Hit; rejected damage must never use a stale target.
            if (Suppressed || shield || damage<=0 || !projectileCharges.TryGetValue(id,out var charge) || lastBodyTarget==null) return;
            var target=lastBodyTarget;
            ApplyImpact(charge,target,target.transform.position,killed);
        }
        private CapturedAttack CaptureAttack(TelekineticItem item) => DescribeAttack(item, ActiveModifiers(item));

        private CapturedAttack DescribeAttack(TelekineticItem item, WeaponModifiers modifiers)
        {
            var s=Settings(item);
            // Copy only this attack. Shared authored settings and live projectiles are never changed.
            var profile=new OrbitalWeaponSettings
            {
                Kind=s.Kind,Category=s.Category,WeaponVisual=s.WeaponVisual,ProjectileVisual=s.ProjectileVisual,
                Presentation=s.Presentation,ProjectileVisualScale=s.ProjectileVisualScale,ProjectileTrailSeconds=s.ProjectileTrailSeconds,
                Damage=s.Damage,FireInterval=s.FireInterval,Range=s.Range,ProjectileSpeed=s.ProjectileSpeed,ProjectileRadius=s.ProjectileRadius,
                BurstShots=s.BurstShots,BurstInterval=s.BurstInterval,Pellets=s.Pellets,SpreadDegrees=s.SpreadDegrees,
                BodyPierces=s.BodyPierces+Mathf.RoundToInt(Modules.PierceBodies*modifiers.PierceStrength),
                BypassShields=s.BypassShields || modifiers.BypassShields,
                Knockback=Mathf.Min(5f,s.Knockback+modifiers.Knockback),Color=s.Color,ContactRadius=s.ContactRadius,
                ContactHitInterval=s.ContactHitInterval,SpinDegreesPerSecond=s.SpinDegreesPerSecond
            };
            float damage = BaseDamage(item) * modifiers.DamageMultiplier * modifiers.CritMultiplier;
            return new CapturedAttack
            {
                Item=item,Profile=profile,Damage=damage,AttacksPerSecond=modifiers.RateMultiplier/BaseAttackInterval(item),
                Ricochets=modifiers.Ricochets,Volleys=1+modifiers.ExtraAttacks,Echoes=modifiers.Echoes,
                Impact=new ImpactCharge { SplashDamage=damage*modifiers.SplashFraction,
                    ChainDamage=damage*modifiers.ChainFraction,
                    SplashRadius=Modules.SplashRadius,ChainRadius=Modules.ChainRadius,
                    Slow=Mathf.Max(.1f,1-modifiers.SlowReduction),SlowSeconds=Modules.SlowSeconds }
            };
        }
        private bool GatherContactTargets(Vector2 point)
        {
            contactTargets.Clear(); overlaps.Clear();
            Physics2D.OverlapCircle(point,Saw.ContactRadius*station.State.ContactSizeMultiplier,ContactFilter2D.noFilter,overlaps);
            foreach (var hit in overlaps)
            {
                var target=hit.GetComponentInParent<EnemyHealth>();
                if (target!=null && !target.IsDead && target.isActiveAndEnabled && target.gameObject.scene==gameObject.scene) contactTargets.Add(target);
            }
            return contactTargets.Count>0;
        }
        private bool EmitCaptured(CapturedAttack attack,int volleys,Vector2 point,Vector2 direction,bool contactsReady=false)
        {
            var item=attack.Item; var profile=attack.Profile;
            if (item.Kind==TelekineticPart.Saw)
            {
                if (!contactsReady && !GatherContactTargets(point)) return false;
                var impact=attack.Impact.Copy();
                foreach (var target in contactTargets)
                {
                    if (target==null || target.IsDead) continue;
                    if (!profile.BypassShields && target.TryGetComponent<EnemyDirectionalShield>(out var shield) && shield.BlockDiskContact(point,Saw.ContactRadius*station.State.ContactSizeMultiplier)) continue;
                    float before=target.CurrentHealth;
                    for (int shot=0;shot<volleys && !target.IsDead;shot++)
                    {
                        if (profile.BypassShields) target.TakeDamage(attack.Damage,point); else combat.ApplyDamage(target,attack.Damage,point);
                    }
                    if (target.CurrentHealth<before) ApplyImpact(impact,target,point,target.IsDead);
                    if (target.CurrentHealth<before && profile.Knockback>0 && !target.IsDead && target.TryGetComponent<EnemyMovement>(out var movement))
                        movement.ApplyKnockback(((Vector2)target.transform.position-point).normalized,profile.Knockback);
                }
                if (Time.time>=nextContactAudio) { audioSource.PlayOneShot(SawClip,.16f); nextContactAudio=Time.time+.08f; }
                return true; // A blocked contact still spends the normal saw interval.
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
                    combat.SpawnRigDirectionalProjectile(origin,flight,profile,volley,attack.Damage,item.Id,attack.Ricochets);
                }
                emittingImpact=null;
            }
            if (ShotClip!=null) audioSource.PlayOneShot(ShotClip,.08f);
            return true;
        }
        private void ApplyImpact(ImpactCharge charge,EnemyHealth target,Vector2 point,bool killed)
        {
            if (Suppressed) return;
            if (charge.Slow<1 && !killed && charge.SlowedTargets.Add(target)) ApplySlow(target,charge.Slow,charge.SlowSeconds);
            if (charge.SplashDamage>0 && !charge.SplashSpent)
            {
                charge.SplashSpent = true;
                SplashBursts++; RingEffect(point,charge.SplashRadius,PartColor(TelekineticPart.Splash));
                foreach (var other in combat.FindNearestMany(point,charge.SplashRadius,int.MaxValue))
                    if (other!=target && other.gameObject.scene==gameObject.scene) combat.ApplyDamage(other,charge.SplashDamage,point);
            }
            // All pellets and later contacts from this volley share the same charge.
            if (killed && charge.ChainDamage>0 && !charge.ChainSpent)
            {
                charge.ChainSpent = true;
                StartCoroutine(ApplyChain(charge, target.transform.position));
            }
        }

        private IEnumerator ApplyChain(ImpactCharge charge, Vector2 point)
        {
            var scene = gameObject.scene;
            var next = combat.FindNearest(point, charge.ChainRadius, scene);
            if (next == null) yield break;

            var line = EffectLine(ChainTail, .11f, worldSorted: true);
            line.endColor = ChainHead;
            var points = new Vector3[7];
            line.positionCount = points.Length;
            Vector2 endpoint = point;
            for (int hop = 0; hop < ChainMaxJumps; hop++)
            {
                float startedAt = Time.time;
                bool retargeted = false;
                while (true)
                {
                    if (!ChainArcActive(line)) yield break;
                    if (next == null || !next.isActiveAndEnabled || next.IsDead)
                    {
                        next = retargeted ? null : combat.FindNearest(point, charge.ChainRadius, scene);
                        if (next == null)
                        {
                            Destroy(line.gameObject);
                            yield break;
                        }
                        retargeted = true;
                    }

                    endpoint = next.transform.position;
                    float progress = Mathf.Clamp01((Time.time - startedAt) / ChainTravelSeconds);
                    line.widthMultiplier = Mathf.Lerp(.11f, .17f, progress * progress);
                    DrawChainArc(line, points, point, endpoint, 0f,
                        Mathf.Lerp(.32f, 1f, progress), progress < .55f ? 1f : -1f);
                    if (progress >= 1f) break;
                    yield return null;
                }

                if (!combat.ApplyDamage(next, charge.ChainDamage, point)) break;
                ChainJumps++;
                if (!next.IsDead || hop + 1 == ChainMaxJumps) break;

                var following = combat.FindNearest(endpoint, charge.ChainRadius, scene);
                if (following == null) break;
                point = endpoint;
                next = following;
                yield return null;
            }

            float fadeAt = Time.time;
            for (float age = 0; age < ChainFadeSeconds; age = Time.time - fadeAt)
            {
                if (!ChainArcActive(line)) yield break;
                if (next != null && next.isActiveAndEnabled && !next.IsDead) endpoint = next.transform.position;
                float progress = age / ChainFadeSeconds;
                line.startColor = new Color(ChainTail.r, ChainTail.g, ChainTail.b, ChainTail.a * (1f - progress));
                line.endColor = new Color(ChainHead.r, ChainHead.g, ChainHead.b, 1f - progress);
                line.widthMultiplier = Mathf.Lerp(.17f, .03f, progress);
                DrawChainArc(line, points, point, endpoint, Mathf.SmoothStep(0f, 1f, progress), 1f, -1f);
                yield return null;
            }
            if (line != null) Destroy(line.gameObject);
        }

        private bool ChainArcActive(LineRenderer line) => line != null && line.gameObject.activeSelf && !Suppressed;
        private void ApplySlow(EnemyHealth target,float multiplier,float seconds)
        {
            if (!target.TryGetComponent<EnemyMovement>(out var movement)) return;
            if (!slowed.TryGetValue(target,out var state))
            {
                var marker=new GameObject("Telekinetic slow status"); marker.transform.SetParent(target.transform,false); marker.transform.localPosition=Vector3.up*.7f;
                var sprite=marker.AddComponent<SpriteRenderer>(); ApplyIcon(sprite,TelekineticPart.Slow);
                sprite.sortingLayerName="Player"; sprite.sortingOrder=60;
                marker.transform.localScale=Vector3.one*.3f;
                state=new SlowState { Effects=EnemyAnomalyEffects.GetOrCreate(target),Marker=marker,Multiplier=1 }; slowed.Add(target,state);
            }
            state.Multiplier=Mathf.Min(state.Multiplier,multiplier); state.Until=CombatSeconds+seconds;
            state.Effects.EnterZone(this,state.Multiplier,new Color(.65f,.85f,1));
        }
        private void TickEffectTimers()
        {
            for (int i=echoes.Count-1;i>=0;i--)
            {
                var echo=echoes[i]; if (echo.Due>CombatSeconds) continue;
                echoes.RemoveAt(i);
                if (EmitCaptured(echo.Attack,1,echo.Point,echo.Direction)) { EchoShots++; echo.Attack.Item.Shots++; ShotsFired++; }
            }
            expiredSlows.Clear();
            foreach (var pair in slowed)
            {
                if (pair.Key!=null && !pair.Key.IsDead && pair.Value.Until>CombatSeconds) continue;
                if (pair.Value.Effects!=null) pair.Value.Effects.ExitZone(this);
                if (pair.Value.Marker!=null) Destroy(pair.Value.Marker);
                expiredSlows.Add(pair.Key);
            }
            foreach (var target in expiredSlows) slowed.Remove(target);
            effectVisuals.RemoveAll(go=>go==null);
        }
        private LineRenderer EffectLine(Color color, float width = .035f, bool worldSorted = false)
        {
            var go = new GameObject("Telekinetic effect pulse");
            // A chain crosses different actors, so it must sort outside the player's group.
            if (!worldSorted) go.transform.SetParent(transform, false);
            else if (go.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            effectVisuals.Add(go);
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=SpriteMaterial; line.startColor=line.endColor=color;
            line.widthMultiplier=width; line.sortingLayerName="Player"; line.sortingOrder=65;
            return line;
        }

        private static void DrawChainArc(LineRenderer line, Vector3[] points, Vector2 from, Vector2 to,
            float tail, float head, float polarity)
        {
            from.y += ChainArcHeight;
            to.y += ChainArcHeight;
            Vector2 delta = to - from;
            Vector2 kink = Vector2.Perpendicular(delta.normalized) * Mathf.Clamp(delta.magnitude * .18f, .1f, .23f);
            for (int i = 0; i < points.Length; i++)
            {
                float t = Mathf.Lerp(tail, head, (float)i / (points.Length - 1));
                float crown = 4f * t * (1f - t);
                float zigzag = i == 0 || i == points.Length - 1 ? 0f : (i % 2 == 0 ? polarity : -polarity);
                points[i] = from + delta * t + (Vector2.up * .22f + kink * zigzag) * crown;
            }
            line.SetPositions(points);
        }
        private void RingEffect(Vector2 point,float radius,Color color)
        {
            var line=EffectLine(color); line.positionCount=17;
            for (int i=0;i<=16;i++) { float a=i*Mathf.PI/8; line.SetPosition(i,point+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius); }
            Destroy(line.gameObject,.18f);
        }
        private void ClearEffectState()
        {
            echoes.Clear(); projectileCharges.Clear(); emittingImpact=null; lastBodyTarget=null;
            foreach (var state in slowed.Values)
            { if (state.Effects!=null) state.Effects.ExitZone(this); if (state.Marker!=null) Destroy(state.Marker); }
            slowed.Clear();
            foreach (var visual in effectVisuals) if (visual!=null) { visual.SetActive(false); Destroy(visual); }
            effectVisuals.Clear(); SplashBursts=ChainJumps=EchoShots=0;
        }
    }
}
