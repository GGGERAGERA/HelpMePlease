using NUnit.Framework;

public sealed class CombatFeelLabSettingsTests
{
    [Test]
    public void ResetAllRestoresEveryDescriptorToNeutral()
    {
        CombatFeelLabSettings settings = new();
        settings.ApplyCharacterPreset(CombatFeelLabSettings.CharacterPreset.Chaotic);
        settings.ResetAll();

        foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
            Assert.That(settings.GetRaw(descriptor.Parameter),
                Is.EqualTo(descriptor.Neutral).Within(.0001f), descriptor.Name);
    }

    [Test]
    public void SaveSlotsRemainIndependent()
    {
        CombatFeelLabSettings settings = new();
        settings.Set(CombatFeelParameter.WeaponKickDistance, .2f);
        settings.SaveA();
        settings.Set(CombatFeelParameter.WeaponKickDistance, .7f);
        settings.SaveB();

        Assert.That(settings.LoadA(), Is.True);
        Assert.That(settings.GetRaw(CombatFeelParameter.WeaponKickDistance),
            Is.EqualTo(.2f).Within(.0001f));
        Assert.That(settings.LoadB(), Is.True);
        Assert.That(settings.GetRaw(CombatFeelParameter.WeaponKickDistance),
            Is.EqualTo(.7f).Within(.0001f));
    }

    [Test]
    public void SoloNeutralizesOtherGroupsWithoutDestroyingTheirValues()
    {
        CombatFeelLabSettings settings = new();
        settings.Set(CombatFeelParameter.WeaponKickDistance, .4f);
        settings.Set(CombatFeelParameter.VisualHitPush, .3f);

        settings.ToggleSolo(CombatFeelGroup.Shot);
        Assert.That(settings.Get(CombatFeelParameter.VisualHitPush), Is.Zero);
        Assert.That(settings.Get(CombatFeelParameter.WeaponKickDistance),
            Is.EqualTo(.4f).Within(.0001f));

        settings.ToggleSolo(CombatFeelGroup.Shot);
        Assert.That(settings.Get(CombatFeelParameter.VisualHitPush),
            Is.EqualTo(.3f).Within(.0001f));
    }

    [Test]
    public void RandomizeCanBeUndoneExactly()
    {
        CombatFeelLabSettings settings = new();
        settings.Set(CombatFeelParameter.ProjectileScale, 1.8f);
        settings.Randomize(CombatFeelGroup.Projectile);

        Assert.That(settings.UndoRandomize(), Is.True);
        Assert.That(settings.GetRaw(CombatFeelParameter.ProjectileScale),
            Is.EqualTo(1.8f).Within(.0001f));
        Assert.That(settings.CanUndoRandomize, Is.False);
    }

    [Test]
    public void CompactConfigOmitsNeutralValues()
    {
        CombatFeelLabSettings settings = new();
        Assert.That(settings.GetCompactConfig(), Is.EqualTo("COMBAT FEEL CONFIG"));

        settings.Set(CombatFeelParameter.KillZoomPunch, -.04f);
        string config = settings.GetCompactConfig();
        StringAssert.Contains("CAMERA:", config);
        StringAssert.Contains("Kill Zoom Punch = -0.04", config);
        StringAssert.DoesNotContain("Projectile Visual Scale", config);
    }
}
