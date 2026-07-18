using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AudioSettingsPanel : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundsSlider;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    private Action closedCallback;
    private bool listenersRegistered;

    private void Awake()
    {
        ConfigureSlider(masterSlider);
        ConfigureSlider(musicSlider);
        ConfigureSlider(soundsSlider);
        RegisterListeners();
    }

    private void OnEnable()
    {
        RefreshValues();
    }

    private void OnDestroy()
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
        AudioSettingsService service = AudioSettingsService.Instance;

        if (service == null)
            return;

        masterSlider?.SetValueWithoutNotify(service.MasterVolume);
        musicSlider?.SetValueWithoutNotify(service.MusicVolume);
        soundsSlider?.SetValueWithoutNotify(service.SoundsVolume);
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
            return;

        masterSlider?.onValueChanged.AddListener(HandleMasterChanged);
        musicSlider?.onValueChanged.AddListener(HandleMusicChanged);
        soundsSlider?.onValueChanged.AddListener(HandleSoundsChanged);
        backButton?.onClick.AddListener(Close);
        listenersRegistered = true;
    }

    private void RemoveListeners()
    {
        if (!listenersRegistered)
            return;

        masterSlider?.onValueChanged.RemoveListener(HandleMasterChanged);
        musicSlider?.onValueChanged.RemoveListener(HandleMusicChanged);
        soundsSlider?.onValueChanged.RemoveListener(HandleSoundsChanged);
        backButton?.onClick.RemoveListener(Close);
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
}
