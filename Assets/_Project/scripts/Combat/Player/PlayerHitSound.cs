using UnityEngine;

public class PlayerHitSound : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField] private float volume = 0.7f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        AudioService.Instance?.RouteExternalSource(
            audioSource,
            AudioCategory.SFX
        );
    }

    public void Play()
    {
        if (audioSource == null || hitClips == null || hitClips.Length == 0)
            return;

        AudioService.Instance?.RouteExternalSource(
            audioSource,
            AudioCategory.SFX
        );

        AudioClip clip = hitClips[Random.Range(0, hitClips.Length)];
        audioSource.PlayOneShot(clip, volume);
    }
}
