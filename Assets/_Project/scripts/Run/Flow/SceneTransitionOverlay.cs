using System;
using System.Collections;
using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    private bool busy;
    private Canvas canvas;
    private CanvasGroup group;
    private Image scan;
    private RectTransform scanRect;
    private float previousTimeScale;
    private bool returning;
    private bool faulted;
    private string requestedScene;
    private TextMeshProUGUI errorText;

    public static bool CanLoad(string scene) => !IsTransitioning &&
        !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);

    // Preparation runs exactly once, after the lock is acquired and before unloading.
    public static bool Load(string scene, Action prepare = null, Action<float> closing = null)
    {
        if (IsTransitioning) return false;
        if (!CanLoad(scene))
        {
            Debug.LogError($"[SceneTransition] Scene '{scene}' is not in the build.");
            return false;
        }
        if (Instance == null)
        {
            var prefab = Resources.Load<GameObject>("SceneTransitionOverlay");
            if (prefab != null) Instantiate(prefab);
            else new GameObject("SceneTransitionOverlay").AddComponent<SceneTransitionOverlay>();
        }
        Instance.StartCoroutine(Instance.GuardedTransition(scene, prepare, closing));
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildView();
    }

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
        float phase = Mathf.Repeat(Time.unscaledTime * .24f, 1f);
        float x = returning ? 1f - phase : phase;
        scanRect.anchorMin = new Vector2(x, 0f);
        scanRect.anchorMax = new Vector2(x, 1f);
        scan.color = new Color(.12f, .72f, .9f, .06f + .04f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    // Unity does not propagate exceptions in nested coroutine iterators to their
    // parent's finally. Drive the small stack here so callback failures cannot strand input.
    private IEnumerator GuardedTransition(string scene, Action prepare, Action<float> closing)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Transition(scene, prepare, closing));
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

    private IEnumerator Transition(string scene, Action prepare, Action<float> closing)
    {
        busy = true;
        requestedScene = scene;
        faulted = false;
        if (errorText != null) errorText.gameObject.SetActive(false);
        previousTimeScale = Time.timeScale;
        bool loaded = false;
        returning = scene == "MainMenu";
        canvas.enabled = true;
        group.blocksRaycasts = true;
        UpgradeManager.Instance?.CancelPendingRewards();
        Time.timeScale = 0f;
        try
        {
            prepare?.Invoke();
            yield return Fade(group.alpha, 1f, closeDuration, closing);
            // Submit an opaque frame before activation, including the duration=0 path.
            yield return null;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single); }
            catch (Exception error) { Debug.LogException(error); }
            if (operation == null) { ShowFailure(); yield break; }
            while (!operation.isDone) yield return null;
            loaded = true;

            // sceneLoaded precedes Start. Allow Start, spawned components and LateUpdate.
            yield return null;
            float deadline = Time.realtimeSinceStartup + readyTimeout;
            CharacterSpawner spawner = FindFirstObjectByType<CharacterSpawner>();
            CameraFollow camera = FindFirstObjectByType<CameraFollow>();
            BunkerPlayerLoadoutController bunkerLoadout = FindFirstObjectByType<BunkerPlayerLoadoutController>();
            while (!Ready(scene, spawner, camera, bunkerLoadout) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!Ready(scene, spawner, camera, bunkerLoadout))
            {
                Debug.LogError($"[SceneTransition] Ready timeout in '{scene}': check player, ORBITAL, HUD and camera initialization.");
                ShowFailure();
                yield break;
            }
            yield return null;
            float holdUntil = Time.realtimeSinceStartup + minimumHold + additionalHold;
            while (Time.realtimeSinceStartup < holdUntil) yield return null;
            yield return Fade(1f, 0f, revealDuration, null);
        }
        finally
        {
            if (!faulted) Release(loaded ? 1f : previousTimeScale);
        }
    }

    private static bool Ready(string scene, CharacterSpawner spawner, CameraFollow camera,
        BunkerPlayerLoadoutController bunkerLoadout)
    {
        if (SceneManager.GetActiveScene().name != scene) return false;
        if (scene == "MVP")
            return spawner != null && spawner.SpawnedPlayer != null &&
                spawner.SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>() is { IsInitialized: true } &&
                HUDManager.Instance != null && HUDManager.Instance.IsPlayerBound &&
                camera != null && camera.target != null && camera.ControlledCamera != null;
        return scene != "MainMenu" || (BunkerContext.Instance != null &&
            bunkerLoadout != null && bunkerLoadout.IsReady &&
            camera != null && camera.target != null && camera.ControlledCamera != null);
    }

    private void ShowFailure()
    {
        busy = faulted = true;
        Time.timeScale = 0f;
        canvas.enabled = true;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        AudioSettingsService.Instance?.SetTransitionGain(0f);
        if (errorText == null)
        {
            var go = new GameObject("Connection recovery", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);
            errorText = go.GetComponent<TextMeshProUGUI>();
            errorText.fontSize = 22f;
            errorText.alignment = TextAlignmentOptions.Center;
            errorText.color = new Color(.4f, .75f, .85f);
            errorText.raycastTarget = false;
            errorText.rectTransform.sizeDelta = new Vector2(800f, 200f);
            errorText.text = "СБОЙ СИНХРОНИЗАЦИИ\n\nENTER — ПОВТОРИТЬ\nESC — В БУНКЕР";
        }
        errorText.gameObject.SetActive(true);
    }

    private void Recover(bool bunker)
    {
        string scene = bunker ? "MainMenu" : requestedScene;
        if (!Application.CanStreamedLevelBeLoaded(scene)) return;
        busy = false;
        Load(scene, () =>
        {
            if (bunker && RunStateManager.Instance != null && !RunStateManager.Instance.IsRunEnded)
                RunStateManager.Instance.EndRun(RunEndReason.ReturnedToBunker);
        });
    }

    private IEnumerator Fade(float from, float to, float duration, Action<float> closing)
    {
        float elapsed = 0f;
        do
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            group.alpha = Mathf.Lerp(from, to, eased);
            AudioSettingsService.Instance?.SetTransitionGain(1f - group.alpha);
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
        group.blocksRaycasts = false;
        canvas.enabled = false;
        busy = false;
        faulted = false;
        Time.timeScale = timeScale;
    }

    private void OnDisable() { StopAllCoroutines(); Release(previousTimeScale); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void BuildView()
    {
        var root = new GameObject("Laboratory shutter", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        Image background = MakeImage("Cold blackout", root.transform, new Color(.012f, .022f, .038f, 1f));
        background.raycastTarget = true;
        for (int i = 0; i < 7; i++)
        {
            var seam = MakeImage("Panel seam", root.transform, new Color(.16f, .5f, .65f, .055f));
            seam.rectTransform.anchorMin = new Vector2((i + 1f) / 8f, 0f);
            seam.rectTransform.anchorMax = new Vector2((i + 1f) / 8f, 1f);
            seam.rectTransform.sizeDelta = new Vector2(1f, 0f);
        }
        scan = MakeImage("Connection scan", root.transform, Color.cyan);
        scanRect = scan.rectTransform;
        scanRect.sizeDelta = new Vector2(3f, 0f);
        var signal = MakeImage("Connection indicator", root.transform, new Color(.2f, .65f, .8f, .45f));
        signal.rectTransform.anchorMin = signal.rectTransform.anchorMax = new Vector2(.5f, .12f);
        signal.rectTransform.sizeDelta = new Vector2(36f, 2f);
        canvas.enabled = false;
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.sizeDelta = Vector2.zero;
        return image;
    }
}
