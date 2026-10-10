using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Navigation for the authored StartScreen scene. Gameplay owns its own return flow.</summary>
public sealed class StartScreenController : MonoBehaviour
{
    [SerializeField] private CanvasGroup menu;
    [SerializeField] private Button beginButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private AudioSettingsPanel settings;
    [SerializeField] private Image signal;
    [SerializeField] private StartScreenAtmosphere atmosphere;
    private bool beginning;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        beginButton.Select();
    }

    private void OnEnable()
    {
        beginButton.onClick.AddListener(Begin);
        settingsButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(Exit);
    }

    private void OnDisable()
    {
        beginButton.onClick.RemoveListener(Begin);
        settingsButton.onClick.RemoveListener(OpenSettings);
        exitButton.onClick.RemoveListener(Exit);
        StopAllCoroutines();
        beginning = false;
        menu.alpha = 1f;
        menu.interactable = menu.blocksRaycasts = true;
        atmosphere.ResetAwakening();
    }

    private void Update()
    {
        Color color = signal.color;
        color.a = .25f + .1f * Mathf.Sin(Time.unscaledTime * 1.2f);
        signal.color = color;
        if (!beginning && settings.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            settings.Close();
    }

    public void Begin()
    {
        if (beginning || settings.IsOpen) return;
        if (!SceneTransitionOverlay.CanLoad(RunEndService.BunkerSceneName)) return;
        beginning = true;
        menu.interactable = false;
        menu.blocksRaycasts = false;
        menu.alpha = 0f;
        AudioSettingsService.Instance?.Save();
        EventSystem.current?.SetSelectedGameObject(null);
        StartCoroutine(BeginSequence());
    }

    private IEnumerator BeginSequence()
    {
        yield return new WaitForSecondsRealtime(2f);
        yield return atmosphere.PlayAwakening();
        // Loading, readiness, fade and recovery remain owned by the shared transition.
        if (SceneTransitionOverlay.Load(RunEndService.BunkerSceneName,
            closing: atmosphere.FadeMenuMusic, closingDuration: atmosphere.FadeDuration,
            afterBlack: atmosphere.PlayGlassBreak, fadeMaster: false)) yield break;
        beginning = false;
        menu.alpha = 1f;
        menu.interactable = menu.blocksRaycasts = true;
        atmosphere.ResetAwakening();
        beginButton.Select();
    }

    public void OpenSettings()
    {
        if (beginning || settings.IsOpen) return;
        menu.interactable = false;
        settings.Open(() =>
        {
            menu.interactable = true;
            settingsButton.Select();
        });
        settings.GetComponentInChildren<Selectable>()?.Select();
    }

    public void Exit()
    {
        if (beginning) return;
        AudioSettingsService.Instance?.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
