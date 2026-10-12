#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subject42.DebugLabs.EliteOrbit
{
    [DefaultExecutionOrder(-3000)]
    public sealed class EliteOrbitalLab : MonoBehaviour
    {
        public const string ScenePath="Assets/_Project/Dev/Labs/OrbitalLab/EliteOrbitalLab.unity";
        public OrbitalPresentationConfig Presentation;
        public LocalizationService LocalizationPrefab;
        public CharacterData Character;
        public GameObject PlayerTemplate,ElitePrefab,OrdinaryPrefab;
        public EliteOrbitPreset[] Presets;
        public Camera ArenaCamera;
        public CameraFollow CameraRig;
        public AudioClip BlockClip,HitClip,NodeBreakClip;
        public EliteOrbitEnemy Elite { get; private set; }
        public PlayerHealth Player { get; private set; }
        public OrbitalStationRuntime Station { get; private set; }
        public EliteOrbitCombat CombatPolicy { get; private set; }
        public bool Busy { get; private set; }
        public bool Paused { get; private set; }
        public bool WithGroup;
        public int WeaponLoad { get; private set; }
        public int Encounter { get; private set; }
        private bool panel=true,ownsManager,encounterGroup;
        private RunStateManager manager;
        private LocalizationService ownedLocalization;
        private readonly List<EnemyHealth> ordinary=new();
        private readonly HashSet<GameObject> authored=new();
        private readonly List<(GameObject effect,float end)> effects=new();
        private Transform effectRoot;
        private AudioSource audioSource;
        private Vector2 scroll;
        private string notice="Выберите наполнение элитника. WASD / Space / ПКМ — прежнее управление.";
        private Rect PanelRect=>new(8,8,Mathf.Min(325,Screen.width*.3f),Screen.height-16);
        private void Awake()
        {
            OrbitalPresentationConfig.Configure(Presentation);
            if (LocalizationService.Instance==null)
            {
                ownedLocalization=Instantiate(LocalizationPrefab);
                SceneManager.MoveGameObjectToScene(ownedLocalization.gameObject,gameObject.scene);
            }
            ownsManager=RunStateManager.Instance==null; manager=RunStateManager.EnsureExists();
            if (ownsManager) SceneManager.MoveGameObjectToScene(manager.gameObject,gameObject.scene);
            CombatPolicy=new EliteOrbitCombat(this); ProductionOrbitalCombatAdapter.LabInterceptor=CombatPolicy;
            effectRoot=new GameObject("Elite lab short feedback").transform; effectRoot.SetParent(transform,false);
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false;
        }
        private void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects()) authored.Add(root);
            StartCoroutine(Recreate(false));
        }
        public void Spawn(int preset) { if (!Busy) { Encounter=Mathf.Clamp(preset,0,Presets.Length-1); encounterGroup=WithGroup; StartCoroutine(Recreate(true)); } }
        public void Restart() { if (!Busy) StartCoroutine(Recreate(true)); }
        public void SetWeapon(int weapon) { if (!Busy) { WeaponLoad=Mathf.Clamp(weapon,0,2); StartCoroutine(Recreate(Elite!=null)); } }
        private IEnumerator Recreate(bool encounter)
        {
            Busy=true; SetPaused(false); Clear();
            Station?.Teardown(); Station=null;
            if (Player!=null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); Player=null; }
            PlayerRuntimeReference.Clear();
            yield return null;
            manager.DebugResetOrbitalRunState();
            var player=Instantiate(PlayerTemplate,transform); player.name="Player / original movement and ORBITAL modules";
            player.transform.position=Vector3.zero; player.SetActive(true);
            Player=player.GetComponent<PlayerHealth>(); Player.SetRuntimeHealth(150,150);
            PlayerLoadoutFactory.ApplyDebugMoveSpeed(player.GetComponent<CharacterMovement2D>(),6);
            PlayerRuntimeReference.Bind(player);
            Station=OrbitalStationRuntime.Ensure(player,Character);
            Station.Interaction.BindCursor(null,ArenaCamera);
            foreach (var module in Station.State.Modules.ToArray()) Station.RemoveModule(module.StableModuleId);
            var ring=Station.State.Rings[0];
            ring.Radius=(WeaponLoad==1 ? 3.25f : 1.3f)-Presentation.RingRadiusPadding;
            ring.BaseRotationSpeed=80; ring.CurrentPhase=0;
            while (ring.MountCount<2) Station.AddMount(ring.StableRingId,out _);
            var kind=WeaponLoad==2 ? OrbitalModuleKind.Pistol : OrbitalModuleKind.LaserSword;
            for (int i=0;i<2;i++) if (!Station.InstallModule(kind,ring.StableRingId,i,out var error)) Debug.LogError(error,this);
            foreach (var runtime in Station.Rings) runtime.Tick(0);
            CameraRig.target=player.transform; CameraRig.transform.position=new Vector3(0,0,-10);
            CameraRig.SetDebugOrthographicSize(7.5f);
            yield return null;
            if (encounter)
            {
                var go=Instantiate(ElitePrefab,new Vector3(4,0,0),Quaternion.identity,transform);
                go.SetActive(true); Elite=go.GetComponent<EliteOrbitEnemy>();
                Elite.Feedback+=Feedback; Elite.Initialize(Presets[Encounter]);
                if (encounterGroup)
                    for (int i=0;i<4;i++)
                    {
                        var ally=Instantiate(OrdinaryPrefab,new Vector3(5.7f+(i%2)*1.1f,(i/2==0 ? 1 : -1)*2.5f,0),Quaternion.identity,transform);
                        var marker=ally.AddComponent<CombatFeelTestDummy>(); marker.Initialize(null); marker.Invulnerable=false;
                        ordinary.Add(ally.GetComponent<EnemyHealth>());
                    }
                notice=Presets[Encounter].Hint;
            }
            CombatPolicy.Reset(); Busy=false;
        }
        public void Clear()
        {
            if (Elite!=null) { Elite.Feedback-=Feedback; Elite.gameObject.SetActive(false); Destroy(Elite.gameObject); Elite=null; }
            foreach (var enemy in ordinary) if (enemy!=null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            ordinary.Clear(); CombatPolicy.Reset();
            (Station?.Combat as ProductionOrbitalCombatAdapter)?.DebugClearProjectiles();
            foreach (var item in effects) if (item.effect!=null) Destroy(item.effect);
            effects.Clear(); audioSource.Stop();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (!authored.Contains(root)) { root.SetActive(false); Destroy(root); }
            notice="Встреча очищена";
        }
        public void SetPaused(bool paused)
        { Paused=paused; if (Station?.InputOwner!=null) Station.InputOwner.ReleaseBulletTime(); Time.timeScale=paused ? 0 : 1; }
        private void Feedback(EliteFeedback kind,Vector2 point)
        {
            Color color=kind==EliteFeedback.Block ? new Color(.45f,.8f,1) : kind==EliteFeedback.BodyHit ? new Color(1,.82f,.25f) : new Color(1,.28f,.7f);
            var root=new GameObject(kind+" / short feedback"); root.transform.SetParent(effectRoot,false); root.transform.position=point;
            int count=kind==EliteFeedback.NodeDestroyed ? 8 : kind==EliteFeedback.Block ? 2 : 3;
            for (int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count;
                var sprite=EliteOrbitEnemy.Pixel("Feedback",root.transform,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.15f,
                    kind==EliteFeedback.Block ? new Vector2(.035f,.35f) : new Vector2(.05f,.19f),color);
                sprite.transform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
            }
            effects.Add((root,Time.time+(kind==EliteFeedback.NodeDestroyed ? .28f : .12f)));
            var clip=kind==EliteFeedback.Block ? BlockClip : kind==EliteFeedback.NodeDestroyed ? NodeBreakClip : HitClip;
            if (clip!=null) audioSource.PlayOneShot(clip,.22f);
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) panel=!panel;
            if (Station?.InputOwner!=null)
                Station.InputOwner.DebugSuppressPlayerInput=Busy || Paused || panel && PanelRect.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y));
            for (int i=effects.Count-1;i>=0;i--)
                if (Time.time>=effects[i].end) { Destroy(effects[i].effect); effects.RemoveAt(i); }
        }
        private void OnGUI()
        {
            if (!panel) { GUI.Label(new Rect(8,8,220,25),"F1 — лабораторная панель"); return; }
            GUILayout.BeginArea(PanelRect,GUI.skin.box);
            var wrap=new GUIStyle(GUI.skin.label) { wordWrap=true };
            GUILayout.Label("SUBJECT #42 / ЭЛИТНЫЕ ОРБИТЫ");
            GUILayout.Label("WASD — движение · Space — рывок\nПКМ — прежнее замедление · F1 — панель",wrap);
            GUILayout.Label(Busy ? "Создание встречи..." : $"Игрок HP {(Player!=null ? Player.CurrentHealth : 0):F0}/150");
            scroll=GUILayout.BeginScrollView(scroll); GUI.enabled=!Busy;
            GUILayout.Label("ОРУЖИЕ ИГРОКА / ПРЕЖНИЕ МОДУЛИ");
            if (GUILayout.Button("Ближний контакт / мечи, r=1.3")) SetWeapon(0);
            if (GUILayout.Button("Дальний контакт / мечи, r=3.25")) SetWeapon(1);
            if (GUILayout.Button("Стрельба / пистолеты")) SetWeapon(2);
            GUILayout.Space(8); GUILayout.Label("ВСТРЕЧА");
            WithGroup=GUILayout.Toggle(WithGroup,"Элитник + 4 обычных врага при spawn");
            for (int i=0;i<Presets.Length;i++) if (GUILayout.Button("Spawn: "+Presets[i].DisplayName)) Spawn(i);
            if (GUILayout.Button("Restart той же встречи")) Restart();
            if (GUILayout.Button("Clear")) Clear();
            if (GUILayout.Button(Paused ? "Продолжить" : "Пауза")) SetPaused(!Paused);
            if (Elite!=null)
            {
                GUILayout.Label($"{Elite.Preset.DisplayName}: HP {Elite.Health.CurrentHealth:F0}/{Elite.Health.MaxHealth:F0}");
                GUILayout.Label($"Орбита: r={Elite.Preset.Radius:F1}, {Elite.Preset.RotationSpeed:F0}°/с; занято {Elite.Preset.Slots.Length}/{Elite.Preset.MountCount}");
                if (Elite.HasShield) GUILayout.Label($"Пластина {Elite.Preset.ShieldArc:F0}°; блоков {Elite.Blocks}; остальной сектор открыт.",wrap);
                if (Elite.Nodes.Count>0) GUILayout.Label($"Защитные узлы {Elite.LiveNodes}/3; тело получает {Elite.DamageMultiplier*100:F0}% урона.",wrap);
            }
            GUILayout.Label(notice,wrap);
            GUILayout.Label("Кольцо и свободные точки безвредны. У модулей отдельные физические зоны. Тактики и баланс проверяйте вручную.",wrap);
            GUI.enabled=true; GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void OnDestroy()
        {
            if (ReferenceEquals(ProductionOrbitalCombatAdapter.LabInterceptor,CombatPolicy)) ProductionOrbitalCombatAdapter.LabInterceptor=null;
            Station?.Teardown(); PlayerRuntimeReference.Clear();
            if (ownsManager && manager!=null) Destroy(manager.gameObject);
            if (ownedLocalization!=null) Destroy(ownedLocalization.gameObject);
            Time.timeScale=1;
        }
    }
}
#endif
