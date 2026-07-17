using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    private const string LastTrackKey = "LastMusicTrackIndex";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] tracks;

    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool randomTrack = true;
    [SerializeField] private float volume = 0.4f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
    }

    private void Start()
    {
        if (AudioService.Instance != null)
            return;

        if (playOnStart)
            PlayTrack();
    }

    public void PlayTrack()
    {
        if (tracks == null || tracks.Length == 0)
        {
            return;
        }

        int index = GetTrackIndex();

        audioSource.clip = tracks[index];
        audioSource.volume = volume;
        audioSource.Play();

        PlayerPrefs.SetInt(LastTrackKey, index);
        PlayerPrefs.Save();
    }

    private int GetTrackIndex()
    {
        if (!randomTrack || tracks.Length == 1)
            return 0;

        int lastIndex = PlayerPrefs.GetInt(LastTrackKey, -1);
        int newIndex = Random.Range(0, tracks.Length);

        while (newIndex == lastIndex)
        {
            newIndex = Random.Range(0, tracks.Length);
        }

        return newIndex;
    }
}
