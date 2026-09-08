using NUnit.Framework;
using UnityEngine;

public sealed class BunkerOnboardingTests
{
    private int? saved;
    [SetUp] public void Backup()
    {
        saved = PlayerPrefs.HasKey(BunkerStationProgressionService.OnboardingKey)
            ? PlayerPrefs.GetInt(BunkerStationProgressionService.OnboardingKey) : (int?)null;
        PlayerPrefs.DeleteKey(BunkerStationProgressionService.OnboardingKey);
    }
    [TearDown] public void Restore()
    {
        if (saved.HasValue) PlayerPrefs.SetInt(BunkerStationProgressionService.OnboardingKey, saved.Value);
        else PlayerPrefs.DeleteKey(BunkerStationProgressionService.OnboardingKey);
        PlayerPrefs.Save();
    }
    [Test] public void FreshSave_SequentialVisits_CompletionPersists()
    {
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.Character));
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Character);
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.Weapon));
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Weapon);
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.RunGate));
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Complete);
        Assert.That(BunkerStationProgressionService.GetOnboardingStep(PlayerPrefs.GetInt(BunkerStationProgressionService.OnboardingKey)), Is.EqualTo(BunkerOnboardingStep.Complete));
    }
    [Test] public void WeaponFirst_IsRemembered_CharacterConfirmationStillRequired()
    {
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Weapon);
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.Character));
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Character);
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.RunGate));
    }
    [Test] public void DirectRun_RepeatedVisits_CannotRestartGuidance()
    {
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Complete);
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Weapon);
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Character);
        BunkerStationProgressionService.RecordOnboarding(BunkerOnboardingStep.Character);
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.Complete));
        PlayerPrefs.DeleteKey(BunkerStationProgressionService.OnboardingKey);
        Assert.That(BunkerStationProgressionService.OnboardingStep, Is.EqualTo(BunkerOnboardingStep.Character));
    }
}
