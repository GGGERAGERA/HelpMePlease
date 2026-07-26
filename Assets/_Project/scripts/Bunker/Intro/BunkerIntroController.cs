using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BunkerIntroStep
{
    [TextArea(1, 4)] public string mainText;
    [TextArea(1, 5)] public string systemText;
    [Min(0.1f)] public float duration = 2f;
    [Min(0f)] public float fadeIn = 0.25f;
    [Min(0f)] public float fadeOut = 0.2f;
    [Min(1f)] public float charactersPerSecond = 34f;
    [Range(0f, 1f)] public float overlayAlpha = 0.62f;
    public bool typewriter = true;
    public bool errorStyle;
    public bool glitch;
    public AudioCueId audioCue = AudioCueId.None;
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

    [Header("Gameplay Lock")]
    [SerializeField] private Behaviour[] gameplayBehavioursToDisable;
    [SerializeField] private GameObject[] gameplayUiToHide;

    [Header("Sequence")]
    [SerializeField] private List<BunkerIntroStep> steps = new();
    [SerializeField, Min(0f)] private float initialBlackDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float cameraMoveDuration = 2.25f;
    [SerializeField, Min(0.05f)] private float finalFadeDuration = 0.65f;

    [Header("Skip")]
    [SerializeField, Min(0f)] private float skipUnlockDelay = 1f;
    [SerializeField, Min(0.1f)] private float skipHoldDuration = 0.65f;

    private readonly List<bool> behaviourStates = new();
    private readonly List<bool> uiStates = new();

    private Coroutine sequenceRoutine;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;
    private bool cameraFollowWasEnabled;
    private bool gameplayLocked;
    private bool isRunning;
    private bool skipRequested;
    private float introStartedAt;
    private float skipHeldTime;
    private float currentOverlayAlpha = 1f;

    public bool IsPlaying => isRunning;

    private void Reset()
    {
        PopulateDefaultSteps();
    }

    private void Awake()
    {
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

        if (!ValidateCriticalReferences())
        {
            EnsureNormalState();
            return;
        }

        BeginIntro();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        UpdateSkipInput();
    }

    private void OnDisable()
    {
        if (!isRunning && !gameplayLocked)
            return;

        StopSequence();
        RestoreGameplay();

        if (view != null)
            view.HideImmediate();
    }

    private void OnDestroy()
    {
        if (isRunning || gameplayLocked)
        {
            StopSequence();
            RestoreGameplay();
        }
    }

    private void BeginIntro()
    {
        isRunning = true;
        skipRequested = false;
        skipHeldTime = 0f;
        introStartedAt = Time.unscaledTime;
        currentOverlayAlpha = 1f;

        CaptureAndLockGameplay();
        CaptureAndTakeCameraControl();
        view.Prepare();
        sequenceRoutine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        yield return WaitUnscaled(initialBlackDuration);

        for (int i = 0; i < steps.Count && !skipRequested; i++)
            yield return PlayStep(steps[i]);

        if (skipRequested)
        {
            CompleteIntro(true);
            yield break;
        }

        yield return MoveCameraToGameplayTarget();
        yield return FadeOutView();
        CompleteIntro(true);
    }

    private IEnumerator PlayStep(BunkerIntroStep step)
    {
        if (step == null)
            yield break;

        AudioService.Instance?.Play(step.audioCue);

        string main = step.mainText ?? string.Empty;
        string system = step.systemText ?? string.Empty;
        int mainCharacters = CountVisibleCharacters(main);
        int systemCharacters = CountVisibleCharacters(system);
        float duration = Mathf.Max(0.1f, step.duration);
        float fadeIn = Mathf.Min(Mathf.Max(0f, step.fadeIn), duration);
        float fadeOut = Mathf.Min(
            Mathf.Max(0f, step.fadeOut),
            Mathf.Max(0f, duration - fadeIn));
        float overlayStart = currentOverlayAlpha;
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;

            float alpha = fadeIn <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / fadeIn);

            if (fadeOut > 0f && elapsed > duration - fadeOut)
                alpha *= Mathf.Clamp01((duration - elapsed) / fadeOut);

            float overlayProgress = fadeIn <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / fadeIn);
            float overlayAlpha = Mathf.Lerp(
                overlayStart,
                step.overlayAlpha,
                Smooth01(overlayProgress));

            int visibleMain = mainCharacters;
            int visibleSystem = systemCharacters;

            if (step.typewriter)
            {
                int visible = Mathf.FloorToInt(
                    elapsed * Mathf.Max(1f, step.charactersPerSecond));
                visibleMain = Mathf.Min(mainCharacters, visible);
                visibleSystem = Mathf.Clamp(
                    visible - mainCharacters,
                    0,
                    systemCharacters);
            }

            float glitchOffset = 0f;

            if (step.glitch && elapsed < Mathf.Min(0.45f, duration))
            {
                int flickerFrame = Mathf.FloorToInt(elapsed * 34f);

                if ((flickerFrame & 1) == 0)
                {
                    alpha *= 0.32f;
                    glitchOffset = (flickerFrame % 3 - 1) * 7f;
                }
            }

            view.SetTextOffset(glitchOffset);
            view.SetStep(
                main,
                system,
                step.errorStyle,
                visibleMain,
                visibleSystem,
                alpha,
                overlayAlpha);

            yield return null;
        }

        currentOverlayAlpha = step.overlayAlpha;
        view.SetTextOffset(0f);
    }

    private IEnumerator MoveCameraToGameplayTarget()
    {
        if (cameraRig == null)
            yield break;

        Vector3 startPosition = cameraRig.position;
        Quaternion startRotation = cameraRig.rotation;
        float elapsed = 0f;

        while (elapsed < cameraMoveDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / cameraMoveDuration);
            Vector3 targetPosition = GetGameplayCameraPosition();
            cameraRig.position = Vector3.Lerp(startPosition, targetPosition, t);
            cameraRig.rotation = Quaternion.Slerp(
                startRotation,
                cameraStartRotation,
                t);
            yield return null;
        }

        cameraRig.position = GetGameplayCameraPosition();
        cameraRig.rotation = cameraStartRotation;
    }

    private IEnumerator FadeOutView()
    {
        float elapsed = 0f;

        while (elapsed < finalFadeDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            view.SetRootAlpha(1f - Mathf.Clamp01(elapsed / finalFadeDuration));
            yield return null;
        }
    }

    private void UpdateSkipInput()
    {
        bool unlocked =
            Time.unscaledTime >= introStartedAt + skipUnlockDelay;

        if (!unlocked)
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

        if (held)
        {
            skipHeldTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                skipHeldTime / Mathf.Max(0.1f, skipHoldDuration));
            view.SetSkipHint(true, progress);

            if (progress >= 1f)
                skipRequested = true;
        }
        else
        {
            skipHeldTime = 0f;
            view.SetSkipHint(true, 0f);
        }
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
        cameraFollowWasEnabled = cameraFollow != null && cameraFollow.enabled;

        if (cameraFollow != null)
            cameraFollow.enabled = false;

        cameraRig.SetPositionAndRotation(
            introCameraPoint.position,
            introCameraPoint.rotation);
    }

    private void CompleteIntro(bool markViewed)
    {
        sequenceRoutine = null;
        isRunning = false;
        skipRequested = false;

        if (cameraRig != null)
        {
            cameraRig.position = GetGameplayCameraPosition();
            cameraRig.rotation = cameraStartRotation;
        }

        RestoreGameplay();

        if (view != null)
            view.HideImmediate();

        if (markViewed)
        {
            PlayerPrefs.SetInt(ViewedPreferenceKey, 1);
            PlayerPrefs.Save();
        }
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

        if (cameraFollow != null)
            cameraFollow.enabled = cameraFollowWasEnabled;

        gameplayLocked = false;
        isRunning = false;
        skipRequested = false;
        skipHeldTime = 0f;
        behaviourStates.Clear();
        uiStates.Clear();
    }

    private void EnsureNormalState()
    {
        isRunning = false;
        gameplayLocked = false;
        skipRequested = false;

        if (view != null)
            view.HideImmediate();
    }

    private void StopSequence()
    {
        isRunning = false;
        skipRequested = false;

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }
    }

    private Vector3 GetGameplayCameraPosition()
    {
        Transform target = cameraFollow != null && cameraFollow.target != null
            ? cameraFollow.target
            : player;

        if (target != null && cameraFollow != null)
            return target.position + cameraFollow.offset;

        if (target != null)
            return new Vector3(
                target.position.x,
                target.position.y,
                cameraStartPosition.z);

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

        if (cameraRig == null || introCameraPoint == null)
        {
            Debug.LogError(
                "[BunkerIntro] Camera rig or IntroCameraPoint is missing. " +
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

        if (steps == null || steps.Count == 0)
        {
            Debug.LogError(
                "[BunkerIntro] The sequence has no configured steps. " +
                "The intro was cancelled safely.",
                this);
            valid = false;
        }

        return valid;
    }

    private static bool HasBeenViewed()
    {
        return PlayerPrefs.GetInt(ViewedPreferenceKey, 0) == 1;
    }

    private static int CountVisibleCharacters(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : text.Length;
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

    [ContextMenu("Restore Default Intro Steps")]
    public void PopulateDefaultSteps()
    {
        steps = new List<BunkerIntroStep>
        {
            Step(
                "SUBJECT#42",
                "АВАРИЙНЫЙ ПРОТОКОЛ ВОССТАНОВЛЕНИЯ",
                2.5f,
                0.9f,
                AudioCueId.UIConfirm),
            Step(
                string.Empty,
                "ВНЕШНЯЯ СЕТЬ: НЕДОСТУПНА\n\n" +
                "ПЕРСОНАЛ: НЕ ОБНАРУЖЕН\n\n" +
                "ЦЕЛОСТНОСТЬ КОМПЛЕКСА: 31%",
                3.1f,
                0.76f),
            Step(
                string.Empty,
                "КРИОГЕННЫЙ БЛОК: НАРУШЕНИЕ ГЕРМЕТИЧНОСТИ\n\n" +
                "КАПСУЛА 42: ВСКРЫТА",
                2.8f,
                0.62f),
            Step(
                "БИОЛОГИЧЕСКАЯ АКТИВНОСТЬ ПОДТВЕРЖДЕНА",
                "ИДЕНТИФИКАЦИЯ ОБЪЕКТА...",
                2.6f,
                0.5f),
            Step(
                "ОШИБКА ИДЕНТИФИКАЦИИ",
                string.Empty,
                1.5f,
                0.48f,
                AudioCueId.UIHover,
                true,
                true),
            Step(
                string.Empty,
                "ИСТОЧНИК ЭКСПЕРИМЕНТАЛЬНОГО ПРОТОКОЛА:\n\n" +
                "ЧЕЛОВЕЧЕСКАЯ АДМИНИСТРАЦИЯ",
                1.8f,
                0.46f),
            Step(
                string.Empty,
                "ИСТОЧНИК ЭКСПЕРИМЕНТАЛЬНОГО ПРОТОКОЛА:\n\n" +
                "АВТОНОМНАЯ СИСТЕМА",
                1.45f,
                0.46f,
                AudioCueId.UIHover,
                false,
                true),
            Step(
                string.Empty,
                "ИСТОЧНИК ЭКСПЕРИМЕНТАЛЬНОГО ПРОТОКОЛА:\n\n" +
                "НЕ УСТАНОВЛЕНО",
                1.85f,
                0.44f,
                AudioCueId.UIHover,
                true,
                true),
            Step(
                string.Empty,
                "ДЛЯ ПОДДЕРЖАНИЯ ЖИЗНЕДЕЯТЕЛЬНОСТИ КОМПЛЕКСА\n" +
                "НЕОБХОДИМЫ РЕСУРСЫ ПОВЕРХНОСТИ",
                3f,
                0.36f),
            Step(
                "НАЙДИТЕ ВЫХОД",
                string.Empty,
                2.4f,
                0.22f,
                AudioCueId.UIConfirm)
        };
    }

    private static BunkerIntroStep Step(
        string main,
        string system,
        float duration,
        float overlayAlpha,
        AudioCueId cue = AudioCueId.None,
        bool error = false,
        bool glitch = false)
    {
        return new BunkerIntroStep
        {
            mainText = main,
            systemText = system,
            duration = duration,
            fadeIn = 0.24f,
            fadeOut = 0.2f,
            charactersPerSecond = 38f,
            overlayAlpha = overlayAlpha,
            typewriter = true,
            errorStyle = error,
            glitch = glitch,
            audioCue = cue
        };
    }
}
