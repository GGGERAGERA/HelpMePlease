#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using UnityEngine;

namespace Subject42.DebugLabs.Telekinetic
{
    public sealed partial class TelekineticConstructorLab
    {
        private enum OrbitMode { Auto, Manual, Assist }
        private enum OrbitEnemies { Regular, Flanker, Breacher }

        [SerializeField] private CharacterData orbitCharacter;
        [SerializeField] private GameObject flankerPrefab, breacherPrefab;
        [SerializeField, Range(20, 240)] private float orbitSpeed = 90f;
        private bool orbitExperiment, orbitFireHeld, orbitFirePressed, orbitAiming;
        private Vector2 orbitDragPoint;
        private OrbitMode orbitMode;
        private OrbitEnemies orbitEnemies;
        private RunStateManager orbitRunState;
        private OrbitalStationRuntime orbitStation;
        private TelekineticItem primaryWeapon;
        private Transform primaryView;
        private System.Random orbitSpawnRandom = new(42);

        private int OrbitEnemyCount => orbitEnemies switch
        {
            OrbitEnemies.Flanker => 20,
            OrbitEnemies.Breacher => 10,
            _ => Mathf.Max(0, EnemyDensity)
        };

        private void StartOrbitFight()
        {
            if (orbitRunState == null)
            {
                // Only the standalone lab owns this run state.
                if (RunStateManager.Instance != null)
                    throw new System.InvalidOperationException("Open TelekineticConstructorLab as a standalone scene.");
                orbitRunState = RunStateManager.EnsureExists();
            }

            var state = orbitRunState.DebugResetOrbitalRunState();
            state.Telekinetic = Build;
            state.TelekineticAngle = Angle;
            orbitStation = OrbitalStationRuntime.Ensure(Player.gameObject, orbitCharacter);
            var stationView = orbitStation.GetComponent<OrbitalStationView>();
            // The lab already owns time, center movement, constructor input and enemy spawning.
            orbitStation.enabled = false;
            stationView.Input.enabled = false;
            stationView.CenterShift.enabled = false;
            stationView.Presentation.enabled = false;
            stationView.Relocation.enabled = false;
            stationView.Rewards.enabled = false;
            // These optional callbacks use the station's own Update to expire their pooled flashes.
            var rigCombat = (ProductionOrbitalCombatAdapter)orbitStation.Combat;
            rigCombat.ProjectileVisualImpact = null;
            rigCombat.ChargedLineVisualImpact = null;
            orbitStation.transform.position = (Vector2)Player.transform.position + CenterOffset;

            var rig = orbitStation.Rig;
            primaryWeapon = Build.Items.FirstOrDefault(item => item.IsWeapon && item.Kind != TelekineticPart.Saw);
            if (primaryWeapon != null)
                primaryView = rig.Construction.Find(TelekineticBuild.Label(primaryWeapon.Kind) + " #" + primaryWeapon.Id);
        }

        private void StopOrbitFight()
        {
            if (orbitStation == null) return;
            Angle = orbitStation.Rig.Angle;
            orbitStation.Teardown();
            Destroy(orbitStation.gameObject);
            orbitStation = null;
            primaryWeapon = null;
            primaryView = null;
            ReleaseOrbitPointer();
        }

        private void ReleaseOrbitPointer()
        {
            orbitFireHeld = orbitFirePressed = orbitAiming = false;
        }

        private void ReadOrbitPointer(Event mouseEvent)
        {
            if (mouseEvent.button != 0) return;
            Vector2 point = new(mouseEvent.mousePosition.x, Screen.height - mouseEvent.mousePosition.y);
            switch (mouseEvent.type)
            {
                case EventType.MouseDown:
                    orbitFireHeld = orbitFirePressed = IsOrbitArena(point);
                    orbitDragPoint = point;
                    break;
                case EventType.MouseDrag when orbitFireHeld:
                    orbitDragPoint = point;
                    break;
                case EventType.MouseUp:
                    orbitFireHeld = false;
                    break;
            }
        }

        private void TickOrbitFight(bool focused, float deltaTime)
        {
            bool trigger = orbitFireHeld || orbitFirePressed;
            Vector2 point = trigger ? orbitDragPoint : (Vector2)Input.mousePosition;
            orbitFirePressed = false;
            bool pointerAllowed = focused && IsOrbitArena(point);
            Vector3 screen = new(point.x, point.y, -ArenaCamera.transform.position.z);
            TickOrbitRig(deltaTime, pointerAllowed, pointerAllowed && trigger, ArenaCamera.ScreenToWorldPoint(screen));
        }

        private void TickOrbitRig(float deltaTime, bool pointerAllowed, bool manualFire, Vector2 cursor)
        {
            var rig = orbitStation.Rig;
            orbitStation.transform.position = (Vector2)Player.transform.position + CenterOffset;
            rig.AutoRotationSpeed = orbitSpeed;
            orbitAiming = primaryWeapon != null && pointerAllowed && (orbitMode == OrbitMode.Manual || orbitMode == OrbitMode.Assist && manualFire);
            if (primaryWeapon != null) AimPrimary(orbitAiming, cursor);

            bool holdPrimary = primaryWeapon != null && orbitMode == OrbitMode.Manual && !manualFire;
            float remaining = 0f;
            if (holdPrimary)
            {
                remaining = Mathf.Max(0f, primaryWeapon.ShotRemaining - deltaTime * rig.RateFactor(primaryWeapon));
                // Keep the rig's shot path and module timing. Only withhold this trigger for the current tick.
                primaryWeapon.ShotRemaining = float.PositiveInfinity;
            }

            int before = rig.ShotsFired;
            try { rig.Tick(deltaTime, false, false, true); }
            finally { if (holdPrimary) primaryWeapon.ShotRemaining = remaining; }
            ShotsFired += rig.ShotsFired - before;
            if (orbitAiming) AimPrimary(true, cursor);
        }

        private void AimPrimary(bool aim, Vector2 cursor)
        {
            primaryView.localRotation = Quaternion.Euler(0, 0, primaryWeapon.Heading);
            Vector2 direction = cursor - (Vector2)primaryView.position;
            if (aim && direction.sqrMagnitude > .0001f) primaryView.right = direction;
        }

        private void SpawnOrbitEnemy()
        {
            float angle = (float)(orbitSpawnRandom.NextDouble() * 2 * System.Math.PI);
            float distance = 8f + (float)orbitSpawnRandom.NextDouble() * 4f;
            var position = (Vector2)Player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            var prefab = orbitEnemies switch
            {
                OrbitEnemies.Flanker => flankerPrefab,
                OrbitEnemies.Breacher => breacherPrefab,
                _ => EnemyPrefab
            };
            SpawnEnemy(prefab, position, false, orbitEnemies == OrbitEnemies.Regular ? EnemyMaxHealth : null);
        }

        private void SelectOrbitMode(OrbitMode mode)
        {
            orbitMode = mode;
            ReleaseOrbitPointer();
            if (!orbitExperiment)
            {
                orbitExperiment = true;
                NewFight();
            }
            else if (Editing) SetEditing(false);
        }

        private void SelectOrbitEnemies(OrbitEnemies preset)
        {
            if (orbitEnemies == preset) return;
            orbitEnemies = preset;
            NewFight();
        }
    }
}
#endif
