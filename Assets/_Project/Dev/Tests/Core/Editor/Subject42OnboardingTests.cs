#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OnboardingTests
{
    private static readonly string[] Keys = { RunMessageService.MovementHintPreferenceKey,
        RunMessageService.AutoAttackHintPreferenceKey, RunMessageService.ExperienceHintPreferenceKey,
        RunMessageService.SlowFieldHintPreferenceKey };
    private const string Backup = "Subject42.OnboardingTests.";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void PreservePreferences()
    {
        CoreTestSupport.PreservePreferences();
        foreach (var key in Keys)
        {
            SessionState.SetBool(Backup + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetInt(Backup + key, PlayerPrefs.GetInt(key));
            PlayerPrefs.DeleteKey(key);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        foreach (var key in Keys)
        {
            if (SessionState.GetBool(Backup + key + ".exists", false))
                PlayerPrefs.SetInt(key, SessionState.GetInt(Backup + key, 0));
            else PlayerPrefs.DeleteKey(key);
            SessionState.EraseBool(Backup + key + ".exists");
            SessionState.EraseInt(Backup + key);
        }
        PlayerPrefs.Save();
    }

    [Test]
    public void OpeningReliefEndsWithoutWeakeningLaterThreat()
    {
        var config = AssetDatabase.LoadAssetAtPath<RunThreatConfig>("Assets/_Project/Data/World/RunThreatConfig.asset");
        Assert.That(config.IsOpening(0f, 0f), Is.True);
        Assert.That(config.IsOpening(9f, 54f), Is.True);
        Assert.That(config.IsOpening(9f, 55f), Is.False);
        Assert.That(config.IsOpening(25f, 10f), Is.False);
        Assert.That(config.MeleeSpeed(24.99f), Is.EqualTo(.87f).Within(.001f));
        Assert.That(config.MeleeSpeed(25f), Is.EqualTo(config.MeleeSpeed(24.99f)).Within(.001f));
        Assert.That(config.MeleeSpeed(37.5f), Is.InRange(.9f, .97f));
        Assert.That(config.MeleeSpeed(50f), Is.EqualTo(1f));
        Assert.That(config.MeleeResponseSeconds(50f), Is.Zero);
    }

    [UnityTest]
    public IEnumerator FieldAndFirstRunHintsSurviveReleaseExhaustionDisableAndReload()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        // Allocate captured locals after the EnterPlayMode domain reload.
        yield return RunSmokeInPlayMode();
    }

    private static IEnumerator RunSmokeInPlayMode()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset");
        var rule = AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_None.asset");
        var anomaly = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Gravity.asset");
        RunStateManager.EnsureExists().BeginNewRun(character, null, stage, rule, anomaly, true);
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => PlayerRuntimeReference.CachedPlayer != null && HUDManager.Instance.IsInformationVisible);
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(10000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        Assert.That(station, Is.Not.Null, "Player station must be composed before the smoke");
        Assert.That(station.IsInitialized, Is.True);
        var messages = RunMessageService.Instance;
        Assert.That(messages, Is.Not.Null, "RunMessageService must be enabled with the HUD");
        var hint = HUDManager.Instance.GetComponentsInChildren<Transform>(true)
            .Single(t => t.name == "MovementHint").gameObject;
        var text = hint.GetComponentInChildren<TMP_Text>(true);
        Assert.That(hint, Is.Not.Null, "Authored onboarding panel is required");
        Assert.That(text, Is.Not.Null, "Authored onboarding text is required");
        yield return CoreTestSupport.Await(() => hint.activeSelf);

        Assert.That(text.text, Does.Contain(LocalizationService.Instance.Get("onboarding.movement")));
        var movement = player.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.right;
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[0]) == 1);
        movement.MovementIntent = () => Vector2.zero;

        var tick = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", Private);
        tick.Invoke(station, new object[] { .3f, true, true });
        Assert.That(station.SlowField.HasBeenUsed, Is.True);
        tick.Invoke(station, new object[] { .3f, false, false });
        station.BulletTime.Reset();

        // A real enemy lifecycle and XP acquisition trigger the remaining hints.
        var encounter = new GameObject("Onboarding encounter", typeof(EnemyHealth));
        yield return CoreTestSupport.Await(() => text.text.Contains(LocalizationService.Instance.Get("onboarding.autoAttack")));
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[1]) == 1);
        ExperienceManager.Instance.AddExperience(1);
        yield return CoreTestSupport.Await(() => text.text.Contains(LocalizationService.Instance.Get("onboarding.experience")));
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[2]) == 1);
        yield return CoreTestSupport.Await(() => text.text.Contains(LocalizationService.Instance.Get("onboarding.slowField")));
        tick.Invoke(station, new object[] { .1f, true, true });
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[3]) == 1);
        Object.Destroy(encounter);

        var threat = Object.FindFirstObjectByType<RunThreatController>();
        Assert.That(threat.DisplayedTier, Is.EqualTo(ThreatTier.Tier1));
        RunStateManager.Instance.AdvanceThreat(25f, 1f);
        yield return null;
        Assert.That(threat.DisplayedTier, Is.EqualTo(ThreatTier.Tier2));

        // Reload the gameplay scene in a fresh run, retaining saved onboarding flags.
        RunStateManager.Instance.BeginNewRun(character, null, stage, rule, anomaly, true);
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => HUDManager.Instance.IsInformationVisible);
        messages = RunMessageService.Instance;
        hint = HUDManager.Instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MovementHint").gameObject;
        yield return new WaitForSeconds(6f);
        Assert.That(hint.activeSelf, Is.False, "Completed hints must not return on a fresh run");
        foreach (var key in Keys) Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
    }
}
#endif
