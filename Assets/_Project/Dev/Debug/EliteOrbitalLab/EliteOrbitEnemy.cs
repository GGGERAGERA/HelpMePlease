#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.DebugLabs.EliteOrbit
{
    [DefaultExecutionOrder(-150)]
    public sealed class EliteOrbitEnemy : MonoBehaviour
    {
        public sealed class Node
        {
            public EnemyHealth Health;
            public OrbitalMountRuntime Mount;
            public SpriteRenderer Fill;
            public bool Destroyed;
            public Vector2 Position=>Mount.Transform.position;
            public const float Radius=.31f;
        }
        public EliteOrbitPreset Preset;
        public EnemyHealth Health { get; private set; }
        public OrbitalRingRuntime Ring { get; private set; }
        public IReadOnlyList<Node> Nodes=>nodes;
        public int LiveNodes { get { int count=0; foreach (var node in nodes) if (!node.Destroyed) count++; return count; } }
        public float DamageMultiplier=>Mathf.Max(.25f,1-LiveNodes*(Preset!=null ? Preset.ReductionPerNode : 0));
        public int Blocks { get; private set; }
        public bool DebugFreezeOrbit;
        public event Action<EliteFeedback,Vector2> Feedback;
        public const int PlateSections=12;
        private readonly List<Node> nodes=new();
        private readonly List<OrbitalModuleView> blades=new();
        private readonly List<Transform> plateRoots=new();
        private readonly List<int> plateSlots=new();
        private readonly List<SpriteRenderer> nodeIndicators=new();
        private SpriteRenderer hpFill;
        private Transform orbitRoot,hud;
        private float nextBladeHit;
        public Vector2 Center=>transform.position;
        public float BodyRadius
        {
            get { var collider=GetComponent<Collider2D>(); return collider!=null ? Mathf.Max(collider.bounds.extents.x,collider.bounds.extents.y) : .4f; }
        }
        public float Phase=>Ring?.Phase ?? 0;
        public bool HasShield=>plateSlots.Count>0;
        private void Awake()
        {
            Health=GetComponent<EnemyHealth>();
            Health.HitFeedback+=BodyHit; Health.DeathFeedback+=BodyDied;
        }
        public void Initialize(EliteOrbitPreset preset)
        {
            Preset=preset;
            var policy=GetComponent<CombatFeelTestDummy>(); policy.Initialize(null); policy.Invulnerable=false;
            Health.SetRuntimeMaxHealth(preset.BodyHealth);
            foreach (var movement in GetComponents<EnemyMovement>()) movement.SetSpeedMultiplier(preset.MovementMultiplier);
            orbitRoot=new GameObject("Elite ORBITAL / shared ring and mounting points").transform;
            orbitRoot.SetParent(transform,false);
            Vector3 scale=transform.lossyScale;
            orbitRoot.localScale=new Vector3(1/scale.x,1/scale.y,1);
            var config=OrbitalPresentationConfig.Active;
            var state=new OrbitalRingState { StableRingId=1,Order=0,Radius=preset.Radius-config.RingRadiusPadding,
                BaseRotationSpeed=preset.RotationSpeed,Direction=1,MountCapacity=preset.MountCount,MountCount=preset.MountCount,PowerMultiplier=1 };
            Ring=new OrbitalRingRuntime(state,orbitRoot,config.VisualMaterial,config.PixelSprite,false,OrbitalPathGeometry.Circle);
            foreach (var slot in preset.Slots)
            {
                var mount=Ring.Mounts[slot.MountIndex]; mount.SetVisualState(OrbitalMountRuntime.VisualState.Occupied);
                if (slot.Kind==EliteModuleKind.ArmorPlate) CreatePlate(mount,slot.MountIndex);
                if (slot.Kind==EliteModuleKind.ContactBlade) CreateBlade(mount);
                if (slot.Kind==EliteModuleKind.ProtectionNode) CreateNode(mount);
            }
            hud=new GameObject("Elite health and protection count").transform; hud.SetParent(orbitRoot,false); hud.localPosition=new Vector3(0,1.15f,0);
            Pixel("HP background",hud,Vector2.zero,new Vector2(1.55f,.12f),new Color(.12f,.13f,.18f));
            hpFill=Pixel("HP",hud,Vector2.zero,new Vector2(1.5f,.08f),new Color(1,.28f,.16f));
            for (int i=0;i<nodes.Count;i++) nodeIndicators.Add(Pixel("Protection node "+(i+1),hud,new Vector2(-.3f+i*.3f,.2f),new Vector2(.15f,.12f),new Color(.95f,.3f,.65f)));
            // A silhouette cue independent of colour: three pointed rank tabs above the original head.
            for (int i=-1;i<=1;i++)
            { var tab=Pixel("Elite rank tab",orbitRoot,new Vector2(i*.18f,.75f),new Vector2(.12f,.28f),new Color(1,.45f,.17f)); tab.transform.localRotation=Quaternion.Euler(0,0,i*20); }
            Health.NotifySpawnConfigured(); UpdateHud();
        }
        public static SpriteRenderer Pixel(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position;
            var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=OrbitalPresentationConfig.Active.PixelSprite;
            Vector2 spriteSize=renderer.sprite.bounds.size;
            go.transform.localScale=new Vector3(size.x/spriteSize.x,size.y/spriteSize.y,1);
            renderer.color=color; renderer.sortingLayerName="Player"; renderer.sortingOrder=110; return renderer;
        }
        private void CreatePlate(OrbitalMountRuntime mount,int slot)
        {
            var root=new GameObject("Wide enemy armor plate / open sector").transform; root.SetParent(mount.Transform,false);
            plateRoots.Add(root); plateSlots.Add(slot);
            for (int i=0;i<PlateSections;i++)
            {
                float a=(-Preset.ShieldArc*.5f+Preset.ShieldArc*i/PlateSections)*Mathf.Deg2Rad;
                float b=(-Preset.ShieldArc*.5f+Preset.ShieldArc*(i+1)/PlateSections)*Mathf.Deg2Rad;
                Vector2 first=new Vector2(Mathf.Cos(a)-1,Mathf.Sin(a))*Preset.Radius;
                Vector2 second=new Vector2(Mathf.Cos(b)-1,Mathf.Sin(b))*Preset.Radius;
                var plate=Pixel("Physical plate section",root,(first+second)*.5f,new Vector2(Vector2.Distance(first,second)+.025f,Preset.ShieldThickness),new Color(.82f,.28f,.15f));
                plate.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(second.y-first.y,second.x-first.x)*Mathf.Rad2Deg);
            }
        }
        private void CreateBlade(OrbitalMountRuntime mount)
        {
            var visual=Instantiate(OrbitalPresentationConfig.Active.SawPrefab,mount.Transform,false);
            visual.name="Hostile contact blade"; visual.transform.localScale*=.9f;
            var view=visual.GetComponent<OrbitalModuleView>();
            view.Halo.enabled=false;
            foreach (var sprite in view.Sprites) if (sprite!=view.Halo) { sprite.color=new Color(1,.3f,.15f); sprite.sortingLayerName="Player"; sprite.sortingOrder=112; }
            foreach (var particle in view.Particles) { particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); particle.gameObject.SetActive(false); }
            var teeth=new List<SpriteRenderer>(view.Sprites);
            for (int side=-1;side<=1;side+=2)
            {
                var tooth=Pixel("Hostile blade hooked guard",view.Body,new Vector2(side*.16f,-.22f),new Vector2(.09f,.25f),new Color(.8f,.16f,.12f));
                tooth.transform.localRotation=Quaternion.Euler(0,0,-side*30); teeth.Add(tooth);
            }
            view.Sprites=teeth.ToArray();
            blades.Add(view);
        }
        private void CreateNode(OrbitalMountRuntime mount)
        {
            var go=new GameObject("Destructible protection satellite"); go.transform.SetParent(mount.Transform,false);
            var node=new Node { Mount=mount };
            var health=go.AddComponent<EnemyHealth>(); node.Health=health;
            var policy=go.AddComponent<CombatFeelTestDummy>(); policy.Initialize(null); policy.Invulnerable=false;
            health.SetRuntimeMaxHealth(Preset.NodeHealth);
            var shell=Pixel("Faceted hostile node",go.transform,Vector2.zero,new Vector2(.43f,.43f),new Color(.9f,.2f,.6f)); shell.transform.localRotation=Quaternion.Euler(0,0,45);
            Pixel("Node center",go.transform,Vector2.zero,new Vector2(.19f,.19f),new Color(1,.75f,.85f));
            Pixel("Node HP background",go.transform,new Vector2(0,.4f),new Vector2(.5f,.06f),new Color(.12f,.12f,.18f));
            node.Fill=Pixel("Node HP",go.transform,new Vector2(0,.4f),new Vector2(.5f,.045f),new Color(1,.3f,.6f));
            health.HitFeedback+=(damage,point,critical)=>Feedback?.Invoke(EliteFeedback.NodeHit,point);
            health.OnHealthChanged.AddListener((current,maximum)=> { if (node.Fill!=null) node.Fill.transform.localScale=new Vector3(.5f*Mathf.Clamp01(current/maximum)/node.Fill.sprite.bounds.size.x,.045f/node.Fill.sprite.bounds.size.y,1); });
            health.DeathFeedback+=()=>
            {
                node.Destroyed=true; Feedback?.Invoke(EliteFeedback.NodeDestroyed,node.Position);
                node.Mount.SetVisualState(OrbitalMountRuntime.VisualState.Normal);
                health.gameObject.SetActive(false); UpdateHud();
            };
            nodes.Add(node); health.NotifySpawnConfigured();
        }
        public bool PlateSection(int index,float phase,Vector2 center,out Vector2 a,out Vector2 b)
        {
            a=b=default; if (!HasShield || Ring==null) return false;
            float local=Ring.Mounts[plateSlots[0]].LocalPhase;
            float first=phase+local-Preset.ShieldArc*.5f+Preset.ShieldArc*index/PlateSections;
            float second=phase+local-Preset.ShieldArc*.5f+Preset.ShieldArc*(index+1)/PlateSections;
            a=center+(Vector2)Ring.Geometry.PositionDegrees(first,Preset.Radius); b=center+(Vector2)Ring.Geometry.PositionDegrees(second,Preset.Radius); return true;
        }
        public void Block(Vector2 point)
        { Blocks++; Feedback?.Invoke(EliteFeedback.Block,point); }
        private void BodyHit(float damage,Vector2 point,bool critical) { Feedback?.Invoke(EliteFeedback.BodyHit,point); UpdateHud(); }
        private void UpdateHud()
        {
            if (hpFill!=null) hpFill.transform.localScale=new Vector3(1.5f*Mathf.Clamp01(Health.CurrentHealth/Health.MaxHealth)/hpFill.sprite.bounds.size.x,.08f/hpFill.sprite.bounds.size.y,1);
            for (int i=0;i<nodeIndicators.Count;i++) nodeIndicators[i].color=nodes[i].Destroyed ? new Color(.18f,.2f,.24f) : new Color(.95f,.3f,.65f);
        }
        private void Update()
        {
            if (Ring==null || Health.IsDead) return;
            Ring.Tick(DebugFreezeOrbit ? 0 : Time.deltaTime);
            for (int i=0;i<plateRoots.Count;i++) plateRoots[i].localRotation=Quaternion.Euler(0,0,Phase+Ring.Mounts[plateSlots[i]].LocalPhase);
            foreach (var blade in blades)
            {
                var mount=blade.transform.parent;
                float angle=Mathf.Atan2(mount.localPosition.y,mount.localPosition.x)*Mathf.Rad2Deg;
                blade.transform.rotation=Quaternion.Euler(0,0,angle+90+OrbitalPresentationConfig.Active.SawTangentOffset);
            }
        }
        public bool BladeTouches(PlayerHealth player,float phase,Vector2 center)
        {
            if (player==null || Ring==null) return false;
            var playerCollider=player.GetComponent<Collider2D>(); float radius=playerCollider!=null ? Mathf.Max(playerCollider.bounds.extents.x,playerCollider.bounds.extents.y) : .25f;
            Vector2 playerCenter=playerCollider!=null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;
            foreach (var blade in blades)
            {
                int slot=0;
                foreach (var configured in Preset.Slots) if (configured.Kind==EliteModuleKind.ContactBlade && blade.transform.parent==Ring.Mounts[configured.MountIndex].Transform) slot=configured.MountIndex;
                float angle=phase+Ring.Mounts[slot].LocalPhase;
                Vector3 oldPosition=blade.transform.position; Quaternion oldRotation=blade.transform.rotation;
                blade.transform.position=center+(Vector2)Ring.Geometry.PositionDegrees(angle,Preset.Radius);
                blade.transform.rotation=Quaternion.Euler(0,0,angle+90+OrbitalPresentationConfig.Active.SawTangentOffset);
                bool touched=false;
                foreach (var sprite in blade.Sprites) if (sprite!=blade.Halo && OrbitalBodyContactDetector.IntersectsCircle(sprite,playerCenter,radius)) touched=true;
                blade.transform.SetPositionAndRotation(oldPosition,oldRotation);
                if (touched) return true;
            }
            return false;
        }
        public void DamagePlayer(PlayerHealth player)
        {
            if (Time.time<nextBladeHit) return;
            if (player.TakeDamage(Preset.BladeDamage,(Vector2)player.transform.position-Center))
            { nextBladeHit=Time.time+Preset.BladeRepeatSeconds; Feedback?.Invoke(EliteFeedback.BodyHit,player.transform.position); }
        }
        private void BodyDied() => Teardown();
        private void OnDisable() => Teardown();
        private void OnDestroy()
        { if (Health!=null) { Health.HitFeedback-=BodyHit; Health.DeathFeedback-=BodyDied; } }
        private void Teardown()
        {
            foreach (var node in nodes) if (node.Health!=null) node.Health.gameObject.SetActive(false);
            if (orbitRoot!=null) orbitRoot.gameObject.SetActive(false);
            Ring?.Teardown(); Ring=null;
        }
    }
}
#endif
