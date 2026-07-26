using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BunkerIntroTextStyle
{
    HumanRecording,
    System,
    Error
}

[Serializable]
public sealed class BunkerIntroStep
{
    [TextArea(1, 4)] public string mainText;
    [TextArea(1, 3)] public string secondaryText;
    [Min(0.1f)] public float duration = 1.5f;
    [Min(0f)] public float fadeIn = 0.25f;
    [Min(0f)] public float fadeOut = 0.2f;
    [Min(1f)] public float charactersPerSecond = 34f;
    public bool typewriter = true;
    public bool glitch;
    public BunkerIntroTextStyle style;
}

public sealed class BunkerIntroController : MonoBehaviour
{
    public const string ViewedPreferenceKey = "BunkerIntro.Viewed.v1";

    [Header("Scene References")]
    [SerializeField] private BunkerIntroView view;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Transform cameraRig;
    [SerializeField] private Transform introCameraPoint;
    [SerializeField] private Transform player;
    [SerializeField] private Transform awakeningProp;

    [Header("Gameplay Lock")]
    [SerializeField] private Behaviour[] gameplayBehavioursToDisable;
    [SerializeField] private GameObject[] gameplayUiToHide;

    [Header("Human Recording")]
    [SerializeField] private List<BunkerIntroStep> recordingSteps = new();
    [SerializeField, Min(0f)] private float initialBlackDuration = 0.65f;

    [Header("System Reaction")]
    [SerializeField] private List<BunkerIntroStep> systemSteps = new();
    [SerializeField] private BunkerIntroStep finalStep = new();

    [Header("Staging")]
    [SerializeField, Min(0.05f)] private float cameraMoveDuration = 1.65f;
    [SerializeField, Min(0.05f)] private float finalFadeDuration = 0.55f;
    [SerializeField] private Vector3 propLiftOffset =
        new(-0.32f, 0.55f, 0f);
    [SerializeField, Min(0.1f)] private float propAnimationDuration = 0.9f;

    [Header("Optional Intro Audio")]
    [SerializeField] private AudioSource recordingSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip humanRecordingClip;
    [SerializeField] private AudioClip radioNoiseClip;
    [SerializeField] private AudioClip glassCrackClip;
    [SerializeField] private AudioClip alarmPulseClip;
    [SerializeField] private AudioClip systemErrorClip;
    [SerializeField] private AudioClip metalDropClip;

    [Header("Skip")]
    [SerializeField, Min(0f)] private float skipUnlockDelay = 1f;
    [SerializeField, Min(0.1f)] private float skipHoldDuration = 0.65f;

    private readonly List<bool> behaviourStates = new();
    private readonly List<bool> uiStates = new();

    private Coroutine sequenceRoutine;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;
    private Vector3 propStartLocalPosition;
    private Quaternion propStartLocalRotation;
    private Vector3 propStartLocalScale;
    private bool propStartActive;
    private bool cameraFollowWasEnabled;
    private bool gameplayLocked;
    private bool isRunning;
    private bool isFinishing;
    private bool hasFinished;
    private bool skipRequested;
    private float introStartedAt;
    private float skipHeldTime;

    public bool IsPlaying => isRunning;

    private void Reset()
    {
        PopulateDefaultSteps();
    }

    private void Awake()
    {
        ConfigureAudioSources();
        CapturePropState();

        if (view != null)
            view.HideImmediate();
    }

    private void Start()
    {
        if (HasBeenViewed())
        {
            EnsureNormalState();
            return;
        }

        TryBeginIntro();
    }

    private void Update()
    {
        if (isRunning)
            UpdateSkipInput();
    }

    private void OnDisable()
    {
        if (isRunning || gameplayLocked)
            AbortWithoutSaving();
    }

    private void OnDestroy()
    {
        if (isRunning || gameplayLocked)
            AbortWithoutSaving();
    }

    private void TryBeginIntro()
    {
        if (isRunning || isFinishing)
            return;

        hasFinished = false;

        if (!ValidateCriticalReferences())
        {
            EnsureNormalState();
            return;
        }

        isRunning = true;
        skipRequested = false;
        skipHeldTime = 0f;
        introStartedAt = Time.unscaledTime;

        ConfigureAudioSources();
        CaptureAndLockGameplay();
        CaptureAndTakeCameraControl();
        CapturePropState();
        view.Prepare();
        sequenceRoutine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        yield return WaitUnscaled(initialBlackDuration);

        if (FinishIfSkipped())
            yield break;

        yield return PlayHumanRecordingStage();

        if (FinishIfSkipped())
            yield break;

        yield return PlayEmergencyAwakeningStage();

        if (FinishIfSkipped())
            yield break;

        yield return PlaySystemReactionStage();

        if (FinishIfSkipped())
            yield break;

        yield return PlayPropReactionStage();

        if (FinishIfSkipped())
            yield break;

        yield return PlayReturnControlStage();

        if (FinishIfSkipped())
            yield break;

        FinishIntro(true);
    }

    private IEnumerator PlayHumanRecordingStage()
    {
        StartLoop(ambienceSource, radioNoiseClip, 0.28f);
        PlayClip(recordingSource, humanRecordingClip, 0.9f);

        for (int i = 0; i < recordingSteps.Count && !skipRequested; i++)
            yield return PlayTextStep(recordingSteps[i], 1f);

        StopSource(recordingSource);
        StopSource(ambienceSource);

        if (!skipRequested)
            yield return WaitUnscaled(0.22f);
    }

    private IEnumerator PlayEmergencyAwakeningStage()
    {
        view.ClearText();
        PlayClip(sfxSource, glassCrackClip, 0.8f);
        StartLoop(ambienceSource, alarmPulseClip, 0.25f);

        yield return AnimateFlash(
            new Color(0.92f, 0.04f, 0.02f, 1f),
            0f,
            0.82f,
            0.09f);
        yield return AnimateFlash(
            new Color(0.92f, 0.04f, 0.02f, 1f),
            0.82f,
            0f,
            0.12f);
        yield return AnimateOverlay(1f, 0.9f, 0.22f);
        yield return AnimateOverlay(0.9f, 0.58f, 0.62f);
        yield return AnimateFlash(
            new Color(1f, 0.12f, 0.03f, 1f),
            0f,
            0.62f,
            0.08f);
        yield return AnimateFlash(
            new Color(1f, 0.12f, 0.03f, 1f),
            0.62f,
            0.08f,
            0.15f);
        yield return AnimateOverlay(0.58f, 0.16f, 0.75f);
        view.SetFlash(Color.red, 0f);
    }

    private IEnumerator PlaySystemReactionStage()
    {
        for (int i = 0; i < systemSteps.Count && !skipRequested; i++)
        {
            BunkerIntroStep step = systemSteps[i];

            if (step != null &&
                (step.style == BunkerIntroTextStyle.Error || step.glitch))
            {
                PlayClip(sfxSource, systemErrorClip, 0.65f);
            }

            yield return PlayTextStep(step, 0.12f);
        }
    }

    private IEnumerator PlayPropReactionStage()
    {
        view.ClearText();

        if (awakeningProp == null)
        {
            yield return WaitUnscaled(propAnimationDuration);
            yield break;
        }

        awakeningProp.gameObject.SetActive(true);
        Vector3 start = propStartLocalPosition;
        Vector3 lifted = start + propLiftOffset;
        float liftDuration = propAnimationDuration * 0.62f;
        float dropDuration = Mathf.Max(
            0.05f,
            propAnimationDuration - liftDuration);
        float elapsed = 0f;

        while (elapsed < liftDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / liftDuration);
            float shake = Mathf.Sin(elapsed * 52f) * 0.035f * t;
            awakeningProp.localPosition =
                Vector3.Lerp(start, lifted, t) +
                new Vector3(shake, -shake * 0.35f, 0f);
            awakeningProp.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Sin(elapsed * 38f) * 8f * t);
            yield return null;
        }

        if (skipRequested)
        {
            RestoreProp();
            yield break;
        }

        elapsed = 0f;
        PlayClip(sfxSource, metalDropClip, 0.62f);

        while (elapsed < dropDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            float dropT = t * t;
            awakeningProp.localPosition =
                Vector3.Lerp(lifted, start, dropT);
            awakeningProp.localRotation = Quaternion.Slerp(
                awakeningProp.localRotation,
                propStartLocalRotation,
                dropT);
            yield return null;
        }

        RestoreProp();
    }

    private IEnumerator PlayReturnControlStage()
    {
        StopSource(ambienceSource);
        yield return PlayTextStep(finalStep, 0.08f);

        if (skipRequested)
            yield break;

        yield return MoveCameraToGameplayTarget();

        if (skipRequested)
            yield break;

        yield return FadeOutView();
    }

    private IEnumerator PlayTextStep(
        BunkerIntroStep step,
        float overlayAlpha)
    {
        if (step == null)
            yield break;

        string main = step.mainText ?? string.Empty;
        string secondary = step.secondaryText ?? string.Empty;
        int mainCharacters = main.Length;
        int secondaryCharacters = secondary.Length;
        float duration = Mathf.Max(0.1f, step.duration);
        float fadeIn = Mathf.Min(Mathf.Max(0f, step.fadeIn), duration);
        float fadeOut = Mathf.Min(
            Mathf.Max(0f, step.fadeOut),
            Mathf.Max(0f, duration - fadeIn));
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = fadeIn <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / fadeIn);

            if (fadeOut > 0f && elapsed > duration - fadeOut)
                alpha *= Mathf.Clamp01((duration - elapsed) / fadeOut);

            int visibleMain = mainCharacters;
            int visibleSecondary = secondaryCharacters;

            if (step.typewriter)
            {
                int visible = Mathf.FloorToInt(
                    elapsed * Mathf.Max(1f, step.charactersPerSecond));
                visibleMain = Mathf.Min(mainCharacters, visible);
                visibleSecondary = Mathf.Clamp(
                    visible - mainCharacters,
                    0,
                    secondaryCharacters);
            }

            float glitchOffset = 0f;

            if (step.glitch && elapsed < Mathf.Min(0.32f, duration))
            {
                int flickerFrame = Mathf.FloorToInt(elapsed * 38f);

                if ((flickerFrame & 1) == 0)
                {
                    alpha *= 0.3f;
                    glitchOffset = (flickerFrame % 3 - 1) * 6f;
                }
            }

            view.SetTextOffset(glitchOffset);
            view.SetText(
                main,
                secondary,
                step.style,
                visibleMain,
                visibleSecondary,
                alpha);
            view.SetOverlayAlpha(overlayAlpha);
            yield return null;
        }

        view.SetTextOffset(0f);
    }

    private IEnumerator AnimateOverlay(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / Mathf.Max(0.01f, duration));
            view.SetOverlayAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        if (!skipRequested)
            view.SetOverlayAlpha(to);
    }

    private IEnumerator AnimateFlash(
        Color color,
        float from,
        float to,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            view.SetFlash(color, Mathf.Lerp(from, to, t));
            yield return null;
        }

        if (!skipRequested)
            view.SetFlash(color, to);
    }

    private IEnumerator MoveCameraToGameplayTarget()
    {
        Vector3 startPosition = cameraRig.position;
        Quaternion startRotation = cameraRig.rotation;
        float elapsed = 0f;

        while (elapsed < cameraMoveDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / cameraMoveDuration);
            cameraRig.position = Vector3.Lerp(
                startPosition,
                GetGameplayCameraPosition(),
                t);
            cameraRig.rotation = Quaternion.Slerp(
                startRotation,
                cameraStartRotation,
                t);
            yield return null;
        }

        if (!skipRequested)
        {
            cameraRig.position = GetGameplayCameraPosition();
            cameraRig.rotation = cameraStartRotation;
        }
    }

    private IEnumerator FadeOutView()
    {
        float elapsed = 0f;

        while (elapsed < finalFadeDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            view.SetRootAlpha(
                1f - Mathf.Clamp01(elapsed / finalFadeDuration));
            yield return null;
        }
    }

    private void UpdateSkipInput()
    {
        if (Time.unscaledTime < introStartedAt + skipUnlockDelay)
        {
            skipHeldTime = 0f;
            view.SetSkipHint(false, 0f);
            return;
        }

        bool held =
            Input.GetKey(KeyCode.Escape) ||
            Input.GetKey(KeyCode.Space) ||
            Input.GetKey(KeyCode.Return) ||
            Input.GetKey(KeyCode.KeypadEnter) ||
            Input.GetMouseButton(0);

        if (!held)
        {
            skipHeldTime = 0f;
            view.SetSkipHint(true, 0f);
            return;
        }

        skipHeldTime += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(
            skipHeldTime / Mathf.Max(0.1f, skipHoldDuration));
        view.SetSkipHint(true, progress);

        if (progress >= 1f)
            skipRequested = true;
    }

    private bool FinishIfSkipped()
    {
        if (!skipRequested)
            return false;

        FinishIntro(true);
        return true;
    }

    private void FinishIntro(bool markViewed)
    {
        if (isFinishing || hasFinished)
            return;

        isFinishing = true;
        sequenceRoutine = null;
        isRunning = false;
        skipRequested = false;

        StopIntroAudio();
        RestoreProp();
        RestoreCameraImmediately();
        RestoreGameplay();
        view?.HideImmediate();

        if (markViewed)
        {
            PlayerPrefs.SetInt(ViewedPreferenceKey, 1);
            PlayerPrefs.Save();
        }

        hasFinished = true;
        isFinishing = false;
    }

    private void AbortWithoutSaving()
    {
        if (isFinishing)
            return;

        isFinishing = true;

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        isRunning = false;
        skipRequested = false;
        StopIntroAudio();
        RestoreProp();
        RestoreCameraImmediately();
        RestoreGameplay();
        view?.HideImmediate();
        isFinishing = false;
    }

    private void CaptureAndLockGameplay()
    {
        behaviourStates.Clear();
        uiStates.Clear();

        if (gameplayBehavioursToDisable != null)
        {
            foreach (Behaviour behaviour in gameplayBehavioursToDisable)
            {
                bool enabled = behaviour != null && behaviour.enabled;
                behaviourStates.Add(enabled);

                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }

        if (gameplayUiToHide != null)
        {
            foreach (GameObject uiRoot in gameplayUiToHide)
            {
                bool active = uiRoot != null && uiRoot.activeSelf;
                uiStates.Add(active);

                if (uiRoot != null)
                    uiRoot.SetActive(false);
            }
        }

        gameplayLocked = true;
    }

    private void CaptureAndTakeCameraControl()
    {
        cameraStartPosition = cameraRig.position;
        cameraStartRotation = cameraRig.rotation;
        cameraFollowWasEnabled = cameraFollow.enabled;
        cameraFollow.enabled = false;
        cameraRig.SetPositionAndRotation(
            introCameraPoint.position,
            introCameraPoint.rotation);
    }

    private void CapturePropState()
    {
        if (awakeningProp == null)
            return;

        propStartLocalPosition = awakeningProp.localPosition;
        propStartLocalRotation = awakeningProp.localRotation;
        propStartLocalScale = awakeningProp.localScale;
        propStartActive = awakeningProp.gameObject.activeSelf;
    }

    private void RestoreProp()
    {
        if (awakeningProp == null)
            return;

        awakeningProp.localPosition = propStartLocalPosition;
        awakeningProp.localRotation = propStartLocalRotation;
        awakeningProp.localScale = propStartLocalScale;
        awakeningProp.gameObject.SetActive(propStartActive);
    }

    private void RestoreCameraImmediately()
    {
        if (cameraRig == null)
            return;

        cameraRig.position = GetGameplayCameraPosition();
        cameraRig.rotation = cameraStartRotation;

        if (cameraFollow != null)
            cameraFollow.enabled = cameraFollowWasEnabled;
    }

    private void RestoreGameplay()
    {
        if (gameplayLocked)
        {
            if (gameplayBehavioursToDisable != null)
            {
                for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
                {
                    Behaviour behaviour = gameplayBehavioursToDisable[i];

                    if (behaviour != null && i < behaviourStates.Count)
                        behaviour.enabled = behaviourStates[i];
                }
            }

            if (gameplayUiToHide != null)
            {
                for (int i = 0; i < gameplayUiToHide.Length; i++)
                {
                    GameObject uiRoot = gameplayUiToHide[i];

                    if (uiRoot != null && i < uiStates.Count)
                        uiRoot.SetActive(uiStates[i]);
                }
            }
        }

        gameplayLocked = false;
        behaviourStates.Clear();
        uiStates.Clear();
    }

    private void EnsureNormalState()
    {
        isRunning = false;
        gameplayLocked = false;
        skipRequested = false;
        hasFinished = true;
        StopIntroAudio();
        RestoreProp();
        view?.HideImmediate();
    }

    private Vector3 GetGameplayCameraPosition()
    {
        Transform target = cameraFollow != null && cameraFollow.target != null
            ? cameraFollow.target
            : player;

        if (target != null && cameraFollow != null)
            return target.position + cameraFollow.offset;

        if (target != null)
        {
            return new Vector3(
                target.position.x,
                target.position.y,
                cameraStartPosition.z);
        }

        return cameraStartPosition;
    }

    private bool ValidateCriticalReferences()
    {
        bool valid = true;

        if (view == null || !view.IsConfigured)
        {
            Debug.LogError(
                "[BunkerIntro] Intro UI is not fully configured. " +
                "The intro was cancelled safely.",
                this);
            valid = false;
        }

        if (cameraRig == null ||
            cameraFollow == null ||
            introCameraPoint == null)
        {
            Debug.LogError(
                "[BunkerIntro] Camera references are missing. " +
                "The intro was cancelled safely.",
                this);
            valid = false;
        }

        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            player = foundPlayer != null ? foundPlayer.transform : null;
        }

        if (player == null)
        {
            Debug.LogError(
                "[BunkerIntro] Player was not found. " +
                "The intro was cancelled safely.",
                this);
            valid = false;
        }

        if (recordingSteps == null ||
            recordingSteps.Count == 0 ||
            systemSteps == null ||
            systemSteps.Count == 0 ||
            finalStep == null)
        {
            Debug.LogError(
                "[BunkerIntro] Narrative steps are not configured. " +
                "The intro was cancelled safely.",
                this);
            valid = false;
        }

        return valid;
    }

    private void ConfigureAudioSources()
    {
        ConfigureAudioSource(recordingSource, AudioCategory.SFX);
        ConfigureAudioSource(ambienceSource, AudioCategory.Ambience);
        ConfigureAudioSource(sfxSource, AudioCategory.SFX);
    }

    private static void ConfigureAudioSource(
        AudioSource source,
        AudioCategory category)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.ignoreListenerPause = true;
        source.spatialBlend = 0f;
        AudioService.Instance?.RouteExternalSource(source, category);
    }

    private static void PlayClip(
        AudioSource source,
        AudioClip clip,
        float volume)
    {
        if (source == null || clip == null)
            return;

        source.Stop();
        source.loop = false;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
    }

    private static void StartLoop(
        AudioSource source,
        AudioClip clip,
        float volume)
    {
        if (source == null || clip == null)
            return;

        source.Stop();
        source.loop = true;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
    }

    private void StopIntroAudio()
    {
        StopSource(recordingSource);
        StopSource(ambienceSource);
        StopSource(sfxSource);
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.loop = false;
    }

    private static bool HasBeenViewed()
    {
        return PlayerPrefs.GetInt(ViewedPreferenceKey, 0) == 1;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    [ContextMenu("Reset Intro Viewed Flag")]
    public void ResetViewedFlagForTesting()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlayerPrefs.DeleteKey(ViewedPreferenceKey);
        PlayerPrefs.Save();
        Debug.Log($"[BunkerIntro] Reset flag: {ViewedPreferenceKey}", this);
#else
        Debug.LogWarning(
            "[BunkerIntro] Reset is available only in Editor or Development Build.",
            this);
#endif
    }

    [ContextMenu("Play Intro From Start")]
    public void PlayIntroFromStartForTesting()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Application.isPlaying)
        {
            Debug.Log(
                "[BunkerIntro] Enter Play Mode, then use Play Intro From Start.",
                this);
            return;
        }

        if (isRunning || gameplayLocked)
            AbortWithoutSaving();

        hasFinished = false;
        TryBeginIntro();
#else
        Debug.LogWarning(
            "[BunkerIntro] Replay is available only in Editor or Development Build.",
            this);
#endif
    }

    [ContextMenu("Restore Default Intro Steps")]
    public void PopulateDefaultSteps()
    {
        recordingSteps = new List<BunkerIntroStep>
        {
            Step(
                "...слышишь меня?",
                "ПОВРЕЖДЁННАЯ ЗАПИСЬ",
                1.35f,
                BunkerIntroTextStyle.HumanRecording,
                false,
                26f),
            Step(
                "Если ты проснулся...",
                "ПОВРЕЖДЁННАЯ ЗАПИСЬ",
                2.55f,
                BunkerIntroTextStyle.HumanRecording,
                false,
                24f),
            Step(
                "...значит мы проиграли.",
                "ПОВРЕЖДЁННАЯ ЗАПИСЬ",
                3.05f,
                BunkerIntroTextStyle.HumanRecording,
                false,
                23f),
            Step(
                "Прости нас.",
                "ПОВРЕЖДЁННАЯ ЗАПИСЬ",
                1.65f,
                BunkerIntroTextStyle.HumanRecording,
                false,
                22f)
        };

        systemSteps = new List<BunkerIntroStep>
        {
            Step(
                "БИОЛОГИЧЕСКАЯ АКТИВНОСТЬ ОБНАРУЖЕНА",
                string.Empty,
                1.65f,
                BunkerIntroTextStyle.System,
                false,
                38f),
            Step(
                "НЕВОЗМОЖНО",
                string.Empty,
                1.05f,
                BunkerIntroTextStyle.Error,
                true,
                30f),
            Step(
                "ИДЕНТИФИКАЦИЯ...\n\nАРХИВ ПОВРЕЖДЁН\n\nОБЪЕКТ НЕ ОПОЗНАН",
                string.Empty,
                2.45f,
                BunkerIntroTextStyle.System,
                true,
                39f)
        };

        finalStep = Step(
            "НАЙДИТЕ ВЫХОД",
            string.Empty,
            1.15f,
            BunkerIntroTextStyle.System,
            false,
            32f);
    }

    private static BunkerIntroStep Step(
        string main,
        string secondary,
        float duration,
        BunkerIntroTextStyle style,
        bool glitch,
        float charactersPerSecond)
    {
        return new BunkerIntroStep
        {
            mainText = main,
            secondaryText = secondary,
            duration = duration,
            fadeIn = style == BunkerIntroTextStyle.HumanRecording
                ? 0.35f
                : 0.2f,
            fadeOut = style == BunkerIntroTextStyle.HumanRecording
                ? 0.28f
                : 0.18f,
            charactersPerSecond = charactersPerSecond,
            typewriter = true,
            glitch = glitch,
            style = style
        };
    }
}
