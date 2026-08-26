using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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
    public void DirtyStateTracksSavedValuesAndResetIsNotSave()
    {
        CombatFeelLabSettings settings = new();
        Assert.That(settings.HasUnsavedChanges, Is.False);

        settings.Set(CombatFeelParameter.WeaponKickDistance, .4f);
        Assert.That(settings.HasUnsavedChanges, Is.True);
        settings.MarkSaved();
        Assert.That(settings.HasUnsavedChanges, Is.False);

        settings.ResetAll();
        Assert.That(settings.HasUnsavedChanges, Is.True,
            "Reset must not replace the saved baseline");
    }

    [Test]
    public void ScreenFractionsAreDisplayedAsReadablePercentages()
    {
        CombatFeelDescriptor deadZone = default;
        foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
            if (descriptor.Parameter == CombatFeelParameter.LookAheadDeadZone)
                deadZone = descriptor;

        Assert.That(deadZone.Metadata.FormatValue(.08f), Is.EqualTo("8% экрана"));
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
        StringAssert.Contains("Убийства зум импульс = -0.04", config);
        StringAssert.DoesNotContain("Снаряда масштаб", config);
    }

    [Test]
    public void AuditRegistryPartitionsAll209AndEveryUiParameterHasConsumer()
    {
        Array parameters = Enum.GetValues(typeof(CombatFeelParameter));
        Assert.That(parameters.Length, Is.EqualTo(218));
        Assert.That(CombatFeelConsumerRegistry.ParametersBeforeAudit, Is.EqualTo(209));
        Assert.That(CombatFeelConsumerRegistry.RemovedCount, Is.EqualTo(72));
        Assert.That(CombatFeelConsumerRegistry.FixedBrokenCount, Is.EqualTo(31));
        Assert.That(CombatFeelLabSettings.Descriptors.Count, Is.EqualTo(146));
        Assert.That(CombatFeelLabSettings.Descriptors.Count +
            CombatFeelConsumerRegistry.RemovedCount, Is.EqualTo(parameters.Length));
        HashSet<CombatFeelParameter> seen = new();
        foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
        {
            Assert.That(seen.Add(descriptor.Parameter), Is.True,
                descriptor.Parameter + " is duplicated");
            CombatFeelParameterMetadata m = descriptor.Metadata;
            Assert.That(m, Is.Not.Null);
            Assert.That(m.TechnicalName, Is.EqualTo(descriptor.Parameter.ToString()));
            Assert.That(m.RussianName, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.DescriptionRu, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.Unit, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.ConsumerPath, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.ConsumerTarget, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.WhatToWatchRu, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.MinimumMeaningRu, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.MaximumMeaningRu, Is.Not.Empty, m.TechnicalName);
            Assert.That(m.AuditStatus, Does.StartWith("WORKING"), m.TechnicalName);
            Assert.That(CombatFeelConsumerRegistry.TryGet(descriptor.Parameter,
                descriptor.Group, out CombatFeelConsumerDeclaration consumer), Is.True);
            Assert.That(consumer.RuntimePath, Is.Not.Empty);
            Assert.That(m.Production, Is.EqualTo(descriptor.Neutral).Within(.0001f));
            Assert.That(m.Neutral, Is.EqualTo(descriptor.Neutral).Within(.0001f));
            Assert.That(m.Minimum, Is.LessThanOrEqualTo(m.Production), m.TechnicalName);
            Assert.That(m.Maximum, Is.GreaterThanOrEqualTo(m.Production), m.TechnicalName);
            Assert.That(m.SafeRandomMinimum, Is.GreaterThanOrEqualTo(m.Minimum), m.TechnicalName);
            Assert.That(m.SafeRandomMaximum, Is.LessThanOrEqualTo(m.Maximum), m.TechnicalName);
            Assert.That(m.SafeRandomMaximum, Is.GreaterThanOrEqualTo(m.SafeRandomMinimum), m.TechnicalName);
            Assert.That(m.DiagnosticExtreme, Is.InRange(m.Minimum, m.Maximum), m.TechnicalName);
            Assert.That(float.IsNaN(m.Minimum) || float.IsInfinity(m.Minimum), Is.False);
            Assert.That(float.IsNaN(m.Maximum) || float.IsInfinity(m.Maximum), Is.False);
        }
        Assert.That(seen.Count, Is.EqualTo(146));
        Assert.That(CombatFeelConsumerRegistry.WasRemoved(
            CombatFeelParameter.TrailLength), Is.True);
        StringAssert.Contains("REDUNDANT", CombatFeelConsumerRegistry.GetRemovalReason(
            CombatFeelParameter.TrailLength));
    }

    [Test]
    public void RandomizeNeverLeavesSafeRandomRanges()
    {
        CombatFeelLabSettings settings = new();
        settings.Randomize();
        foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
        {
            if (descriptor.Parameter == CombatFeelParameter.MasterIntensity) continue;
            float value = settings.GetRaw(descriptor.Parameter);
            Assert.That(value, Is.InRange(descriptor.Metadata.SafeRandomMinimum,
                descriptor.Metadata.SafeRandomMaximum), descriptor.Parameter.ToString());
        }
    }

    [Test]
    public void InsaneIsMoreExtremeThanHardForEveryNumericParameter()
    {
        foreach (CombatFeelGroup group in Enum.GetValues(typeof(CombatFeelGroup)))
        {
            CombatFeelLabSettings settings = new();
            settings.ApplyGroupPreset(group, CombatFeelLabSettings.GroupPreset.Hard);
            Dictionary<CombatFeelParameter, float> hard = new();
            foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
                if (descriptor.Group == group) hard[descriptor.Parameter] =
                    settings.GetRaw(descriptor.Parameter);
            settings.ApplyGroupPreset(group, CombatFeelLabSettings.GroupPreset.Insane);
            foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
            {
                if (descriptor.Group != group || descriptor.Toggle) continue;
                float hardDistance = Math.Abs(hard[descriptor.Parameter] - descriptor.Neutral);
                float insaneDistance = Math.Abs(settings.GetRaw(descriptor.Parameter) - descriptor.Neutral);
                Assert.That(insaneDistance, Is.GreaterThan(hardDistance),
                    descriptor.Parameter.ToString());
            }
        }
    }

    [Test]
    public void ExperimentRangesAreNeverNarrowerThanAuthoredRanges()
    {
        foreach (CombatFeelDescriptor descriptor in CombatFeelLabSettings.Descriptors)
        {
            CombatFeelParameterMetadata m = descriptor.Metadata;
            Assert.That(m.Minimum, Is.LessThanOrEqualTo(m.AuthoredMinimum), m.TechnicalName);
            Assert.That(m.Maximum, Is.GreaterThanOrEqualTo(m.AuthoredMaximum), m.TechnicalName);
        }
    }

    [Test]
    public void ProjectileWeaponComposesProductionAndCombatFeelLayers()
    {
        GameObject root = new("Weapon");
        GameObject visualObject = new("Visual");
        visualObject.transform.SetParent(root.transform, false);
        visualObject.transform.localPosition = new Vector3(1f, 2f, 0f);
        visualObject.transform.localScale = new Vector3(2f, 3f, 1f);
        try
        {
            ProjectileWeapon weapon = root.AddComponent<ProjectileWeapon>();
            typeof(ProjectileWeapon).GetField("recoilVisual",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                    weapon, visualObject.transform);
            typeof(ProjectileWeapon).GetMethod("CaptureRecoilRestState",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(weapon, null);

            weapon.SetCombatFeelLayer(new Vector3(.5f, -.25f, 0f), 30f,
                new Vector2(.5f, -.25f));

            Assert.That(visualObject.transform.localPosition.x, Is.EqualTo(1.5f).Within(.001f));
            Assert.That(visualObject.transform.localPosition.y, Is.EqualTo(1.75f).Within(.001f));
            Assert.That(visualObject.transform.localScale.x, Is.EqualTo(3f).Within(.001f));
            Assert.That(visualObject.transform.localScale.y, Is.EqualTo(2.25f).Within(.001f));
            Assert.That(visualObject.transform.localEulerAngles.z, Is.EqualTo(30f).Within(.01f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void CameraShakeKeepsCombatFeelOffsetWhenProductionShakeRestores()
    {
        GameObject cameraObject = new("Camera Shake");
        cameraObject.transform.localPosition = new Vector3(2f, 3f, 0f);
        try
        {
            CameraShake shake = cameraObject.AddComponent<CameraShake>();
            shake.SetCombatFeelOffset(new Vector3(1f, -.5f, 0f));
            shake.StopAllShakes();
            Assert.That(cameraObject.transform.localPosition.x, Is.EqualTo(3f).Within(.001f));
            Assert.That(cameraObject.transform.localPosition.y, Is.EqualTo(2.5f).Within(.001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void MouseLookAheadSignalUsesStableScreenSpaceDeadZoneAndSaturation()
    {
        Vector2 screen = new(1920f, 1080f);
        Assert.That(CameraFollow.EvaluateMouseLookAheadSignal(
            screen * .5f, screen, .1f, .65f, 1f), Is.EqualTo(Vector2.zero));
        Assert.That(CameraFollow.EvaluateMouseLookAheadSignal(
            screen * .5f + Vector2.right * 20f, screen, .1f, .65f, 1f),
            Is.EqualTo(Vector2.zero));

        Vector2 right = CameraFollow.EvaluateMouseLookAheadSignal(
            new Vector2(1920f, 540f), screen, .1f, .65f, 1f);
        Vector2 left = CameraFollow.EvaluateMouseLookAheadSignal(
            new Vector2(0f, 540f), screen, .1f, .65f, 1f);
        Assert.That(right.x, Is.EqualTo(1f).Within(.001f));
        Assert.That(left.x, Is.EqualTo(-1f).Within(.001f));
        Assert.That(right.y, Is.Zero.Within(.001f));
    }

    [Test]
    public void HigherLookAheadExponentRespondsLessAtMidScreenDistance()
    {
        Vector2 screen = new(1920f, 1080f);
        Vector2 mouse = screen * .5f + Vector2.right * 180f;
        float linear = CameraFollow.EvaluateMouseLookAheadSignal(
            mouse, screen, 0f, .65f, 1f).magnitude;
        float delayed = CameraFollow.EvaluateMouseLookAheadSignal(
            mouse, screen, 0f, .65f, 2f).magnitude;
        Assert.That(delayed, Is.LessThan(linear));
    }
}
