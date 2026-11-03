using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.Combat.Telekinetic
{
    [DisallowMultipleComponent]
    public sealed partial class TelekineticRig : MonoBehaviour, ITelekineticStructureOwner
    {
        public GameObject PistolVisual, ShotgunVisual, SawVisual, SniperVisual;
        public Material SpriteMaterial;
        public Sprite ModuleSprite;
        public Sprite[] ItemIcons;
        public AudioClip ShotClip, SawClip;
        public TelekineticStructureView StructureView;
        [SerializeField] private GameObject constructorPanelPrefab;
        private GameObject constructorPanel;
        [Min(.4f)] public float CellSpacing=1.05f;
        [Min(1)] public float RotationSpeed=90, RotationCapacity=100;
        [Min(0)] public float RotationDrain=25, RotationRecovery=12, AutoRotationSpeed=12;
        public TelekineticModuleTuning Modules=new();
        public OrbitalWeaponSettings Pistol, Shotgun, Saw, Sniper;
        public TelekineticBuild Build => station.State.Telekinetic;
        public Transform Construction { get; private set; }
        public bool Editing => station != null && station.InputOwner != null && station.InputOwner.IsConstructing;
        public float Angle => station.State.TelekineticAngle;
        public float RotationEnergy => station.State.RotationEnergy;
        public bool AutoRotate { get => station.State.TelekineticAutoRotate; set => station.State.TelekineticAutoRotate=value; }
        public int ShotsFired { get; private set; }
        public int ModulePulses { get; private set; }
        public float CombatSeconds { get; private set; }
        private float presentationRadius;
        public float PresentationRadius => presentationRadius+Mathf.Max(0,station.State.ContactSizeMultiplier-1)*Saw.ContactRadius;
        Material ITelekineticStructureOwner.SpriteMaterial => SpriteMaterial;
        float ITelekineticStructureOwner.CellSpacing => CellSpacing;
        private OrbitalStationRuntime station;
        private ProductionOrbitalCombatAdapter combat;
        private AudioSource audioSource;
        private readonly Dictionary<int,GameObject> views=new();
        private readonly Dictionary<int,SpriteRenderer> chargeBars=new();
        private float nextContactAudio;
        private bool ready;
        private TelekineticItem selected, held;
        private bool heldFromReserve, merging;

        public void Bind(OrbitalStationRuntime owner)
        {
            station=owner; combat=(ProductionOrbitalCombatAdapter)owner.Combat;
            Construction=new GameObject("Telekinetic rig / common phase").transform;
            Construction.SetParent(transform,false);
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false; audioSource.spatialBlend=0;
            StructureView.Bind(this,Construction); BindEffectCallbacks();
            drawnAreaRadius = previousAreaRadius = Build.AvailableRadius;
            areaRevealedAt = Time.unscaledTime - AreaRevealSeconds;
            ready=true; SyncViews(); ApplyPose();
        }
        public void SetEditing(bool value)
        {
            if (value) station.Interaction.OpenConstructor(); else station.Interaction.CloseConstructor();
        }
        public void SetConstructorVisible(bool visible)
        {
            ResetAnomaly();
            CancelSelection();
            if (visible)
            {
                if (constructorPanel == null) constructorPanel = Instantiate(constructorPanelPrefab);
                constructorPanel.SetActive(true);
                ResetBoardFeedback();
            }
            else if (constructorPanel != null) constructorPanel.SetActive(false);
            StructureView.SetCombatVisible(!visible);
        }
        public bool CancelSelection()
        {
            bool had = held != null || selected != null || merging;
            held = selected = null;
            merging = false;
            return had;
        }

        public bool Place(TelekineticPart kind, Vector2Int p, int turns = 0) =>
            Install(Build.Reserve.FirstOrDefault(item => item.Kind == kind), p, turns);

        private bool Install(TelekineticItem item, Vector2Int p, int turns) =>
            CanEdit && Apply(Build.Install(item, p, turns));

        public bool Move(TelekineticItem item,Vector2Int p,int? turns=null) => CanEdit && Apply(Build.Move(item,p,turns));
        public bool Rotate(TelekineticItem item) => CanEdit && Apply(Build.Rotate(item));

        public bool TurnWeapon(TelekineticItem item, int steps)
        {
            if (!CanEdit || item == null || !item.IsWeapon || !Build.Items.Contains(item)) return false;
            item.AimSteps = ((item.AimSteps + steps) % 8 + 8) % 8;
            views[item.Id].transform.localRotation = Quaternion.Euler(0, 0, item.Heading);
            return true;
        }

        public bool Remove(TelekineticItem item) => CanEdit && Apply(Build.Stow(item));
        public bool Merge(TelekineticItem keep,TelekineticItem consume) => CanEdit && Apply(Build.Merge(keep,consume));
        private bool CanEdit => ready && station.InputOwner.CanEditConstructor;
        private bool Apply(bool changed)
        {
            if (changed) { InvalidateCharges(); SyncViews(); }
            return changed;
        }
        private void InvalidateCharges()
        {
            echoes.Clear();
            foreach (var c in Build.Items)
            { c.Damage.Clear(); c.Rapid.Clear(); c.RapidUntil.Clear(); c.Extra.Clear(); c.Ricochet.Clear(); c.Effects.Clear(); }
        }
        public void Tick(float dt,bool q,bool e,bool allowed)
        {
            if (!ready) return;
            if (!allowed) { ResetAnomaly(); ApplyPose(); return; }
            TickAnomaly(dt);
            AdvanceRotation(dt,q,e);
            CombatSeconds+=dt;
            TickEffectTimers();
            if (!Suppressed) TickModules(dt);
            TickWeapons(dt); combat.Tick(dt); TickWeaponEffects();
            ApplyPose();
            foreach (var c in Build.Items)
                if (chargeBars.TryGetValue(c.Id,out var bar))
                {
                    float progress=Mathf.Clamp01(1-c.Remaining/Modules.Cooldown(c,Build));
                    bar.transform.localScale=new Vector3(.48f*progress,.055f,1);
                    bar.color=Suppressed ? Color.red : progress>.92f ? Color.white : PartColor(c.Kind);
                }
        }
        public void AdvanceRotation(float dt,bool q,bool e)
        {
            float turn=(q?1:0)-(e?1:0);
            float speed=ActiveAnomaly==LocalAnomalyType.Stasis?.5f:1;
            float drain=RotationDrain*(ActiveAnomaly==LocalAnomalyType.Berserk?1.75f:1);
            if (ActiveAnomaly==LocalAnomalyType.Gravity) turn*=Mathf.Cos(anomalyElapsed*2.7f);
            float energy=Mathf.Clamp(RotationEnergy,0,RotationCapacity);
            float delta=0;
            if (q || e)
            {
                if (!Mathf.Approximately(turn,0) && energy>0)
                {
                    float duration=drain>0?Mathf.Min(dt,energy/drain):dt;
                    delta=turn*RotationSpeed*speed*duration; energy=Mathf.Max(0,energy-drain*dt);
                }
            }
            else
            {
                energy=Mathf.Min(RotationCapacity,energy+RotationRecovery*dt);
                if (AutoRotate) delta=AutoRotationSpeed*speed*dt;
            }
            station.State.RotationEnergy=energy;
            station.State.TelekineticAngle=Mathf.Repeat(Angle+delta,360);
        }
        private void ApplyPose()
        {
            if (Construction!=null) Construction.rotation=Quaternion.Euler(0,0,Angle);
        }
        public Vector2 DistortCenter(Vector2 offset) => ActiveAnomaly==LocalAnomalyType.Gravity
            ? (Vector2)(Quaternion.Euler(0,0,18*Mathf.Sin(anomalyElapsed*2.1f))*offset) : offset;
        public bool HasWeapon => Build.Items.Any(c=>c.IsWeapon);
        private readonly Dictionary<int,SpriteRenderer[]> weaponBodies=new();
        public bool HasBodyContact(Vector2 point,float radius) => weaponBodies.Values.Any(bodies=>
            bodies.Any(body=>OrbitalBodyContactDetector.IntersectsCircle(body,point,radius)));
        public void Release()
        {
            if (!ready) return;
            ResetAnomaly(); ClearEffectState(); InvalidateCharges();
            combat.Hit-=RememberBodyTarget; combat.ProjectileSpawned-=CaptureProjectile;
            combat.ProjectileContact-=ProjectileContact; combat.ProjectileEnded-=ProjectileEnded;
            if (constructorPanel != null) Destroy(constructorPanel);
            ready=false;
        }
        private void OnDestroy() => Release();
    }
}
