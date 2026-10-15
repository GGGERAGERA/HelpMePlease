#if UNITY_EDITOR
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;

public sealed class OrbitalProductionTransferTests
{
    [Test]
    public void RetiredModuleIdsCannotBeInstalled()
    {
        var state = OrbitalRunState.CreateDefault(123);
        state.AddMount(state.Rings[0].StableRingId, out _);
        Assert.That(state.CanInstallModule((OrbitalModuleKind)1, state.Rings[0].StableRingId, 1, out _), Is.False);
        Assert.That(state.CanInstallModule((OrbitalModuleKind)2, state.Rings[0].StableRingId, 1, out _), Is.False);
    }

    [Test]
    public void RetiredModulesMigrateWithoutResettingValidProgress()
    {
        var state = OrbitalRunState.CreateDefault(123);
        state.AddMount(state.Rings[0].StableRingId, out _);
        state.Modules.Add(new OrbitalModuleState { StableModuleId = 2, ModuleType = (OrbitalModuleKind)1,
            StableRingId = state.Rings[0].StableRingId, MountIndex = 1 });
        state.NextStableModuleId = 3;
        state.Version = 2;
        state.MigrateRetiredModules();
        Assert.That(state.Validate(out string error), Is.True, error);
        Assert.That(state.Modules.Count, Is.EqualTo(1));
        Assert.That(state.RunId, Is.EqualTo(123));
        Assert.That(state.Rings[0].MountCount, Is.EqualTo(2));
        Assert.That(state.NextStableModuleId, Is.EqualTo(3));
    }

    [Test]
    public void CategoryStatePersistsBeforeWeaponAcquisition()
    {
        var state = OrbitalRunState.CreateDefault(123);
        Assert.That(state.TryUpgradeCategory(OrbitalRewardKind.ContactDamage), Is.True);
        Assert.That(state.TryUpgradeCategory(OrbitalRewardKind.ShootingRate), Is.True);
        var restored = UnityEngine.JsonUtility.FromJson<OrbitalRunState>(UnityEngine.JsonUtility.ToJson(state));
        restored.AddMount(restored.Rings[0].StableRingId, out _);
        Assert.That(restored.TryInstallModule(OrbitalModuleKind.Saw, restored.Rings[0].StableRingId, 1, out _, out _), Is.True);
        Assert.That(restored.ContactDamageMultiplier, Is.EqualTo(1.2f));
        Assert.That(restored.ShootingRateMultiplier, Is.EqualTo(1.15f));
        Assert.That(restored.ShootingDamageMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void ShootingTargetUpgradeRejectsContactWeapon()
    {
        var state = OrbitalRunState.CreateDefault(123);
        state.AddMount(state.Rings[0].StableRingId, out _);
        state.TryInstallModule(OrbitalModuleKind.Saw, state.Rings[0].StableRingId, 1, out var saw, out _);
        Assert.That(state.CanUpgradeModuleDamage(saw.StableModuleId, out _), Is.False);
        Assert.That(state.CanUpgradeModuleDamage(state.Modules[0].StableModuleId, out _), Is.True);
    }
}
#endif
