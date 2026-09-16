using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AudioSettingsPanel : MonoBehaviour
{
    public bool IsOpen => gameObject.activeInHierarchy;

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundsSlider;

    [Header("Language")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("Display")]
    [SerializeField] private TMP_Dropdown windowModeDropdown;

    [Header("Accessibility")]
    [SerializeField] private Toggle automaticFireToggle;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    private Action closedCallback;
    private bool listenersRegistered;

    private void Awake()
    {
        ConfigureSlider(masterSlider);
        ConfigureSlider(musicSlider);
        ConfigureSlider(soundsSlider);
        ConfigureLanguageDropdown();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RefreshValues();
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    public void Open()
    {
        Open(null);
    }

    public void Open(Action onClosed)
    {
        closedCallback = onClosed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshValues();
    }

    public void Close()
    {
        AudioSettingsService.Instance?.Save();

        Action callback = closedCallback;
        closedCallback = null;
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    private void RefreshValues()
    {
        AudioSettingsService audioService =
            AudioSettingsService.Instance;

        if (audioService != null)
        {
            masterSlider?.SetValueWithoutNotify(
                audioService.MasterVolume
            );
            musicSlider?.SetValueWithoutNotify(
                audioService.MusicVolume
            );
            soundsSlider?.SetValueWithoutNotify(
                audioService.SoundsVolume
            );
        }

        LocalizationService localization =
            LocalizationService.EnsureExists();
        languageDropdown?.SetValueWithoutNotify(
            (int)localization.CurrentLanguage
        );
        languageDropdown?.RefreshShownValue();
        RefreshWindowModes(localization.CurrentLanguage);
        automaticFireToggle?.SetIsOnWithoutNotify(
            WeaponControlSettings.AutomaticFireEnabled
        );
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
            return;

        masterSlider?.onValueChanged.AddListener(HandleMasterChanged);
        musicSlider?.onValueChanged.AddListener(HandleMusicChanged);
        soundsSlider?.onValueChanged.AddListener(HandleSoundsChanged);
        languageDropdown?.onValueChanged.AddListener(
            HandleLanguageChanged
        );
        automaticFireToggle?.onValueChanged.AddListener(
            HandleAutomaticFireChanged
        );
        backButton?.onClick.AddListener(Close);
        windowModeDropdown?.onValueChanged.AddListener(HandleWindowModeChanged);
        LocalizationService.EnsureExists().LanguageChanged += RefreshWindowModes;
        listenersRegistered = true;
    }

    private void RemoveListeners()
    {
        if (!listenersRegistered)
            return;

        masterSlider?.onValueChanged.RemoveListener(HandleMasterChanged);
        musicSlider?.onValueChanged.RemoveListener(HandleMusicChanged);
        soundsSlider?.onValueChanged.RemoveListener(HandleSoundsChanged);
        languageDropdown?.onValueChanged.RemoveListener(
            HandleLanguageChanged
        );
        automaticFireToggle?.onValueChanged.RemoveListener(
            HandleAutomaticFireChanged
        );
        backButton?.onClick.RemoveListener(Close);
        windowModeDropdown?.onValueChanged.RemoveListener(HandleWindowModeChanged);
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= RefreshWindowModes;
        listenersRegistered = false;
    }

    private static void ConfigureSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void ConfigureLanguageDropdown()
    {
        if (languageDropdown == null)
            return;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(
            new List<string>
            {
                "Русский",
                "English"
            }
        );
    }

    private static void HandleMasterChanged(float value)
    {
        AudioSettingsService.Instance?.SetMasterVolume(value);
    }

    private static void HandleMusicChanged(float value)
    {
        AudioSettingsService.Instance?.SetMusicVolume(value);
    }

    private static void HandleSoundsChanged(float value)
    {
        AudioSettingsService.Instance?.SetSoundsVolume(value);
    }

    private static void HandleLanguageChanged(int value)
    {
        GameLanguage language = value == (int)GameLanguage.Russian
            ? GameLanguage.Russian
            : GameLanguage.English;
        LocalizationService.EnsureExists().SetLanguage(language);
    }

    private static void HandleAutomaticFireChanged(bool enabled)
    {
        WeaponControlSettings.SetAutomaticFire(enabled);
    }

    private void RefreshWindowModes(GameLanguage language)
    {
        if (windowModeDropdown == null) return;
        var localization = LocalizationService.EnsureExists();
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new List<string>
        {
            localization.Get("settings.windowed"),
            localization.Get("settings.fullscreen")
        });
        windowModeDropdown.SetValueWithoutNotify(WindowModeSettings.Fullscreen ? 1 : 0);
        windowModeDropdown.RefreshShownValue();
    }

    private static void HandleWindowModeChanged(int value) =>
        WindowModeSettings.SetFullscreen(value == 1);
}
