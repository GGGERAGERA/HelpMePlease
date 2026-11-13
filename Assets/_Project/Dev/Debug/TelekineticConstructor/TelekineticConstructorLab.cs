using Subject42.Combat.Telekinetic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.DebugLabs.Telekinetic
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class TelekineticConstructorLab : MonoBehaviour, ITelekineticStructureOwner
    {
        public const string ScenePath="Assets/_Project/Dev/Labs/TelekineticConstructorLab/TelekineticConstructorLab.unity";
        public GameObject PlayerTemplate, EnemyPrefab;
        public Camera ArenaCamera;
        public GameObject PistolVisual, ShotgunVisual, SawVisual;
        public Material SpriteMaterial;
        Material ITelekineticStructureOwner.SpriteMaterial => SpriteMaterial;
        float ITelekineticStructureOwner.CellSpacing => CellSpacing;
        public Sprite ModuleSprite;
        public OrbitalPresentationConfig Presentation;
        public TelekineticStructureView StructureView;
        public Sprite[] ItemIcons;
        public GameObject ShieldEnemyPrefab;
        public AudioClip ShotClip, SawClip;
        [Header("Lab only / preliminary settings")]
        [Min(.4f)] public float CellSpacing=1.05f;
        [Min(1)] public float RotationSpeed=120;
        [Min(1)] public float RotationCapacity=100;
        [Min(0)] public float RotationDrain=25, RotationRecovery=12;
        [Min(0)] public float CenterMaximum=2, CenterMoveSpeed=6, CenterReturnSpeed=8;
        [Min(0)] public int EnemyDensity=30;
        [Min(1)] public float EnemyMaxHealth=32;
        public bool Invincible=true;
        public TelekineticModuleTuning Modules=new();
        public BulletTimeAbility BulletTime=new();
        public OrbitalWeaponSettings Pistol=OrbitalWeaponSettings.Pistol();
        public OrbitalWeaponSettings Shotgun=OrbitalWeaponSettings.Shotgun();
        public OrbitalWeaponSettings Saw=OrbitalWeaponSettings.Saw();
        public TelekineticBuild Build { get; }=new();
        public PlayerHealth Player { get; private set; }
        public Transform Construction { get; private set; }
        public bool Editing { get; private set; }=true;
        public float Angle { get; private set; }
        public float RotationEnergy { get; private set; }
        public Vector2 CenterOffset { get; private set; }
        public int Alive => enemies.Count(e=>e!=null && !e.IsDead);
        public int ShotsFired { get; private set; }
        public int ModulePulses { get; private set; }
        public float CombatSeconds { get; private set; }
        private readonly List<EnemyHealth> enemies=new();
        private readonly Dictionary<int,GameObject> views=new();
        private readonly Dictionary<int,SpriteRenderer> chargeBars=new();
        private CharacterMovement2D movement;
        private ProductionOrbitalCombatAdapter combat;
        private AudioSource audioSource;
        private float priorTimeScale, priorFixedDelta, nextContactAudio;
        private bool ready, ownsTime, pointerNeedsRelease, shiftNeedsRelease;
        private TelekineticItem selected, dragging;
        private TelekineticPart? brush;
        private bool merging;
        private string notice="";

        private void Start()
        {
            priorTimeScale=Time.timeScale; priorFixedDelta=Time.fixedDeltaTime; ownsTime=true;
            Construction=new GameObject("Levitating construction / local positions").transform;
            Construction.SetParent(transform,false);
            StructureView.Bind(this,Construction);
            CreatePlayer();
            OrbitalPresentationConfig.Configure(Presentation);
            combat=new ProductionOrbitalCombatAdapter(transform,ModuleSprite);
            BindEffectCallbacks();
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false; audioSource.spatialBlend=0;
            densityText=EnemyDensity.ToString(); ready=true; LoadPreset(0); SetEditing(true);
        }
        private void CreatePlayer()
        {
            if (Player!=null)
            {
                Player.Died-=OnDefeated;
                Player.gameObject.SetActive(false); Destroy(Player.gameObject);
            }
            var go=Instantiate(PlayerTemplate,transform); go.name="Subject42 / telekinetic lab player";
            go.transform.position=Vector3.zero; go.SetActive(true);
            Player=go.GetComponent<PlayerHealth>(); Player.SetRuntimeHealth(150,150); Player.DebugInvulnerable=Invincible;
            movement=go.GetComponent<CharacterMovement2D>();
            movement.MovementIntent=()=>Editing || Player.IsDead || !Application.isFocused ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            PlayerRuntimeReference.Bind(go); Player.Died+=OnDefeated;
        }
        private void OnDefeated() { SetEditing(true); notice="Игрок погиб. Нажмите «Новый бой»."; }
        public void SetEditing(bool value)
        {
            if (!ready || !value && Player.IsDead) return;
            if (value) StopOrbitFight();
            Editing=value; dragging=null; merging=false;
            StructureView.SetCombatVisible(!value);
            Construction.gameObject.SetActive(value || !orbitExperiment);
            BulletTime.Release(); shiftNeedsRelease=true; pointerNeedsRelease=true;
            movement.BulletTimeControlMultiplier=1;
            movement.RemoveAnomalySpeedMultiplier(this);
            SetTime(value ? 0 : 1);
            if (value) { Player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero; Build.Disconnected.Clear(); }
            else if (orbitExperiment && orbitStation==null) StartOrbitFight();
        }
        private void SetTime(float value) { Time.timeScale=value; Time.fixedDeltaTime=priorFixedDelta*Mathf.Max(.1f,value); }
        private void Update()
        {
            if (!ready) return;
            if (Input.GetKeyDown(KeyCode.Tab)) SetEditing(!Editing);
            if (Input.GetKeyDown(KeyCode.Escape)) CancelGesture();
            if (Editing) return;
            bool focused=Application.isFocused;
            if (!focused) SuspendInput();
            bool held=focused && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (!held) shiftNeedsRelease=false;
            AdvanceBulletTime(held && !shiftNeedsRelease,Time.unscaledDeltaTime);
            Player.DebugInvulnerable=Invincible;
            bool pointer=focused && Input.GetMouseButton(1);
            if (!pointer) pointerNeedsRelease=false;
            Vector2 desired=Vector2.zero;
            bool steering=pointer && !pointerNeedsRelease && !PointerOverPanel();
            if (steering)
            {
                var screen=Input.mousePosition; screen.z=-ArenaCamera.transform.position.z;
                desired=Vector2.ClampMagnitude((Vector2)ArenaCamera.ScreenToWorldPoint(screen)-(Vector2)Player.transform.position,CenterMaximum);
            }
            CenterOffset=Vector2.MoveTowards(CenterOffset,desired,(steering?CenterMoveSpeed:CenterReturnSpeed)*Time.deltaTime);
            if (steering) movement.SetAnomalySpeedMultiplier(this,.7f); else movement.RemoveAnomalySpeedMultiplier(this);
            CombatSeconds+=Time.deltaTime;
            if (orbitExperiment) TickOrbitFight(focused, Time.deltaTime);
            else
            {
                AdvancePose(focused ? (Input.GetKey(KeyCode.Q)?1:0)-(Input.GetKey(KeyCode.E)?1:0) : 0,Time.deltaTime);
                TickEffectTimers(); TickModules(Time.deltaTime); TickWeapons(Time.deltaTime); combat.Tick(Time.deltaTime);
            }
            MaintainEnemies();
        }
        public void AdvancePose(float turn, float dt)
        {
            RotationEnergy=Mathf.Clamp(RotationEnergy,0,RotationCapacity);
            if (Mathf.Approximately(turn,0)) RotationEnergy=Mathf.Min(RotationCapacity,RotationEnergy+RotationRecovery*dt);
            else if (RotationEnergy>0)
            {
                float duration=RotationDrain>0 ? Mathf.Min(dt,RotationEnergy/RotationDrain) : dt;
                RotationEnergy=Mathf.Max(0,RotationEnergy-RotationDrain*dt);
                Angle=Mathf.Repeat(Angle+Mathf.Clamp(turn,-1,1)*RotationSpeed*duration,360);
            }
            Construction.SetPositionAndRotation((Vector2)Player.transform.position+CenterOffset,Quaternion.Euler(0,0,Angle));
        }
        public void AdvanceBulletTime(bool held,float unscaledDelta)
        {
            BulletTime.Tick(held,unscaledDelta);
            float scale=BulletTime.WorldTimeScale;
            float target=BulletTime.IsActive ? scale : 1;
            float blend=BulletTime.IsActive ? BulletTime.EnterBlendDuration : BulletTime.ExitBlendDuration;
            SetTime(Mathf.MoveTowards(Time.timeScale,target,(1-scale)*unscaledDelta/blend));
            // Same world/player blend as OrbitalInteractionController, including Shift release.
            float playerScale=Mathf.Lerp(1,BulletTime.PlayerEffectiveTimeScale,Mathf.InverseLerp(1,scale,Time.timeScale));
            movement.BulletTimeControlMultiplier=playerScale/Time.timeScale;
        }
        private void LateUpdate()
        {
            if (!ready) return;
            Construction.position=(Vector2)Player.transform.position+CenterOffset;
            if (orbitStation!=null) orbitStation.transform.position=Construction.position;
            ArenaCamera.transform.position=new Vector3(Player.transform.position.x,Player.transform.position.y,-10);
            foreach (var c in Build.Items)
            {
                if (!chargeBars.TryGetValue(c.Id,out var bar)) continue;
                float progress=Mathf.Clamp01(1-c.Remaining/Modules.Cooldown(c,Build));
                bar.transform.localScale=new Vector3(.48f*progress,.055f,1);
                bar.color=progress>.92f ? Color.white : PartColor(c.Kind);
            }
        }
        public bool Place(TelekineticPart kind,Vector2Int p,int turns=0) => Editing && Apply(Build.Place(kind,p,turns));
        public bool Move(TelekineticItem c,Vector2Int p,int? turns=null) => Editing && Apply(Build.Move(c,p,turns));
        public bool Rotate(TelekineticItem item) => Editing && Apply(Build.Rotate(item));
        public bool Remove(TelekineticItem c) => Editing && Apply(Build.Remove(c));
        public bool Merge(TelekineticItem keep,TelekineticItem consume) => Editing && Apply(Build.Merge(keep,consume));
        private bool Apply(bool changed)
        {
            notice=Build.Message;
            if (changed)
            {
                // Editing invalidates pending links, but preserves each surviving module's charge fraction.
                echoes.Clear();
                foreach (var c in Build.Items) { c.Damage.Clear(); c.Rapid.Clear(); c.RapidUntil.Clear(); c.Extra.Clear(); c.Ricochet.Clear(); c.Effects.Clear(); }
                SyncViews();
            }
            return changed;
        }
        private void SyncViews()
        {
            var ids=new HashSet<int>(Build.Items.Select(c=>c.Id));
            foreach (var id in views.Keys.Where(id=>!ids.Contains(id)).ToArray())
            { views[id].SetActive(false); Destroy(views[id]); views.Remove(id); chargeBars.Remove(id); }
            foreach (var c in Build.Items)
            {
                if (c.IsModule)
                {
                    float cooldown=Modules.Cooldown(c,Build);
                    c.Remaining=c.LastCooldown>0 ? cooldown*Mathf.Clamp01(c.Remaining/c.LastCooldown) : cooldown;
                    c.LastCooldown=cooldown;
                }
                if (!views.TryGetValue(c.Id,out var go))
                {
                    go=new GameObject(TelekineticBuild.Label(c.Kind)+" #"+c.Id); go.transform.SetParent(Construction,false); views.Add(c.Id,go);
                    if (c.IsWeapon)
                    {
                        var template=c.Kind==TelekineticPart.Pistol?PistolVisual:c.Kind==TelekineticPart.Shotgun?ShotgunVisual:SawVisual;
                        var art=Instantiate(template,go.transform); art.transform.localPosition=Vector3.zero; art.transform.localRotation=Quaternion.identity; art.SetActive(true);
                    }
                    else
                    {
                        var body=new GameObject("Pixel node").transform; body.SetParent(go.transform,false);
                        // Scale the renderer child; the cell transform remains in metres.
                        var node=body.gameObject.AddComponent<SpriteRenderer>(); ApplyIcon(node,c.Kind);
                        node.sortingLayerName="Player"; node.sortingOrder=45;
                        body.localScale=Vector3.one*(c.Kind==TelekineticPart.Frame?.16f:.4f);
                    }
                    if (c.IsModule)
                    {
                        var barGo=new GameObject("Independent cooldown charge"); barGo.transform.SetParent(go.transform,false);
                        barGo.transform.localPosition=new Vector3(0,-.28f,0);
                        var bar=barGo.AddComponent<SpriteRenderer>(); bar.sprite=ModuleSprite; bar.sharedMaterial=SpriteMaterial;
                        bar.sortingLayerName="Player"; bar.sortingOrder=48; chargeBars.Add(c.Id,bar);
                        c.Remaining=Modules.Cooldown(c,Build);
                    }
                }
                go.transform.localPosition=c.Center*CellSpacing;
                go.transform.localRotation=Quaternion.Euler(0,0,c.IsWeapon ? Mathf.Atan2(c.Center.y,c.Center.x)*Mathf.Rad2Deg : 0);
            }
            StructureView.Rebuild();
        }
        public void ResetBuild()
        {
            SetEditing(true); Build.Clear(); selected=null; brush=null; Angle=0; CenterOffset=Vector2.zero; RotationEnergy=RotationCapacity;
            Construction.rotation=Quaternion.identity; Apply(true);
        }
        public void LoadPreset(int index)
        {
            ResetBuild();
            void Add(TelekineticPart k,int x,int y,int turns=0)
            {
                if (!Build.Place(k,new Vector2Int(x,y),turns))
                    throw new System.InvalidOperationException("Invalid lab preset: "+k+" / "+Build.Message);
            }
            if (index==0)
            {
                Add(TelekineticPart.DMG,1,0); Add(TelekineticPart.FR,-1,0); Add(TelekineticPart.Double,-1,1); Add(TelekineticPart.RIC,0,-1,2);
                Add(TelekineticPart.Energy,2,0); Add(TelekineticPart.Pistol,1,-1); Add(TelekineticPart.Pistol,-2,0);
                Add(TelekineticPart.Shotgun,1,2); Add(TelekineticPart.Saw,-3,1); Add(TelekineticPart.Saw,1,-3); Add(TelekineticPart.Pistol,3,0);
            }
            else if (index==1)
            {
                Add(TelekineticPart.Frame,1,0); Add(TelekineticPart.DMG,2,0); Add(TelekineticPart.Shotgun,3,0); Add(TelekineticPart.Saw,3,-2);
                Add(TelekineticPart.FR,2,2); Add(TelekineticPart.Pistol,3,2); Add(TelekineticPart.Energy,1,1); Add(TelekineticPart.Double,1,-2);
                Add(TelekineticPart.Pistol,2,-3); Add(TelekineticPart.RIC,1,-3,2); Add(TelekineticPart.Energy,-1,-3);
            }
            else if (index==2)
            {
                Add(TelekineticPart.Energy,0,1); Add(TelekineticPart.DMG,1,1); Add(TelekineticPart.Pistol,2,1); Add(TelekineticPart.Double,2,2);
                Add(TelekineticPart.Shotgun,4,2); Add(TelekineticPart.FR,1,3); Add(TelekineticPart.Energy,0,3); Add(TelekineticPart.Frame,-1,0);
                Add(TelekineticPart.Saw,-3,0); Add(TelekineticPart.RIC,-1,-1,2); Add(TelekineticPart.Pistol,-2,-2);
            }
            else if (index==3)
            {
                Add(TelekineticPart.Frame,1,0); Add(TelekineticPart.Pistol,2,0);
                Add(TelekineticPart.Crit,2,1); Add(TelekineticPart.Slow,3,0); Add(TelekineticPart.Knock,2,-1); Add(TelekineticPart.Energy,1,1);
                Add(TelekineticPart.Shotgun,-1,0); Add(TelekineticPart.Pierce,-2,0); Add(TelekineticPart.Splash,-1,-1,1);
            }
            else
            {
                Add(TelekineticPart.Frame,1,0); Add(TelekineticPart.Shotgun,2,0); Add(TelekineticPart.Chain,3,0);
                Add(TelekineticPart.Shred,2,-1,1); Add(TelekineticPart.Echo,0,1); Add(TelekineticPart.Pistol,2,2);
                Add(TelekineticPart.Crit,3,2); Add(TelekineticPart.Energy,0,3); Add(TelekineticPart.Saw,-2,0);
                Add(TelekineticPart.Knock,-2,-1); Add(TelekineticPart.Slow,-3,0); Add(TelekineticPart.Splash,-3,1);
            }
            Apply(true); notice="Пресет: "+PresetNames[index]+". Параметры экспериментальные.";
        }
        public void NewFight()
        {
            SetEditing(true); ClearEnemies(); combat.DebugClearProjectiles();
            CreatePlayer();
            BulletTime.Reset(); CombatSeconds=0; ShotsFired=ModulePulses=0; CenterOffset=Vector2.zero; RotationEnergy=RotationCapacity;
            orbitSpawnRandom=new System.Random(42);
            if (orbitExperiment) Angle=0;
            foreach (var c in Build.Items)
            {
                c.Damage.Clear(); c.Rapid.Clear(); c.RapidUntil.Clear(); c.Extra.Clear(); c.Ricochet.Clear(); c.Effects.Clear();
                c.LastCooldown=c.Remaining=c.IsModule ? Modules.Cooldown(c,Build) : 0; c.ShotRemaining=0; c.Activations=c.Shots=0;
            }
            SetEditing(false); MaintainEnemies();
        }
        private void MaintainEnemies()
        {
            enemies.RemoveAll(e=>e==null || e.IsDead);
            // Density is a target, not a clamped cap. The UI accepts any nonnegative integer.
            int target=orbitExperiment ? OrbitEnemyCount : Mathf.Max(0,EnemyDensity);
            while (enemies.Count>target) { var e=enemies[enemies.Count-1]; enemies.RemoveAt(enemies.Count-1); e.gameObject.SetActive(false); Destroy(e.gameObject); }
            int count=Mathf.Min(target-enemies.Count,20); // Spread creation cost across frames, never reduce the requested density.
            for (int i=0;i<count;i++)
            {
                if (orbitExperiment) SpawnOrbitEnemy();
                else
                {
                    Vector2 p=(Vector2)Player.transform.position+Random.insideUnitCircle.normalized*Random.Range(8,12);
                    SpawnTarget(p);
                }
            }
        }
        public EnemyHealth SpawnTarget(Vector2 position,bool stationary=false,bool shield=false) =>
            SpawnEnemy(shield ? ShieldEnemyPrefab : EnemyPrefab, position, stationary, EnemyMaxHealth);

        private EnemyHealth SpawnEnemy(GameObject prefab, Vector2 position, bool stationary, float? maxHealth)
        {
            var go=Instantiate(prefab,position,Quaternion.identity,transform);
            var dummy=go.AddComponent<LabEnemyDummy>(); dummy.Initialize(); dummy.Invulnerable=false;
            if (stationary) dummy.ApplyMode(LabEnemyDummyMode.Static);
            var health=go.GetComponent<EnemyHealth>();
            if (maxHealth.HasValue) health.SetRuntimeMaxHealth(maxHealth.Value);
            health.NotifySpawnConfigured(); enemies.Add(health);
            return health;
        }
        public void ClearEnemies()
        {
            ClearEffectState();
            foreach (var e in enemies) if (e!=null) { e.gameObject.SetActive(false); Destroy(e.gameObject); }
            enemies.Clear();
        }
        private void CancelGesture() { dragging=null; brush=null; merging=false; selected=null; Build.Disconnected.Clear(); notice="Действие отменено."; }
        private void SuspendInput()
        {
            BulletTime.Release(); shiftNeedsRelease=pointerNeedsRelease=true; ReleaseOrbitPointer();
            movement?.RemoveAnomalySpeedMultiplier(this);
            if (!Editing) { movement.BulletTimeControlMultiplier=1; SetTime(1); }
        }
        private void OnApplicationFocus(bool focus) { if (!focus && ready) SuspendInput(); }
        private void OnDestroy()
        {
            StopOrbitFight();
            if (orbitRunState!=null) Destroy(orbitRunState.gameObject);
            ClearEffectState();
            if (Player!=null) { Player.Died-=OnDefeated; if (PlayerRuntimeReference.CachedPlayer==Player.gameObject) PlayerRuntimeReference.Clear(); }
            combat?.Teardown();
            if (ownsTime) { Time.timeScale=priorTimeScale; Time.fixedDeltaTime=priorFixedDelta; }
        }
    }
}
#endif
