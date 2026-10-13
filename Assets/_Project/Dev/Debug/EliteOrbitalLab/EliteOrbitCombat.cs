#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.DebugLabs.EliteOrbit
{
    // A lab-owned collision policy plugged into the existing adapter, not a second player weapon system.
    public sealed class EliteOrbitCombat : IOrbitalCombatLabInterceptor
    {
        private readonly EliteOrbitalLab lab;
        private readonly Dictionary<OrbitalModuleRuntime,Pose> previous=new();
        private readonly Dictionary<(OrbitalModuleRuntime module,EnemyHealth enemy),float> nextHit=new();
        private Vector2 previousEliteCenter;
        private float previousElitePhase;
        private bool hasElitePose;
        private Vector2 projectileFrameCenter;
        private float projectileFramePhase;
        private struct Pose { public Vector2 Center; public float Phase; public Vector3 Position; public Quaternion Rotation; }
        public EliteOrbitCombat(EliteOrbitalLab owner) { lab=owner; }
        public void Reset() { previous.Clear(); nextHit.Clear(); hasElitePose=false; }
        public bool HandlesContact(OrbitalModuleRuntime module) => module.Kind==OrbitalModuleKind.LaserSword &&
            module.CurrentMount!=null && module.CurrentMount.Transform.gameObject.scene==lab.gameObject.scene;
        public bool TryResolveDamage(EnemyHealth target,float damage,Vector2 point,out bool applied)
        {
            applied=false;
            var elite=lab.Elite;
            if (elite==null || elite.Health.IsDead) return false;
            if (target==elite.Health)
            { target.TakeDamage(damage*elite.DamageMultiplier,point); applied=true; return true; }
            foreach (var node in elite.Nodes)
                if (target==node.Health) { if (!node.Destroyed) { target.TakeDamage(damage,point); applied=true; } return true; }
            return false;
        }
        public bool InterceptProjectile(EnemyHealth target,Vector2 from,Vector2 to,float damage,Vector2 incomingDirection)
        {
            var elite=lab.Elite;
            if (elite==null || elite.Ring==null || target==null || target.gameObject.scene!=lab.gameObject.scene) return false;
            float best=float.PositiveInfinity; Vector2 point=to; EnemyHealth hit=null; bool block=false;
            if (EliteOrbitGeometry.SegmentCircle(from,to,elite.Center,elite.BodyRadius+.07f,out float bodyTime))
            { best=bodyTime; hit=elite.Health; point=Vector2.Lerp(from,to,best); }
            foreach (var node in elite.Nodes)
                if (!node.Destroyed && EliteOrbitGeometry.SegmentCircle(from,to,node.Position,EliteOrbitEnemy.Node.Radius+.07f,out float time) && time<best)
                { best=time; hit=node.Health; point=Vector2.Lerp(from,to,time); }
            if (elite.HasShield)
            {
                Vector2 before=hasElitePose ? projectileFrameCenter : elite.Center;
                float phase=hasElitePose ? projectileFramePhase : elite.Phase;
                float turn=Mathf.DeltaAngle(phase,elite.Phase);
                int steps=Mathf.Clamp(Mathf.CeilToInt((Vector2.Distance(from,to)+Mathf.Abs(turn)*Mathf.Deg2Rad*elite.Preset.Radius)/.045f),1,256);
                for (int step=0;step<=steps;step++)
                {
                    float time=(float)step/steps; if (time>best) break;
                    Vector2 sample=Vector2.Lerp(from,to,time),center=Vector2.Lerp(before,elite.Center,time);
                    for (int section=0;section<EliteOrbitEnemy.PlateSections;section++)
                    {
                        elite.PlateSection(section,phase+turn*time,center,out var a,out var b);
                        if (EliteOrbitGeometry.Distance(sample,a,b)>elite.Preset.ShieldThickness*.5f+.07f) continue;
                        best=time; hit=null; block=true; point=sample; break;
                    }
                    if (block) break;
                }
            }
            if (float.IsPositiveInfinity(best)) return false;
            if (block) elite.Block(point);
            else if (hit!=null && !hit.IsDead) TryResolveDamage(hit,damage,point,out _);
            return true; // The production adapter releases the projectile through its existing lifecycle.
        }
        public void TickContacts(float deltaTime)
        {
            if (lab.Station==null || lab.Player==null) return;
            var elite=lab.Elite;
            Vector2 beforeCenter=hasElitePose ? previousEliteCenter : elite!=null ? elite.Center : Vector2.zero;
            float beforePhase=hasElitePose ? previousElitePhase : elite!=null ? elite.Phase : 0;
            projectileFrameCenter=beforeCenter; projectileFramePhase=beforePhase;
            var enemies=EnemyHealth.ActiveInstances.Where(e=>e!=null && !e.IsDead && e.gameObject.scene==lab.gameObject.scene).ToArray();
            foreach (var module in lab.Station.Modules)
            {
                if (!HandlesContact(module)) continue;
                var mount=module.CurrentMount;
                var view=mount.Transform.GetComponentInChildren<OrbitalModuleView>();
                if (view==null) continue;
                Pose now=new() { Position=view.transform.position,Rotation=view.transform.rotation,Phase=mount.Ring.Phase,
                    Center=(Vector2)view.transform.position-(Vector2)mount.Ring.Geometry.PositionDegrees(mount.Ring.Phase+mount.LocalPhase,mount.Ring.Radius) };
                Pose old=previous.TryGetValue(module,out var saved) ? saved : now;
                float turn=Mathf.DeltaAngle(old.Phase,now.Phase);
                float hostileTurn=elite!=null ? Mathf.DeltaAngle(beforePhase,elite.Phase) : 0;
                int steps=Mathf.Clamp(Mathf.CeilToInt((Vector2.Distance(old.Center,now.Center)+Mathf.Abs(turn)*Mathf.Deg2Rad*mount.Ring.Radius+
                    Mathf.Abs(hostileTurn)*Mathf.Deg2Rad*(elite!=null ? elite.Preset.Radius : 0))/.055f),1,256);
                bool shieldBlocked=false; Vector2 blockPoint=now.Position;
                var touched=new HashSet<EnemyHealth>();
                for (int step=0;step<=steps;step++)
                {
                    float fraction=(float)step/steps;
                    view.transform.position=Vector2.Lerp(old.Center,now.Center,fraction)+(Vector2)mount.Ring.Geometry.PositionDegrees(old.Phase+turn*fraction+mount.LocalPhase,mount.Ring.Radius);
                    view.transform.rotation=Quaternion.Slerp(old.Rotation,now.Rotation,fraction);
                    Vector2 hostileCenter=elite!=null ? Vector2.Lerp(beforeCenter,elite.Center,fraction) : Vector2.zero;
                    if (elite!=null && elite.HasShield && Vector2.Distance(view.transform.position,hostileCenter)>=elite.Preset.Radius-elite.Preset.ShieldThickness)
                        for (int section=0;section<EliteOrbitEnemy.PlateSections;section++)
                        {
                            elite.PlateSection(section,beforePhase+hostileTurn*fraction,hostileCenter,out var a,out var b);
                            foreach (var sprite in view.Sprites)
                                if (sprite!=view.Halo && EliteOrbitGeometry.SpriteBox(sprite,a,b,elite.Preset.ShieldThickness))
                                { shieldBlocked=true; blockPoint=(a+b)*.5f; }
                        }
                    foreach (var enemy in enemies)
                    {
                        float radius; Vector2 center=enemy.transform.position;
                        var node=elite?.Nodes.FirstOrDefault(n=>n.Health==enemy);
                        if (node!=null) radius=EliteOrbitEnemy.Node.Radius;
                        else
                        {
                            var collider=enemy.GetComponent<Collider2D>();
                            radius=collider!=null ? Mathf.Max(collider.bounds.extents.x,collider.bounds.extents.y) : .3f;
                            if (collider!=null) center=collider.bounds.center;
                            if (elite!=null && enemy==elite.Health) center=hostileCenter;
                        }
                        if (module.HasBodyContact(center,radius)) touched.Add(enemy);
                    }
                }
                view.transform.SetPositionAndRotation(now.Position,now.Rotation);
                previous[module]=now;
                if (shieldBlocked && elite!=null && CanHit(module,elite.Health)) { elite.Block(blockPoint); nextHit[(module,elite.Health)]=Time.time+.2f; }
                foreach (var enemy in touched)
                {
                    if (enemy==null || enemy.IsDead || (elite!=null && enemy==elite.Health && shieldBlocked) || !CanHit(module,enemy)) continue;
                    lab.Station.Combat.ApplyDamage(enemy,OrbitalLaserSwordModule.BaseDamage*mount.Ring.PowerMultiplier,now.Position);
                    nextHit[(module,enemy)]=Time.time+.32f;
                    module.TriggerUpgradePresentation();
                }
            }
            if (elite!=null && elite.Ring!=null)
            {
                float turn=Mathf.DeltaAngle(beforePhase,elite.Phase);
                int steps=Mathf.Clamp(Mathf.CeilToInt((Vector2.Distance(beforeCenter,elite.Center)+Mathf.Abs(turn)*Mathf.Deg2Rad*elite.Preset.Radius)/.06f),1,128);
                for (int i=0;i<=steps;i++)
                    if (elite.BladeTouches(lab.Player,beforePhase+turn*i/steps,Vector2.Lerp(beforeCenter,elite.Center,(float)i/steps)))
                    { elite.DamagePlayer(lab.Player); break; }
                previousEliteCenter=elite.Center; previousElitePhase=elite.Phase; hasElitePose=true;
            }
            else hasElitePose=false;
        }
        private bool CanHit(OrbitalModuleRuntime module,EnemyHealth enemy) => !nextHit.TryGetValue((module,enemy),out float next) || Time.time>=next;
    }
}
#endif
