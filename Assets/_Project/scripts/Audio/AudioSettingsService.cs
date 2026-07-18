using System;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class AudioSettingsService : MonoBehaviour
{
    public const string MasterVolumeKey = "audio.master.volume";
    public const string MusicVolumeKey = "audio.music.volume";
    public const string SoundsVolumeKey = "audio.sounds.volume";

    public const string MasterVolumeParameter = "MasterVolume";
    public const string MusicVolumeParameter = "MusicVolume";
    public const string SoundsVolumeParameter = "SFXVolume";

    private const string MixerResourcePath = "Audio/VerticalSliceAudioMixer";
    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.8f;
    private const float DefaultSoundsVolume = 0.9f;
    private const float MutedDecibels = -80f;

    public static AudioSettingsService Instance { get; private set; }

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SoundsVolume { get; private set; }

    private AudioMixer mixer;
    private bool hasPendingChanges;
    private bool warnedAboutMissingMixer;
    private bool warnedAboutMissingParameter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        mixer = Resources.Load<AudioMixer>(MixerResourcePath);

        LoadValues();
        ConfigurePlaybackRouting();
        ApplyAll();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Save();
        Instance = null;
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        hasPendingChanges = true;
        Apply(MasterVolumeParameter, MasterVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        hasPendingChanges = true;
        Apply(MusicVolumeParameter, MusicVolume);
    }

    public void SetSoundsVolume(float value)
    {
        SoundsVolume = Mathf.Clamp01(value);
        hasPendingChanges = true;
        Apply(SoundsVolumeParameter, SoundsVolume);
    }

    public void Save()
    {
        if (!hasPendingChanges)
            return;

        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.SetFloat(SoundsVolumeKey, SoundsVolume);
        PlayerPrefs.Save();
        hasPendingChanges = false;
    }

    private void LoadValues()
    {
        MasterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume)
        );
        MusicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume)
        );
        SoundsVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(SoundsVolumeKey, DefaultSoundsVolume)
        );
    }

    private void ConfigurePlaybackRouting()
    {
        AudioService audioService = GetComponent<AudioService>();

        if (audioService == null || mixer == null)
            return;

        AudioMixerGroup music = FindGroup("Music");
        AudioMixerGroup sfx = FindGroup("SFX");
        AudioMixerGroup ui = FindGroup("UI");
        AudioMixerGroup ambience = FindGroup("Ambience");

        audioService.ConfigureMixerGroups(music, sfx, ui, ambience);
    }

    private AudioMixerGroup FindGroup(string exactName)
    {
        AudioMixerGroup[] groups = mixer.FindMatchingGroups(exactName);
        return Array.Find(groups, group => group != null && group.name == exactName);
    }

    private void ApplyAll()
    {
        Apply(MasterVolumeParameter, MasterVolume);
        Apply(MusicVolumeParameter, MusicVolume);
        Apply(SoundsVolumeParameter, SoundsVolume);
    }

    private void Apply(string parameterName, float linearValue)
    {
        if (mixer == null)
        {
            if (!warnedAboutMissingMixer)
            {
                Debug.LogWarning(
                    $"[AudioSettingsService] AudioMixer Resources/{MixerResourcePath} is missing."
                );
                warnedAboutMissingMixer = true;
            }

            return;
        }

        float decibels = linearValue <= 0f
            ? MutedDecibels
            : Mathf.Log10(Mathf.Max(linearValue, 0.0001f)) * 20f;

        if (!mixer.SetFloat(parameterName, decibels) && !warnedAboutMissingParameter)
        {
            Debug.LogWarning(
                $"[AudioSettingsService] Exposed mixer parameter '{parameterName}' is missing."
            );
            warnedAboutMissingParameter = true;
        }
    }
}
