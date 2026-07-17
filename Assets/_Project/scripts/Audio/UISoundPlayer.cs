using UnityEngine;

public class UISoundPlayer : MonoBehaviour
{
    public static UISoundPlayer Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip panelSwitchSound;
    [SerializeField] private float panelSwitchVolume = 0.4f;

    public void PlayPanelSwitch()
    {
        Play(panelSwitchSound, panelSwitchVolume);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Play(AudioClip clip, float volume = 0.5f)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }
}
