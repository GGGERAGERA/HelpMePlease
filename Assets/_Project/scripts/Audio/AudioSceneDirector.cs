using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioSceneDirector : MonoBehaviour
{
    private const string RunSceneName = "MVP";

    private AudioService service;
    private Scene audioScene;

    private void Awake()
    {
        service = GetComponent<AudioService>();

        if (service == null)
            service = AudioService.Instance;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        ApplySceneAudio(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneAudio(scene);
    }

    private void ApplySceneAudio(Scene scene)
    {
        if (service == null)
            service = AudioService.Instance;

        if (service == null)
            return;

        // sceneLoaded may precede this component's first Start; select once per entry.
        if (audioScene.IsValid() && audioScene == scene) return;
        bool leavingStartScreen = audioScene.IsValid() && audioScene.name == "StartScreen";
        audioScene = scene;

        switch (scene.name)
        {
            case "StartScreen":
                service.StopMusic();
                service.StopAmbience();
                AudioSettingsService.Instance.SetMusicGain(1f);
                service.PlayMusic(AudioCueId.StartScreenMusic);
                break;

            case RunEndService.BunkerSceneName:
                service.StopAmbience();
                if (leavingStartScreen) service.StopMusic();
                AudioSettingsService.Instance.SetMusicGain(1f);
                service.PlayMusic(AudioCueId.BunkerMusic);
                service.PlayAmbience(AudioCueId.BunkerAmbience);
                break;

            case RunSceneName:
                AudioSettingsService.Instance.SetMusicGain(1f);
                service.PlayMusic(AudioCueId.RunMusic);
                service.StopAmbience();
                break;
        }
    }
}
