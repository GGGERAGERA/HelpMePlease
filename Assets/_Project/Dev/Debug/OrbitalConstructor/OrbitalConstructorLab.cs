#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.DebugLabs.Constructor
{
    [DefaultExecutionOrder(200)]
    public sealed class OrbitalConstructorLab : MonoBehaviour
    {
        public const string ScenePath="Assets/_Project/Dev/Labs/OrbitalLab/OrbitalConstructorLab.unity";
        public GameObject PlayerTemplate, MeleePrefab, ShooterPrefab;
        public Camera ArenaCamera;
        public AudioClip SawHitClip;
        public bool ShowCombatTrajectories=true;
        [Range(.8f,8)] public float DummyDistance=1.2f;
        public Transform MachineRoot=>view?.Root;
        public int ContactSoundPlays { get; private set; }
        private AudioSource hitAudio;
        private float nextSoundTime;
        [Header("Preliminary mass balance / degrees per second")]
        [Min(1)] public float BaseRotationSpeed=180;
        [Min(1)] public float MinimumRotationSpeed=35;
        [Min(1)] public float MaximumRotationSpeed=240;
        [Min(0)] public float InertiaResponse=.006f;
        public PartMasses Masses=new();
        [Header("Hook hold / fraction of one revolution, capped in seconds")]
        [Range(.01f,1)] public float HookHoldTurns=.2f;
        [Min(0)] public float MinimumHookHoldSeconds=.1f;
        [Min(.1f)] public float MaximumHookHoldSeconds=1.5f;
        public MassBalance Balance { get; private set; }
        public float RotationSpeed=>Balance.DegreesPerSecond;
        public float TurnSeconds=>Balance.TurnSeconds;
        public int RotationRecalculations { get; private set; }
        // Scene smoke fixtures can freeze rigid motion without violating the configured speed limits.
        public bool DebugFreezeRotation { get; set; }
        [Range(.05f,1)] public float RepeatHitInterval=.32f;
        public OrbitalAssembly Assembly { get; }=new();
        public OrbitalMachineCombat Combat { get; }=new();
        public PlayerHealth Player { get; private set; }
        public bool Editing { get; private set; }=true;
        public bool Busy { get; private set; }
        public float Angle { get; private set; }
        public float Elapsed { get; private set; }
        public float DamageReceived { get; private set; }
        public int ProbePasses { get; private set; }
        public float ProbeInterval { get; private set; }
        public float ProbeRadius { get; private set; }
        private EnemyHealth probeTarget;
        private float lastProbeHit=-1;
        private bool probing, showBalanceSettings, showArchivedParts, lastTrainingDummy;
        public int Alive => enemies.Count(e=>e!=null && !e.IsDead);
        public bool ShowPanel=true;
        public readonly List<string> Results=new();
        private readonly List<EnemyHealth> enemies=new();
        private readonly HashSet<GameObject> authoredRoots=new();
        private OrbitalMachineView view;
        private CharacterMovement2D movement;
        private PartKind selectedKind;
        private int orientation, mode, selectedNode=-1, mountParent=-1, mountPort, pendingElectrode=-1;
        private int previewParent=-2, previewPort=-2, previewOrientation=-1;
        private bool mountsDirty=true, waveActive, crowdEnabled;
        private int lastWave;
        private string notice="Выберите сборку или установите детали в зелёные крепления.", buildName="Ручная", outcome="";
        private Vector2 scroll;
        private float originalTimeScale;
        private static readonly string[] Presets={ "Дальний", "Ближний", "Смешанный" };
        private static readonly string[] Waves={ "Ближний бой", "Стрелки", "Смешанная" };
        public Rect PanelRect=>new(8,8,Mathf.Min(320,Screen.width*.3f),Screen.height-16);

        private void Start()
        {
            originalTimeScale=Time.timeScale;
            crowdEnabled=CrowdSteeringRuntime.Values.Enabled;
            CrowdSteeringRuntime.SetEnabled(false); // Fixed conditions; production AI itself is reused.
            foreach (var root in gameObject.scene.GetRootGameObjects()) authoredRoots.Add(root);
            view=new OrbitalMachineView(transform);
            Combat.ContactHit+=MeasureContact;
            Combat.ContactImpact+=PresentContact;
            hitAudio=gameObject.AddComponent<AudioSource>(); hitAudio.playOnAwake=false; hitAudio.spatialBlend=0;
            CreatePlayer(); LoadPreset(1); Pause();
        }
        private void OnValidate() => RecalculateRotation();
        public void RecalculateRotation()
        {
            Masses ??= new PartMasses();
            Balance=MassBalance.Calculate(Assembly,Masses,BaseRotationSpeed,InertiaResponse,MinimumRotationSpeed,MaximumRotationSpeed);
            Combat.HookHoldSeconds=Balance.HoldSeconds(HookHoldTurns,MinimumHookHoldSeconds,MaximumHookHoldSeconds);
            RotationRecalculations++;
        }
        private void MeasureContact(int source,EnemyHealth target)
        {
            if (!probing || target!=probeTarget) return;
            if (lastProbeHit>=0) ProbeInterval=Time.time-lastProbeHit;
            lastProbeHit=Time.time; ProbePasses++;
        }
        private void PresentContact(Vector2 point)
        {
            view.Impact(point);
            if (SawHitClip!=null && Time.time>=nextSoundTime)
            { hitAudio.PlayOneShot(SawHitClip,.24f); nextSoundTime=Time.time+.045f; ContactSoundPlays++; }
        }
        private void CreatePlayer()
        {
            var go=Instantiate(PlayerTemplate,transform);
            go.name="Player (constructor training only)"; go.transform.position=Vector3.zero;
            go.SetActive(true);
            Player=go.GetComponent<PlayerHealth>(); Player.SetRuntimeHealth(150,150);
            Player.DebugDamageApplied+=OnDamage;
            Player.Died+=Defeated;
            movement=go.GetComponent<CharacterMovement2D>();
            PlayerLoadoutFactory.ApplyDebugMoveSpeed(movement,5);
            movement.MovementIntent=()=>Editing || Busy ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            PlayerRuntimeReference.Bind(go);
        }
        private void OnDamage(float lost) { if (waveActive) DamageReceived+=lost; }
        private void Defeated() => FinishWave(false);
        public void Pause()
        {
            Editing=true; Time.timeScale=0;
            if (Player!=null && Player.TryGetComponent<Rigidbody2D>(out var body)) body.linearVelocity=Vector2.zero;
            mountsDirty=true;
        }
        public void Resume()
        {
            if (Busy || Player==null || Player.IsDead || Assembly.Nodes.Count==0) return;
            Editing=false; mountParent=-1; view.Preview(null,false); Time.timeScale=1; mountsDirty=true;
            Combat.SynchronizePose(Player.transform.position,Angle);
        }
        public void LoadPreset(int index)
        {
            if (Busy) return;
            Pause(); ClearEncounter(); Assembly.LoadPreset(index); Angle=0;
            buildName=Presets[index]; outcome=""; Elapsed=DamageReceived=0;
            Changed(); notice=buildName+": 4 балки, Y и 2 пилы. Урон только в дисках; подводите их к врагам движением.";
        }
        public bool Install(PartKind kind,int parent,int port,float turn)
        {
            if (!Editing || Busy) return false;
            bool installed=Assembly.Add(kind,parent,port,turn,out notice);
            if (installed) { buildName="Ручная"; Changed(); }
            return installed;
        }
        public int Remove(int id)
        {
            if (!Editing || Busy) return 0;
            int count=Assembly.RemoveBranch(id);
            if (count>0) { buildName="Ручная"; Changed(); notice=$"Удалена ветвь: {count} деталей возвращено в запас; её мост снят."; }
            return count;
        }
        public bool Pair(int a,int b)
        {
            if (!Editing || Busy) return false;
            bool paired=Assembly.Pair(a,b,out notice); view.Bridge(Assembly,false); return paired;
        }
        public void FullReset()
        {
            if (Busy) return;
            Pause(); ClearEncounter(); Assembly.Clear(); Angle=0; Elapsed=DamageReceived=0; outcome=""; Results.Clear();
            buildName="Ручная"; Changed(); StartCoroutine(RestorePlayer(false,0));
        }
        public void StartWave(int kind)
        {
            if (Busy || Assembly.Nodes.Count==0) { notice="Сначала установите хотя бы одну деталь"; return; }
            lastTrainingDummy=false;
            lastWave=Mathf.Clamp(kind,0,2); Pause(); ClearEncounter(); StartCoroutine(RestorePlayer(true,lastWave));
        }
        public void StartContactProbe()
        {
            if (Busy || !Assembly.Nodes.Any(n=>n.Kind==PartKind.Blade)) return;
            lastTrainingDummy=true;
            Pause(); ClearEncounter(); StartCoroutine(RestorePlayer(false,0,true));
        }
        private IEnumerator RestorePlayer(bool spawn,int kind,bool probe=false)
        {
            Busy=true;
            if (Player!=null)
            {
                Player.DebugDamageApplied-=OnDamage; Player.Died-=Defeated;
                Player.gameObject.SetActive(false); Destroy(Player.gameObject); Player=null;
            }
            PlayerRuntimeReference.Clear();
            yield return null; // Retire subscriptions, invulnerability and dash/knockback state.
            CreatePlayer(); Angle=0; Elapsed=DamageReceived=0; outcome="";
            view.Root.position=Vector3.zero; view.Root.rotation=Quaternion.identity;
            yield return null;
            Busy=false;
            Combat.Reset(Vector2.zero,0);
            if (probe)
            {
                probeTarget=SpawnTarget(false,new Vector2(DummyDistance,0));
                var marker=probeTarget.GetComponent<LabEnemyDummy>(); marker.ApplyMode(LabEnemyDummyMode.Static); marker.Invulnerable=true;
                probeTarget.name="Stationary training dummy (no AI / no rewards)";
                ProbeRadius=DummyDistance; probing=true;
                Combat.HooksEnabled=false;
                Resume(); notice="Манекен неподвижен. WASD: подведите к нему диск; каркас и окружности не наносят урон.";
            }
            else if (spawn)
            {
                // Identical positions, health, fire timers, rotation and player state on each retry.
                int meleeCount=kind==0 ? 12 : kind==2 ? 8 : 0;
                for (int i=0;i<meleeCount;i++) SpawnTarget(false,new Vector2(5.6f+(i%3)*.65f,(i/3-(meleeCount/3-1)*.5f)*.7f));
                int shooterCount=kind==1 ? 6 : kind==2 ? 4 : 0;
                for (int i=0;i<shooterCount;i++)
                {
                    float a=i*Mathf.PI*2/shooterCount;
                    SpawnTarget(true,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6.5f);
                }
                waveActive=true; notice="Волна: "+Waves[kind]+". WASD — движение; Tab — редактор/пауза."; Resume();
            }
            else { Pause(); notice="Сброс: конструкция, пара, враги, снаряды и эффекты очищены."; }
        }
        public EnemyHealth SpawnTarget(bool shooter,Vector2 position)
        {
            var prefab=shooter ? ShooterPrefab : MeleePrefab;
            var go=Instantiate(prefab,position,Quaternion.identity,transform);
            var marker=go.AddComponent<LabEnemyDummy>(); marker.Initialize(); marker.Invulnerable=false;
            var health=go.GetComponent<EnemyHealth>(); health.SetRuntimeMaxHealth(shooter ? 28 : 32); health.NotifySpawnConfigured();
            enemies.Add(health); return health;
        }
        public void ClearEncounter()
        {
            waveActive=false;
            Combat.HooksEnabled=false;
            view?.ClearImpacts(); hitAudio?.Stop(); nextSoundTime=0; ContactSoundPlays=0;
            probing=false; probeTarget=null; ProbePasses=0; ProbeInterval=ProbeRadius=0; lastProbeHit=-1;
            Combat.Reset(Player!=null ? (Vector2)Player.transform.position : Vector2.zero,Angle);
            foreach (var enemy in enemies) if (enemy!=null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Clear();
            // Existing enemy projectiles/FX spawn at scene root. Never touch roots from another scene.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (!authoredRoots.Contains(root)) { root.SetActive(false); Destroy(root); }
            Combat.Reset(Player!=null ? (Vector2)Player.transform.position : Vector2.zero,Angle);
        }
        private void FinishWave(bool cleared)
        {
            if (!waveActive) return;
            waveActive=false; outcome=cleared ? "Зачищено" : "Игрок погиб";
            Results.Insert(0,$"{buildName} / {Waves[lastWave]}: {outcome}, {Elapsed:F1} с, урон {DamageReceived:F0}; оборот {TurnSeconds:F2} с");
            if (Results.Count>6) Results.RemoveAt(Results.Count-1);
            Pause(); notice=outcome+". Перезапустите волну для сравнения.";
        }
        private void Changed()
        {
            RecalculateRotation();
            selectedNode=pendingElectrode=mountParent=-1; previewParent=-2; mountsDirty=true;
            view.Rebuild(Assembly); view.Preview(null,false);
            Combat.Reset(Player!=null ? (Vector2)Player.transform.position : Vector2.zero,Angle);
        }
        private void Update()
        {
            if (view==null || Player==null) return;
            if (Input.GetKeyDown(KeyCode.F1)) ShowPanel=!ShowPanel;
            if (Input.GetKeyDown(KeyCode.G)) ShowCombatTrajectories=!ShowCombatTrajectories;
            if (Input.GetKeyDown(KeyCode.Tab)) { if (Editing) Resume(); else Pause(); }
            if (!Editing && !Busy) { if (!DebugFreezeRotation) Angle=Mathf.Repeat(Angle+RotationSpeed*Time.deltaTime,360); if (waveActive) Elapsed+=Time.deltaTime; }
            view.Root.position=Player.transform.position;
            view.Root.rotation=Quaternion.Euler(0,0,Angle);
            view.Bridge(Assembly,!Editing);
            view.Animate(Editing || ShowCombatTrajectories);
            if (mountsDirty) { view.ShowMounts(Assembly,selectedKind,OrbitalPartCatalog.Orientations[orientation],Editing && mode==0); mountsDirty=false; }
            if (Editing && !Busy) ReadEditorInput();
        }
        private void LateUpdate()
        {
            if (Player!=null && ArenaCamera!=null) ArenaCamera.transform.position=Player.transform.position+new Vector3(0,0,-10);
            if (Player==null || Busy || Editing) return;
            Combat.HitInterval=RepeatHitInterval;
            Combat.Tick(Assembly,gameObject.scene,Player.transform.position,Angle);
            if (waveActive && Alive==0) FinishWave(true);
        }
        private void ReadEditorInput()
        {
            if (Input.GetKeyDown(KeyCode.Q)) Turn(-1);
            if (Input.GetKeyDown(KeyCode.E)) Turn(1);
            if (Input.GetKeyDown(KeyCode.Delete)) Remove(selectedNode);
            if (Input.GetKeyDown(KeyCode.Return) && mountParent>=0) Install(selectedKind,mountParent,mountPort,OrbitalPartCatalog.Orientations[orientation]);
            if (Input.GetKeyDown(KeyCode.Escape)) { mountParent=pendingElectrode=-1; view.Preview(null,false); previewParent=-2; }
            Vector2 screen=new(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
            if (ShowPanel && PanelRect.Contains(screen)) return;
            if (!ArenaCamera.pixelRect.Contains(Input.mousePosition)) return;
            Vector2 world=ArenaCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 local=OrbitalPartCatalog.Rotate(world-(Vector2)Player.transform.position,-Angle);
            if (mode==0)
            {
                int parent=mountParent,port=mountPort;
                if (parent<0)
                {
                    float distance=.45f;
                    foreach (var mount in Assembly.FreeMounts())
                    {
                        float d=Vector2.Distance(local,mount.mount.Position);
                        if (d<distance) { distance=d; parent=mount.parent; port=mount.port; }
                    }
                }
                if (previewParent!=parent || previewPort!=port || previewOrientation!=orientation)
                {
                    AssemblyNode candidate=null;
                    bool valid=parent>=0 && Assembly.Preview(selectedKind,parent,port,OrbitalPartCatalog.Orientations[orientation],out candidate,out notice);
                    view.Preview(candidate,valid); previewParent=parent; previewPort=port; previewOrientation=orientation;
                }
                if (Input.GetMouseButtonDown(0) && parent>=0) { mountParent=parent; mountPort=port; notice="Точка выбрана. Q/E — поворот; Enter — установить."; }
            }
            if (Input.GetMouseButtonDown(1) || (mode!=0 && Input.GetMouseButtonDown(0)))
            {
                float nearest=.4f; int id=-1;
                foreach (var node in Assembly.Nodes)
                {
                    var def=OrbitalPartCatalog.Get(node.Kind);
                    if (def.DiskRadius>0)
                    {
                        float distance=Mathf.Max(0,Vector2.Distance(local,node.Point(def.WorkingCenter))-def.DiskRadius);
                        if (distance<nearest) { nearest=distance; id=node.Id; }
                    }
                    foreach (var s in OrbitalPartCatalog.Get(node.Kind).Segments)
                    {
                        float d=OrbitalAssembly.SegmentDistance(local,node.Point(s.A),node.Point(s.B));
                        if (d<nearest) { nearest=d; id=node.Id; }
                    }
                }
                selectedNode=id; view.Highlight(Assembly,id);
                if (mode==2 && Assembly.Find(id)?.Kind==PartKind.Electrode)
                {
                    if (pendingElectrode<0) { pendingElectrode=id; notice=$"Первый электрод #{id}; выберите второй."; }
                    else { Pair(pendingElectrode,id); pendingElectrode=-1; }
                }
            }
        }
        private void Turn(int step)
        { orientation=(orientation+step+OrbitalPartCatalog.Orientations.Length)%OrbitalPartCatalog.Orientations.Length; mountsDirty=true; }
        private void OnGUI()
        {
            if (view==null) return;
            if (!ShowPanel) { GUI.Label(new Rect(8,8,360,28),"F1 — панель; Tab — редактор/бой; G — траектории"); return; }
            GUILayout.BeginArea(PanelRect,GUI.skin.box);
            var wrap=new GUIStyle(GUI.skin.label) { wordWrap=true };
            GUILayout.Label("SUBJECT #42 / ОРБИТАЛЬНЫЙ ЦЕХ");
            GUILayout.Label(Busy ? "Восстановление..." : Editing ? "РЕДАКТОР / БОЙ ПРИОСТАНОВЛЕН" : "ВРАЩЕНИЕ / БОЙ");
            GUILayout.Label($"Детали {Assembly.Nodes.Count}/{OrbitalAssembly.MaxParts} | вылет {Assembly.Reach:F1}/8 м");
            GUILayout.Label($"Оборот {TurnSeconds:F2} с | {RotationSpeed:F1}°/с | масса {Balance.TotalMass:F1}");
            GUILayout.Label($"{Elapsed:F1} с | урон {DamageReceived:F0} | враги {Alive} | HP {(Player!=null ? Player.CurrentHealth : 0):F0}");
            GUILayout.Label($"Пилы: попаданий {Combat.ContactHits} | урон попадания {Combat.BladeDamage:F0}");
            if (probing && probeTarget!=null) GUILayout.Label($"Манекен: расстояние {Vector2.Distance(Player.transform.position,probeTarget.transform.position):F1} м; попаданий {ProbePasses}; интервал {ProbeInterval:F2} с");
            scroll=GUILayout.BeginScrollView(scroll);
            GUI.enabled=!Busy;
            if (GUILayout.Button(Editing ? "Вращать / продолжить [Tab]" : "Редактировать / пауза [Tab]")) { if (Editing) Resume(); else Pause(); }
            GUILayout.Label("ОДИН НАБОР / ТРИ МАШИНЫ");
            for (int i=0;i<Presets.Length;i++) if (GUILayout.Button(Presets[i])) LoadPreset(i);
            GUILayout.Label("Балка ×4 · Y ×1 · дисковая пила ×2",wrap);
            ShowCombatTrajectories=GUILayout.Toggle(ShowCombatTrajectories,"Траектории в бою [G]");
            GUILayout.Label("Средняя линия — путь центра пилы; две границы — полоса поражения. Подсказки безвредны.",wrap);
            foreach (var saw in Assembly.Nodes.Where(n=>n.Kind==PartKind.Blade))
            {
                var def=OrbitalPartCatalog.Get(saw.Kind); float radius=saw.Point(def.WorkingCenter).magnitude;
                GUILayout.Label($"Пила #{saw.Id}: r={radius:F2}; полоса {Mathf.Max(0,radius-def.DiskRadius):F2}–{radius+def.DiskRadius:F2} м");
            }
            GUI.enabled=Editing && !Busy;
            showBalanceSettings=GUILayout.Toggle(showBalanceSettings,"Настройки массы и вращения");
            if (showBalanceSettings)
            {
                GUILayout.Label("Скорость = база / (1 + коэффициент × Σ m·r²), с пределами. r — центр детали относительно игрока.",wrap);
                GUI.changed=false;
                BaseRotationSpeed=BalanceSlider("База, °/с",BaseRotationSpeed,30,360);
                InertiaResponse=BalanceSlider("Коэффициент",InertiaResponse,0,.02f);
                MinimumRotationSpeed=BalanceSlider("Минимум, °/с",MinimumRotationSpeed,10,100);
                MaximumRotationSpeed=BalanceSlider("Максимум, °/с",MaximumRotationSpeed,100,360);
                for (int i=0;i<OrbitalPartCatalog.Parts.Length;i++)
                {
                    if (!showArchivedParts && i>(int)PartKind.Blade) continue;
                    var kind=(PartKind)i;
                    Masses.Set(kind,BalanceSlider("Масса / "+OrbitalPartCatalog.Get(kind).Name,Masses.Get(kind),.05f,4));
                }
                if (GUI.changed) RecalculateRotation();
                GUILayout.Label($"Σ m·r² = {Balance.Moment:F1}; урон пилы = {Combat.BladeDamage:F0}, не зависит от массы и длины.",wrap);
            }
            showArchivedParts=GUILayout.Toggle(showArchivedParts,"Прежние детали (вне пресетов)");
            if (!showArchivedParts && ((int)selectedKind>(int)PartKind.Blade || mode==2)) { selectedKind=PartKind.Beam; mode=0; mountsDirty=true; previewParent=-2; }
            int nextMode=GUILayout.Toolbar(mode,showArchivedParts ? new[] { "Сборка", "Удаление", "Пара" } : new[] { "Сборка", "Удаление" });
            if (nextMode!=mode) { mode=nextMode; mountParent=-1; previewParent=-2; mountsDirty=true; view.Preview(null,false); }
            if (mode==0)
            {
                for (int i=0;i<OrbitalPartCatalog.Parts.Length;i++)
                {
                    if (!showArchivedParts && i>(int)PartKind.Blade) continue;
                    var kind=(PartKind)i;
                    if (GUILayout.Button((selectedKind==kind ? "► " : "")+OrbitalPartCatalog.Get(kind).Name+" / осталось "+Assembly.Remaining(kind)))
                    { selectedKind=kind; mountParent=-1; previewParent=-2; mountsDirty=true; }
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◄ Q")) Turn(-1);
                GUILayout.Label($"Поворот {OrbitalPartCatalog.Orientations[orientation]:+0;-0;0}°");
                if (GUILayout.Button("E ►")) Turn(1);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Установить [Enter]") && mountParent>=0) Install(selectedKind,mountParent,mountPort,OrbitalPartCatalog.Orientations[orientation]);
                GUILayout.Label("Выберите деталь → ЛКМ по креплению → Q/E → Enter. Esc снимает выбор.",wrap);
            }
            else if (mode==1)
            {
                GUILayout.Label("ЛКМ по детали; удаляется вся дочерняя ветвь. ПКМ выбирает деталь в любом режиме.",wrap);
                if (GUILayout.Button($"Удалить #{selectedNode} и ветвь [Del]")) Remove(selectedNode);
            }
            else
            {
                GUILayout.Label("ЛКМ по двум электродам: явная пара. Максимум 6 м; механическое дерево уже связывает их.",wrap);
                foreach (var node in Assembly.Nodes.Where(n=>n.Kind==PartKind.Electrode))
                    if (GUILayout.Button("Электрод #"+node.Id))
                    { if (pendingElectrode<0) pendingElectrode=node.Id; else { Pair(pendingElectrode,node.Id); pendingElectrode=-1; } }
                if (GUILayout.Button("Снять энергетическую пару")) { Assembly.Unpair(); view.Bridge(Assembly,false); }
            }
            if (showArchivedParts)
                GUILayout.Label("Щит и электроды оставлены отдельно. Захват крюка отложен и отключён.",wrap);
            GUI.enabled=!Busy;
            GUILayout.Label("СРАВНЕНИЕ / ОДИНАКОВЫЙ СТАРТ");
            GUILayout.Label($"Манекен: стартовое расстояние {DummyDistance:F1} м");
            DummyDistance=GUILayout.HorizontalSlider(DummyDistance,.8f,8);
            if (GUILayout.Button("Манекен: проверка радиуса")) StartContactProbe();
            for (int i=0;i<Waves.Length;i++) if (GUILayout.Button("Волна: "+Waves[i])) StartWave(i);
            if (GUILayout.Button("Перезапустить тренировку")) { if (lastTrainingDummy) StartContactProbe(); else StartWave(lastWave); }
            GUILayout.Label("WASD — подводите пилы к цели · Space — рывок. Внутри дальней окружности враги свободно подходят к игроку.",wrap);
            GUI.enabled=Editing && !Busy;
            GUILayout.Label($"Повтор попадания: {RepeatHitInterval:F2} с");
            RepeatHitInterval=GUILayout.HorizontalSlider(RepeatHitInterval,.1f,.8f);
            GUI.enabled=!Busy;
            if (GUILayout.Button("ПОЛНЫЙ СБРОС")) FullReset();
            foreach (var result in Results) GUILayout.Label(result,wrap);
            GUI.enabled=true;
            GUILayout.EndScrollView(); GUILayout.Label(notice,wrap); GUILayout.EndArea();
            if (Editing && ArenaCamera!=null)
                foreach (var node in Assembly.Nodes.Where(n=>n.Kind==PartKind.Electrode))
                {
                    Vector3 p=ArenaCamera.WorldToScreenPoint(view.Root.TransformPoint(node.Point(OrbitalPartCatalog.Get(node.Kind).Terminal)));
                    GUI.Label(new Rect(p.x+8,Screen.height-p.y-20,70,24),"E#"+node.Id);
                }
        }
        private void OnDestroy()
        {
            Combat.ContactHit-=MeasureContact;
            Combat.ContactImpact-=PresentContact;
            Combat.Reset(Vector2.zero,0);
            if (Player!=null) { Player.DebugDamageApplied-=OnDamage; Player.Died-=Defeated; }
            view?.Dispose(); PlayerRuntimeReference.Clear();
            CrowdSteeringRuntime.SetEnabled(crowdEnabled); Time.timeScale=originalTimeScale;
        }
        private static float BalanceSlider(string name,float value,float minimum,float maximum)
        { GUILayout.Label($"{name}: {value:F3}"); return GUILayout.HorizontalSlider(value,minimum,maximum); }
    }
}
#endif
