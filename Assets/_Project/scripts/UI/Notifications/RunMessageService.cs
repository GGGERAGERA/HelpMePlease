using System.Collections;
using UnityEngine;
using TMPro;
using Subject42.Combat.OrbitalStation;

public sealed class RunMessageService : MonoBehaviour
{
    private const float InitialLevelMessageDuration = 4.5f;
    private const float FirstRunHintDuration = 3f;
    public const string MovementHintPreferenceKey = "onboarding.mvp.movement.seen";
    public static RunMessageService Instance { get; private set; }

    [SerializeField] private RunMessageView view;
    public RunMessageView View => view;
    [SerializeField] private RunMessageData[] messages;
    [SerializeField] private GameObject movementHint;
    [SerializeField] private TextMeshProUGUI movementText;
    [SerializeField] private HUDManager hud;
    public const string AutoAttackHintPreferenceKey = "onboarding.mvp.autoAttack.seen";
    public const string ExperienceHintPreferenceKey = "onboarding.mvp.experience.seen";
    public const string SlowFieldHintPreferenceKey = "onboarding.mvp.slowField.seen";
    private static readonly string[] PreferenceKeys = { MovementHintPreferenceKey,
        AutoAttackHintPreferenceKey, ExperienceHintPreferenceKey, SlowFieldHintPreferenceKey };
    private static readonly string[] HintKeys = { "onboarding.movement", "onboarding.autoAttack",
        "onboarding.experience", "onboarding.slowField" };
    private int hintIndex;
    private float hintShownTime;
    private bool hintsReady, moved, sawEnemies, gainedExperience;
    private Vector3 movementOrigin;
    private GameObject player;
    private CharacterMovement2D movement;
    private OrbitalStationRuntime station;

    private void Awake()
    {
        movementHint.SetActive(false);
    }

    private void OnEnable()
    {
        Instance = this;
        hintsReady = moved = sawEnemies = gainedExperience = false;
        hintIndex = 0;
        hintShownTime = 0f;
        player = null;
        StartCoroutine(PrepareHints());
    }

    private IEnumerator PrepareHints()
    {
        yield return null;
        LevelAnomalyController anomalyController =
            LevelAnomalyController.Instance;
        WorldRuleController worldRuleController =
            WorldRuleController.Instance;

        if (anomalyController != null || worldRuleController != null)
        {
            while ((anomalyController != null &&
                    !anomalyController.IsIntroComplete) ||
                   (worldRuleController != null &&
                    !worldRuleController.IsIntroComplete))
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(
                InitialLevelMessageDuration
            );
        }

        RunStateManager runState = RunStateManager.Instance;

        if (runState == null || runState.CurrentSector == null ||
            runState.CurrentSector.SectorNumber != RunRoute.FirstSector) yield break;
        hintsReady = true;
    }

    private void LateUpdate()
    {
        if (TutorialController.IsActive) { movementHint.SetActive(false); return; }
        var currentPlayer = PlayerRuntimeReference.CachedPlayer;
        if (currentPlayer != player)
        {
            player = currentPlayer;
            movement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
            if (player != null) movementOrigin = player.transform.position;
        }
        if (player != null && movement != null && movement.HasMovementInput &&
            (player.transform.position - movementOrigin).sqrMagnitude >= .04f) moved = true;
        sawEnemies |= EnemyHealth.ActiveInstances.Count > 0;
        var xp = ExperienceManager.Instance;
        gainedExperience |= xp != null && (xp.CurrentExp > 0 || xp.CurrentLevel > 1);

        while (hintIndex < PreferenceKeys.Length && PlayerPrefs.GetInt(PreferenceKeys[hintIndex], 0) != 0)
            hintIndex++;
        bool visible = hud.IsInformationVisible;
        bool showHint = visible && hintsReady && hintIndex < PreferenceKeys.Length &&
            (hintIndex == 0 || hintIndex == 1 && sawEnemies || hintIndex == 2 && gainedExperience ||
             hintIndex == 3 && station != null && station.IsInitialized && station.SlowFieldRadius > 0f);
        if (showHint)
        {
            hintShownTime += Time.deltaTime;
            bool completed = hintIndex == 0 ? moved : hintIndex == 3 ? station.SlowField.HasBeenUsed :
                hintShownTime >= FirstRunHintDuration;
            if (completed)
            {
                PlayerPrefs.SetInt(PreferenceKeys[hintIndex], 1);
                PlayerPrefs.Save();
                hintIndex++;
                hintShownTime = 0f;
                showHint = false;
            }
        }
        movementHint.SetActive(showHint);
        if (showHint) movementText.text = LocalizationService.Instance.Get(HintKeys[hintIndex]);
    }

    public void Show(RunMessageType type)
    {
        RunMessageData data = FindMessage(type);

        if (data == null)
        {
            Debug.LogWarning($"[RunMessageService] Message not found: {type}");
            return;
        }

        Show(data, type == RunMessageType.BossIncoming);
    }

    public void ShowCustom(
        string title,
        string description,
        float duration = 3f,
        bool useTypewriter = false)
    {
        if (view == null)
            return;

        view.Show(title, description, duration, useTypewriter);
    }

    public void ShowWorldEventFeedback(
        string title,
        string description,
        Color accentColor,
        float duration = 0.45f)
    {
        if (view == null)
            return;

        view.ShowWorldEventFeedback(
            title,
            description,
            accentColor,
            duration
        );
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        movementHint.SetActive(false);
        if (Instance == this)
            Instance = null;
    }

    private void Show(RunMessageData data, bool useTypewriter)
    {
        if (view == null || data == null)
            return;

        view.Show(
            data.title,
            data.description,
            data.duration,
            useTypewriter
        );

        if (data.sound != null)
        {
            Vector3 position = Camera.main != null
                ? Camera.main.transform.position
                : transform.position;

            AudioService.Instance?.PlayExternalOneShot(
                data.sound,
                position,
                data.volume,
                AudioCategory.UI
            );
        }
    }

    private RunMessageData FindMessage(RunMessageType type)
    {
        if (messages == null)
            return null;

        foreach (RunMessageData message in messages)
        {
            if (message != null && message.messageType == type)
                return message;
        }

        return null;
    }
}
