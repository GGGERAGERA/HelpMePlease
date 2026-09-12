using TMPro;
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
    [SerializeField] private TMP_Text version;
    [SerializeField] private Image signal;
    private bool beginning;

    private void Start()
    {
        version.text = "v" + Application.version;
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
        if (!SceneTransitionOverlay.CanLoad("MainMenu")) return;
        beginning = true;
        menu.interactable = false;
        AudioSettingsService.Instance?.Save();
        EventSystem.current?.SetSelectedGameObject(null);
        // Reuse the existing fade, loading and error handling instead of another startup flow.
        SceneTransitionOverlay.Load("MainMenu");
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
