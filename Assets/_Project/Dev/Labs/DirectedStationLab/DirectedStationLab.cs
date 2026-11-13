#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using UnityEngine;
using Part = Subject42.Combat.Telekinetic.TelekineticPart;

namespace Subject42.DebugLabs.Telekinetic
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class DirectedStationLab : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Project/Dev/Labs/DirectedStationLab/DirectedStationLab.unity";

        [SerializeField] private CharacterData character;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private OrbitalPresentationConfig presentation;
        [SerializeField, Min(0)] private int enemyCount = 12;

        private static readonly Part[] Catalogue = Enum.GetValues(typeof(Part)).Cast<Part>()
            .Where(kind => kind is not (Part.Saw or Part.Frame)).ToArray();
        private readonly List<EnemyHealth> enemies = new();
        private readonly List<Transform> sniperMounts = new();
        private RunStateManager run;
        private OrbitalStationRuntime station;
        private PlayerHealth player;
        private Rigidbody2D playerBody;
        private System.Random spawnRandom;
        private int preset;
        private bool ownsProfile, wasEditing;
        private Vector2 aimPoint;
        private int stockRevision = -1;
        private bool Editing => station != null && station.InputOwner != null && station.InputOwner.IsConstructing;

        private void Start()
        {
            if (RunStateManager.Instance != null || PlayerPrefs.IsSandboxActive)
                throw new InvalidOperationException("Open Directed Station Lab as a standalone scene.");

            PlayerPrefs.BeginSandbox(fresh: true);
            ownsProfile = true;
            OrbitalPresentationConfig.Configure(presentation);
            run = RunStateManager.EnsureExists();
            var state = run.DebugResetOrbitalRunState();
            state.EnableSegmentedOrbit();
            state.Telekinetic = CreatePreset(0);
            state.TelekineticAutoRotate = false;

            var avatar = Instantiate(character.ProductionPrefab, transform);
            avatar.name = "Subject #42";
            player = avatar.GetComponent<PlayerHealth>();
            player.SetRuntimeHealth(character.maxHealth, character.maxHealth);
            player.DebugInvulnerable = true;
            playerBody = avatar.GetComponent<Rigidbody2D>();
            avatar.GetComponent<CharacterMovement2D>().MovementIntent = ReadMovement;
            PlayerRuntimeReference.Bind(avatar);
            station = OrbitalStationRuntime.Ensure(avatar, character);
            ResetArena();
            BindRig();
            SetEnemyCount(enemyCount);
        }

        private Vector2 ReadMovement() => Editing || !Application.isFocused || densityFocused
            ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        private void BindRig()
        {
            if (!station.IsInitialized) throw new InvalidOperationException("Could not initialize the lab's Telekinetic Rig.");

            // The lab drives the same rig and constructor directly. The station's orbit,
            // pointer steering and reward presentation have no input to this arena.
            var view = station.GetComponent<OrbitalStationView>();
            station.enabled = view.Input.enabled = view.CenterShift.enabled = false;
            view.Presentation.enabled = view.Relocation.enabled = view.Rewards.enabled = false;
            view.Presentation.BindCamera(arenaCamera);
            var combat = (ProductionOrbitalCombatAdapter)station.Combat;
            combat.ProjectileVisualImpact = null;
            combat.ChargedLineVisualImpact = null;
            RefreshMounts();
        }

        private void RefreshMounts()
        {
            sniperMounts.Clear();
            foreach (var item in station.Rig.Build.Items)
            {
                if (!item.IsWeapon) continue;
                var mount = station.Rig.Construction.Find(TelekineticBuild.Label(item.Kind) + " #" + item.Id);
                mount.localRotation = Quaternion.identity;
                if (item.Kind == Part.Sniper) sniperMounts.Add(mount);
            }
        }

        private void Update()
        {
            if (station == null || !station.IsInitialized) return;
            if (Input.GetKeyDown(KeyCode.Tab)) SetEditing(!Editing);
            if (Editing)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && !station.Rig.CancelSelection()) SetEditing(false);
                station.Rig.AutoRotate = false;
                if (stockRevision != station.Rig.StructureView.RebuildVersion)
                {
                    ReplenishStock(station.Rig.Build);
                    stockRevision = station.Rig.StructureView.RebuildVersion;
                }
            }
            else ReadCombatShortcuts();
        }

        private void LateUpdate()
        {
            if (station == null || !station.IsInitialized || Editing) return;
            if (wasEditing) FinishEditing();

            Vector3 center = player.transform.position;
            aimPoint += (Vector2)(center - arenaCamera.transform.position);
            arenaCamera.transform.position = new Vector3(center.x, center.y, arenaCamera.transform.position.z);
            Vector2 mouse = Input.mousePosition;
            if (Application.isFocused && arenaCamera.pixelRect.Contains(mouse) && !PointerOverControls(mouse))
            {
                aimPoint = arenaCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y,
                    center.z - arenaCamera.transform.position.z));
            }
            TickRig(Time.deltaTime, aimPoint);
            MaintainEnemies();
        }

        private void TickRig(float deltaTime, Vector2 cursor)
        {
            var rig = station.Rig;
            Vector2 forward = cursor - (Vector2)player.transform.position;
            if (forward.sqrMagnitude > .0001f)
                station.State.TelekineticAngle = Mathf.Repeat(Vector2.SignedAngle(Vector2.right, forward), 360f);

            // Commit the pose before the weapon clocks advance, so shots originate at
            // the visible mounts even on the first frame of a large mouse turn.
            rig.Construction.rotation = Quaternion.Euler(0, 0, station.State.TelekineticAngle);
            foreach (var mount in sniperMounts)
            {
                Vector2 direction = cursor - (Vector2)mount.position;
                mount.right = direction.sqrMagnitude > .0001f ? direction : (Vector2)rig.Construction.right;
            }
            rig.Tick(deltaTime, false, false, true);
        }

        private void SetEditing(bool value)
        {
            if (value == Editing) return;
            if (value)
            {
                if (!station.InputOwner.BeginConstructor()) return;
                wasEditing = true;
                playerBody.linearVelocity = Vector2.zero;
                ReplenishStock(station.Rig.Build);
                stockRevision = station.Rig.StructureView.RebuildVersion;
                station.Rig.SetConstructorVisible(true);
            }
            else
            {
                station.Interaction.CloseConstructor();
                FinishEditing();
            }
        }

        // The existing constructor can also close itself through its own button.
        private void FinishEditing()
        {
            wasEditing = false;
            station.Rig.AutoRotate = false;
            RefreshMounts();
        }

        private void SelectPreset(int index)
        {
            var build = CreatePreset(index);
            SetEditing(false);
            preset = index;
            station.State.Telekinetic = build;
            ResetFight();
        }

        private void ResetFight()
        {
            SetEditing(false);
            ResetArena();
            foreach (var item in station.Rig.Build.Items)
            {
                item.ShotRemaining = 0f;
                item.Shots = 0;
            }
            station.Rig.AutoRotate = false;
            station.State.TelekineticAngle = 0f;
            station.RebuildRuntimeFromState();
            BindRig();
        }

        private void ResetArena()
        {
            foreach (var enemy in enemies) RemoveEnemy(enemy);
            enemies.Clear();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = Vector2.zero;
            player.transform.position = Vector3.zero;
            arenaCamera.transform.position = new Vector3(0, 0, arenaCamera.transform.position.z);
            spawnRandom = new System.Random(42);
            aimPoint = Vector2.right * 10f;
        }

        private static TelekineticBuild CreatePreset(int index)
        {
            var build = new TelekineticBuild();
            void Add(Part kind, int x, int y, int turns = 0)
            {
                if (!build.Place(kind, new Vector2Int(x, y), turns))
                    throw new InvalidOperationException($"Invalid directed station preset {index}, {kind} at ({x}, {y}). {build.Message}");
            }

            switch (index)
            {
                case 0:
                    Add(Part.Pistol, 1, 0);
                    break;
                case 1:
                    Add(Part.Pistol, 1, 0);
                    Add(Part.Shotgun, 2, 0, 1);
                    Add(Part.Pistol, 4, 0);
                    Add(Part.Sniper, -1, 0, 3);
                    break;
                case 2:
                    Add(Part.Shotgun, 1, 0);
                    Add(Part.DMG, 2, 0);
                    Add(Part.Energy, 3, 0);
                    Add(Part.DMG, 0, 1);
                    Add(Part.FR, 1, 2);
                    Add(Part.Sniper, 1, -1, 2);
                    Add(Part.Pistol, 1, -3);
                    Add(Part.Pistol, 1, 3);
                    Add(Part.Pistol, 2, 2);
                    Add(Part.Pistol, 2, -1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
            ReplenishStock(build);
            return build;
        }

        private static void ReplenishStock(TelekineticBuild build)
        {
            foreach (var kind in Catalogue)
                if (!build.Reserve.Any(item => item.Kind == kind)) build.Grant(kind);
        }

        private void SetEnemyCount(int value)
        {
            enemyCount = Mathf.Max(0, value);
            densityText = enemyCount.ToString();
            MaintainEnemies();
        }

        private void MaintainEnemies()
        {
            enemies.RemoveAll(enemy => enemy == null || enemy.IsDead);
            while (enemies.Count > enemyCount)
            {
                var enemy = enemies[enemies.Count - 1];
                enemies.RemoveAt(enemies.Count - 1);
                RemoveEnemy(enemy);
            }
            // Spread creation over frames while converging on the requested density.
            int batch = Mathf.Min(enemyCount - enemies.Count, 12);
            for (int i = 0; i < batch; i++)
            {
                float angle = (float)spawnRandom.NextDouble() * 2f * Mathf.PI;
                float distance = 7f + (float)spawnRandom.NextDouble() * 3f;
                Vector2 center = player.transform.position;
                center.x = Mathf.Clamp(center.x, -29f + distance, 29f - distance);
                center.y = Mathf.Clamp(center.y, -29f + distance, 29f - distance);
                SpawnEnemy(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
            }
        }

        private EnemyHealth SpawnEnemy(Vector2 position)
        {
            var target = Instantiate(enemyPrefab, position, Quaternion.identity, transform);
            var dummy = target.AddComponent<LabEnemyDummy>();
            dummy.Initialize();
            dummy.Invulnerable = false;
            dummy.Health.SetRuntimeMaxHealth(32);
            dummy.Health.NotifySpawnConfigured();
            enemies.Add(dummy.Health);
            return dummy.Health;
        }

        private static void RemoveEnemy(EnemyHealth enemy)
        {
            if (enemy == null) return;
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }

        private void OnDisable()
        {
            if (Editing) SetEditing(false);
        }

        private void OnDestroy()
        {
            if (player != null && PlayerRuntimeReference.CachedPlayer == player.gameObject)
                PlayerRuntimeReference.Clear();
            if (run != null) Destroy(run.gameObject);
            if (ownsProfile) PlayerPrefs.EndSandbox();
        }
    }
}
#endif
