#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed partial class EnemyOrbitalLabController
{
    [Header("Segmented Orbit — lab inventory and geometry")]
    [Range(1,36)] public int SegmentedSegmentBudget = 12;
    [Min(0)] public int SegmentedPistols = 4, SegmentedShotguns = 1, SegmentedSnipers = 1, SegmentedSaws;
    [Range(1,12)] public int SegmentedLinksPerType = 2;
    [Min(.01f)] public float SegmentedExtraShotCooldown = .5f;
    [Range(.1f,1f)] public float SegmentedSkipStrength = .65f;
    public Vector3 SegmentedRadii = new(2.4f, 3.6f, 4.8f);
    public Vector3 SegmentedSpeeds = new(42f, 36f, 30f);
    [Header("Manual Rotation — lab only")]
    [Min(1f)] public float ManualRotationCapacity = 100f;
    [Min(0f)] public float ManualRotationSpeed = 90f, ManualRotationDrain = 25f, ManualRotationRecovery = 12f;
    [Range(12,64)] public int SegmentedEnemyDensity = 48;
    public bool SegmentedOrbit { get; private set; }
    public SegmentedOrbitConfiguration Segmented { get; private set; }
    SegmentedOrbitView segmentedView;
    bool changingExperiment;
    readonly Dictionary<(int ring, int mount), (int slots, string first, string second)> freeIndividualModules = new();

    public void SetSegmentedOrbit(bool active)
    {
        if (!Ready || SegmentedOrbit == active) return;
        SetPaused(true);
        if (active)
        {
            RememberSandboxWeapons();
            freeIndividualModules.Clear();
            foreach (var gun in Station.Modules)
                freeIndividualModules[(gun.CurrentMount.Ring.State.Order, gun.CurrentMount.MountIndex)] =
                    (WeaponModules.SlotCount(gun.StableModuleId), WeaponModules.GetModule(gun.StableModuleId, 0), WeaponModules.GetModule(gun.StableModuleId, 1));
            Placement.Unbind(); sandboxView.RestoreVisuals();
        }
        else { Segmented.Unbind(); segmentedView.RestoreVisuals(); }
        ClearSandboxArena();
        SegmentedOrbit = active;
        changingExperiment = true;
        ResetBuild(() =>
        {
            RestoreLabBuild();
            SetPaused(true);
            if (!active)
                foreach (var gun in Station.Modules)
                {
                    if (!freeIndividualModules.TryGetValue((gun.CurrentMount.Ring.State.Order, gun.CurrentMount.MountIndex), out var saved)) continue;
                    WeaponModules.SetModule(gun.StableModuleId, 0, "EMPTY");
                    if (WeaponModules.SlotCount(gun.StableModuleId) > 1) WeaponModules.SetModule(gun.StableModuleId, 1, "EMPTY");
                    if (saved.slots > 1) WeaponModules.UnlockSecondSlot(gun.StableModuleId);
                    WeaponModules.SetModule(gun.StableModuleId, 0, saved.first);
                    if (saved.slots > 1) WeaponModules.SetModule(gun.StableModuleId, 1, saved.second);
                }
            ClearSandboxArena(); SetPlayerInvincible(true); SetPaused(true);
            changingExperiment = false;
        });
    }
    internal bool InstallSegmentedWeapon(int level, int position, int choice)
    {
        installingProfile = new[] { Pistol, Shotgun, Sniper, Saw }[choice];
        var kind = new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper, OrbitalModuleKind.Saw }[choice];
        installingProfile.Kind = kind;
        try { return Station.InstallModule(kind, Station.Rings[level].RingId, position, out _); }
        finally { installingProfile = null; }
    }
    internal void RebuildSegmentedPreset()
    {
        ((ProductionOrbitalCombatAdapter)Station.Combat).DebugClearProjectiles();
        foreach (var gun in Station.Modules.ToArray()) Station.RemoveModule(gun.StableModuleId);
        weaponProfiles.Clear(); WeaponModules.BeginLoadout();
        Segmented.Bind(Station);
    }
    public void StartSegmentedCombat()
    {
        if (!Ready || !SegmentedOrbit) return;
        if (AliveEnemies == 0) StartSandboxEncounter(4);
        SetPaused(false);
    }
}
#endif
