using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioSceneDirector : MonoBehaviour
{
    private const string BunkerSceneName = "MainMenu";
    private const string RunSceneName = "MVP";

    private AudioService service;

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

        switch (scene.name)
        {
            case BunkerSceneName:
                service.PlayMusic(AudioCueId.BunkerMusic);
                service.PlayAmbience(AudioCueId.BunkerAmbience);
                break;

            case RunSceneName:
                service.PlayMusic(AudioCueId.RunMusic);
                service.StopAmbience();
                break;
        }
    }
}
