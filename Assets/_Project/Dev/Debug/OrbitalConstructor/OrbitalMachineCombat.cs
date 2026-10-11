#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subject42.DebugLabs.Constructor
{
    // Only scene-owned targets are queried. No weapon/loadout or production station mutation.
    public sealed class OrbitalMachineCombat
    {
        public float HitInterval=.32f, BladeDamage=12f, BridgeDamage=8f;
        public float HookHoldSeconds=.6f;
        public bool HooksEnabled=false;
        public event System.Action<int,EnemyHealth> ContactHit;
        public event System.Action<Vector2> ContactImpact;
        public EnemyHealth HeldEnemy=>hook.Target;
        public float HoldRemaining=>hook.Remaining;
        public int Grabs { get; private set; }
        private readonly OrbitalHookHold hook=new();
        private readonly Dictionary<EnemyHealth,float> nextGrab=new();
        public int ContactHits { get; private set; }
        public int BridgeHits { get; private set; }
        public int Intercepts { get; private set; }
        private readonly Dictionary<(int source,EnemyHealth target),float> nextHit=new();
        private readonly Dictionary<EnemyProjectile,Vector2> projectilePositions=new();
        private readonly Collider2D[] hits=new Collider2D[128];
        private readonly ContactFilter2D filter=new ContactFilter2D { useTriggers=true };
        private Vector2 previousCenter;
        private float previousAngle;
        public void SynchronizePose(Vector2 center,float angle)
        { previousCenter=center; previousAngle=angle; projectilePositions.Clear(); }
        public void Reset(Vector2 center,float angle)
        {
            hook.Release(); nextGrab.Clear(); Grabs=0;
            nextHit.Clear(); projectilePositions.Clear(); previousCenter=center; previousAngle=angle;
            ContactHits=BridgeHits=Intercepts=0;
        }
        public void Tick(OrbitalAssembly assembly,Scene scene,Vector2 center,float angle)
        {
            var held=hook.Target;
            if (held!=null)
            {
                var node=assembly.Find(hook.NodeId);
                if (held.IsDead || node==null || hook.Remaining<=0)
                { nextGrab[held]=Time.time+Mathf.Max(.2f,HitInterval); hook.Release(); }
                else hook.Move(World(node.Point(OrbitalPartCatalog.Get(node.Kind).Terminal),center,angle));
            }
            else hook.Release(); // Also retire captured behaviours if the enemy was destroyed externally.
            // Sample rigid motion so long blades/bridges cannot skip a target between frames.
            float turn=Mathf.DeltaAngle(previousAngle,angle);
            int steps=Mathf.Clamp(Mathf.CeilToInt((Mathf.Abs(turn)*Mathf.Deg2Rad*assembly.Reach+Vector2.Distance(center,previousCenter))/.12f),1,64);
            Physics2D.SyncTransforms();
            for (int step=1;step<=steps;step++)
            {
                float fraction=(float)step/steps;
                Vector2 origin=Vector2.Lerp(previousCenter,center,fraction);
                float rotation=previousAngle+turn*fraction;
                foreach (var node in assembly.Nodes)
                {
                    var definition=OrbitalPartCatalog.Get(node.Kind);
                    if (definition.DiskRadius>0)
                        DamageDisk(scene,node.Id,World(node.Point(definition.WorkingCenter),origin,rotation),definition.DiskRadius);
                    foreach (var segment in OrbitalPartCatalog.Get(node.Kind).Segments)
                        if (segment.Role==SegmentRole.Hook && HooksEnabled && hook.Target==null)
                            Grab(scene,node,World(node.Point(segment.A),origin,rotation),World(node.Point(segment.B),origin,rotation),segment.Width,
                                World(node.Point(OrbitalPartCatalog.Get(node.Kind).Terminal),center,angle));
                }
                if (assembly.Bridge(out var a,out var b))
                    Damage(scene,-1,World(a,origin,rotation),World(b,origin,rotation),.14f,BridgeDamage,true);
            }
            foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if (!projectile.isActiveAndEnabled || projectile.gameObject.scene!=scene) continue;
                Vector2 now=projectile.transform.position;
                Vector2 before=projectilePositions.TryGetValue(projectile,out var cached) ? cached : now-projectile.DebugTravelVelocity*Time.deltaTime;
                bool caught=false;
                float radius=.08f;
                var collider=projectile.GetComponent<Collider2D>();
                if (collider!=null) radius=Mathf.Min(.25f,Mathf.Max(collider.bounds.extents.x,collider.bounds.extents.y));
                for (int step=0;step<=steps && !caught;step++)
                {
                    float fraction=(float)step/steps;
                    Vector2 origin=Vector2.Lerp(previousCenter,center,fraction);
                    float rotation=previousAngle+turn*fraction;
                    foreach (var node in assembly.Nodes)
                        foreach (var segment in OrbitalPartCatalog.Get(node.Kind).Segments)
                            if (segment.Role==SegmentRole.Shield && OrbitalAssembly.SegmentsDistance(before,now,
                                World(node.Point(segment.A),origin,rotation),World(node.Point(segment.B),origin,rotation)) <= radius+segment.Width*.5f)
                            { caught=projectile.TryIntercept(); if (caught) { Intercepts++; break; } }
                }
                if (!caught) projectilePositions[projectile]=now;
            }
            // Dead enemies and released pooled projectiles must not retain cooldown/trajectory state.
            var dead=new List<(int source,EnemyHealth target)>();
            foreach (var key in nextHit.Keys) if (key.target==null || key.target.IsDead) dead.Add(key);
            foreach (var key in dead) nextHit.Remove(key);
            var retired=new List<EnemyProjectile>();
            foreach (var p in projectilePositions.Keys) if (p==null || !p.isActiveAndEnabled) retired.Add(p);
            foreach (var p in retired) projectilePositions.Remove(p);
            previousCenter=center; previousAngle=angle;
        }
        private static Vector2 World(Vector2 p,Vector2 center,float angle) => center+OrbitalPartCatalog.Rotate(p,angle);
        private void DamageDisk(Scene scene,int source,Vector2 center,float radius)
        {
            int count=Physics2D.OverlapCircle(center,radius,filter,hits);
            ApplyHits(scene,source,count,center,BladeDamage,false);
        }
        private void Grab(Scene scene,AssemblyNode node,Vector2 a,Vector2 b,float width,Vector2 anchor)
        {
            int count=Physics2D.OverlapCapsule((a+b)*.5f,new Vector2(Vector2.Distance(a,b)+width,width),CapsuleDirection2D.Horizontal,
                Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,filter,hits);
            for (int i=0;i<count;i++)
            {
                var health=hits[i].GetComponentInParent<EnemyHealth>();
                if (health==null || health.IsDead || health.gameObject.scene!=scene ||
                    nextGrab.TryGetValue(health,out float next) && Time.time<next) continue;
                if (hook.Begin(health,node.Id,anchor,HookHoldSeconds)) { Grabs++; break; }
            }
        }
        private void Damage(Scene scene,int source,Vector2 a,Vector2 b,float width,float damage,bool bridge)
        {
            int count=Physics2D.OverlapCapsule((a+b)*.5f,new Vector2(Vector2.Distance(a,b)+width,width),CapsuleDirection2D.Horizontal,
                Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,filter,hits);
            ApplyHits(scene,source,count,(a+b)*.5f,damage,bridge);
        }
        private void ApplyHits(Scene scene,int source,int count,Vector2 center,float damage,bool bridge)
        {
            for (int i=0;i<count;i++)
            {
                var health=hits[i].GetComponentInParent<EnemyHealth>();
                if (health==null || health.IsDead || health.gameObject.scene!=scene) continue;
                var key=(source,health);
                if (nextHit.TryGetValue(key,out float next) && Time.time<next) continue;
                nextHit[key]=Time.time+Mathf.Max(.05f,HitInterval);
                Vector2 point=hits[i].ClosestPoint(center);
                health.TakeDamage(damage,point);
                if (bridge) BridgeHits++;
                else { ContactHits++; ContactHit?.Invoke(source,health); ContactImpact?.Invoke(point); }
            }
        }
    }
}
#endif
