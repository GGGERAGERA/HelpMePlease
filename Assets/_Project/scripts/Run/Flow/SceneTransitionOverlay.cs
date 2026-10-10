using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>One persistent presentation owner for all single-scene transitions.</summary>
[DefaultExecutionOrder(-32000)]
public sealed class SceneTransitionOverlay : MonoBehaviour
{
    public static SceneTransitionOverlay Instance { get; private set; }
    public static bool IsTransitioning => Instance != null && Instance.busy;
    [SerializeField, Min(0f)] private float closeDuration = .3f;
    [SerializeField, Min(0f)] private float revealDuration = .35f;
    [SerializeField, Min(0f)] private float minimumHold = .08f;
    [SerializeField, Min(1f)] private float readyTimeout = 20f;
    [Tooltip("Development: simulate slow loading. Zero in production.")]
    [SerializeField, Min(0f)] private float additionalHold;

    [SerializeField] private SceneTransitionView view;
    private bool busy;
    private float previousTimeScale;
    private bool returning;
    private bool faulted;
    private bool presentingBlack;
    private string requestedScene;

    public static bool CanLoad(string scene) => !IsTransitioning &&
        !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);

    // Preparation runs exactly once, after the lock is acquired and before unloading.
    public static bool Load(string scene, Action prepare = null, Action<float> closing = null,
        float? closingDuration = null, Func<IEnumerator> afterBlack = null, bool fadeMaster = true)
    {
        if (IsTransitioning) return false;
        if (!CanLoad(scene))
        {
            Debug.LogError($"[SceneTransition] Scene '{scene}' is not in the build.");
            return false;
        }
        if (Instance == null)
        {
            Debug.LogError("[SceneTransition] ProductionSceneComposition must assign the authored transition overlay.");
            return false;
        }
        Instance.StartCoroutine(Instance.GuardedTransition(scene, prepare, closing, closingDuration, afterBlack, fadeMaster));
        return true;
    }

    private void Awake() => InitializeAuthored();

    public void InitializeAuthored()
    {
        if (Instance == this) return;
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        view.SetVisible(false);
    }

    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void RefreshLanguage(GameLanguage language) => view.SetFailureText(LocalizationService.EnsureExists().Get("transition.failure"));

    private void Update()
    {
        if (!busy) return;
        Time.timeScale = 0f;
        if (faulted)
        {
            if (Input.GetKeyDown(KeyCode.Return)) Recover(false);
            else if (Input.GetKeyDown(KeyCode.Escape)) Recover(true);
            return;
        }
        // No new EventSystem: also suppress keyboard/controller submit on selected UI.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (!presentingBlack) view.AnimateScan(returning);
    }

    // Unity does not propagate exceptions in nested coroutine iterators to their
    // parent's finally. Drive the small stack here so callback failures cannot strand input.
    private IEnumerator GuardedTransition(string scene, Action prepare, Action<float> closing,
        float? closingDuration, Func<IEnumerator> afterBlack, bool fadeMaster)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Transition(scene, prepare, closing, closingDuration, afterBlack, fadeMaster));
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            bool more = false;
            Exception failure = null;
            try { more = current.MoveNext(); }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                Debug.LogException(failure);
                ShowFailure();
                yield break;
            }
            if (!more) { stack.Pop(); continue; }
            if (current.Current is IEnumerator child) stack.Push(child);
            else yield return current.Current;
        }
    }

    private IEnumerator Transition(string scene, Action prepare, Action<float> closing,
        float? closingDuration, Func<IEnumerator> afterBlack, bool fadeMaster)
    {
        busy = true;
        requestedScene = scene;
        faulted = false;
        view.SetFailure(false);
        ProductionSceneComposition.Active?.PrepareForTransition();
        previousTimeScale = Time.timeScale;
        bool loaded = false;
        returning = scene == RunEndService.BunkerSceneName;
        view.SetVisible(true);
        presentingBlack = afterBlack != null;
        view.SetCinematic(presentingBlack);
        Time.timeScale = 0f;
        try
        {
            prepare?.Invoke();
            yield return Fade(view.Alpha, 1f, closingDuration ?? closeDuration, closing, fadeMaster);
            // Submit an opaque frame before activation, including the duration=0 path.
            yield return null;
            if (afterBlack != null) yield return afterBlack();
            presentingBlack = false;
            view.SetCinematic(false);
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single); }
            catch (Exception error) { Debug.LogException(error); }
            if (operation == null) { ShowFailure(); yield break; }
            while (!operation.isDone) yield return null;
            loaded = true;

            // sceneLoaded precedes Start. Allow Start, spawned components and LateUpdate.
            yield return null;
            float deadline = Time.realtimeSinceStartup + readyTimeout;
            while (!Ready(scene) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!Ready(scene))
            {
                Debug.LogError($"[SceneTransition] Ready timeout in '{scene}': scene composition did not report readiness.");
                ShowFailure();
                yield break;
            }
            yield return null;
            float holdUntil = Time.realtimeSinceStartup + minimumHold + additionalHold;
            while (Time.realtimeSinceStartup < holdUntil) yield return null;
            yield return Fade(1f, 0f, revealDuration, null, fadeMaster);
        }
        finally
        {
            if (!faulted) Release(loaded ? 1f : previousTimeScale);
        }
    }

    private static bool Ready(string scene)
    {
        var composition = ProductionSceneComposition.Active;
        return composition != null && composition.gameObject.scene == SceneManager.GetActiveScene() &&
            composition.gameObject.scene.name == scene && composition.IsReady;
    }

    private void ShowFailure()
    {
        busy = faulted = true;
        Time.timeScale = 0f;
        view.SetVisible(true);
        view.SetAlpha(1f);
        AudioSettingsService.Instance?.SetTransitionGain(0f);
        RefreshLanguage(LocalizationService.EnsureExists().CurrentLanguage);
        view.SetFailure(true);
    }

    private void Recover(bool bunker)
    {
        if (bunker) { RecoverToBunker(); return; }
        string scene = requestedScene;
        if (!Application.CanStreamedLevelBeLoaded(scene)) return;
        busy = false;
        Load(scene);
    }

    // Invalid gameplay activation may request recovery while this overlay is loading it.
    // Release that transition before using the existing run-end recovery flow.
    public void RecoverToBunker()
    {
        if (!Application.CanStreamedLevelBeLoaded(RunEndService.BunkerSceneName)) return;
        StopAllCoroutines();
        Release(previousTimeScale);
        RunEndService.RecoverToBunker();
    }

    private IEnumerator Fade(float from, float to, float duration, Action<float> closing, bool fadeMaster)
    {
        float elapsed = 0f;
        do
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            view.SetAlpha(Mathf.Lerp(from, to, eased));
            if (fadeMaster) AudioSettingsService.Instance?.SetTransitionGain(1f - view.Alpha);
            closing?.Invoke(eased);
            if (t >= 1f) break;
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        } while (true);
    }

    private void Release(float timeScale)
    {
        if (!busy) return;
        AudioSettingsService.Instance?.SetTransitionGain(1f);
        view.SetVisible(false);
        busy = false;
        faulted = false;
        presentingBlack = false;
        view.SetCinematic(false);
        Time.timeScale = timeScale;
    }

    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
        StopAllCoroutines();
        Release(previousTimeScale);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

}
