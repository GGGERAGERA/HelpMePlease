using System;
using System.Collections.Generic;
using UnityEngine;

public enum AudioCategory
{
    Music,
    SFX,
    UI,
    Ambience
}

[Serializable]
public sealed class AudioCueDefinition
{
    [SerializeField] private AudioCueId id;
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float pitchMin = 1f;
    [SerializeField] private float pitchMax = 1f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend;
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField, Min(1)] private int maxSimultaneous = 1;
    [SerializeField] private bool loop;
    [SerializeField] private AudioCategory category = AudioCategory.SFX;

    public AudioCueId Id => id;
    public float Volume => Mathf.Clamp01(volume);
    public float PitchMin => Mathf.Min(pitchMin, pitchMax);
    public float PitchMax => Mathf.Max(pitchMin, pitchMax);
    public float SpatialBlend => Mathf.Clamp01(spatialBlend);
    public float Cooldown => Mathf.Max(0f, cooldown);
    public int MaxSimultaneous => Mathf.Max(1, maxSimultaneous);
    public bool Loop => loop;
    public AudioCategory Category => category;

    public bool TryGetRandomClip(out AudioClip clip)
    {
        clip = null;

        if (clips == null || clips.Length == 0)
            return false;

        int startIndex = UnityEngine.Random.Range(0, clips.Length);

        for (int offset = 0; offset < clips.Length; offset++)
        {
            AudioClip candidate = clips[(startIndex + offset) % clips.Length];

            if (candidate == null)
                continue;

            clip = candidate;
            return true;
        }

        return false;
    }
}

[CreateAssetMenu(
    fileName = "VerticalSliceAudioCatalog",
    menuName = "Game/Audio/Audio Catalog"
)]
public sealed class AudioCatalog : ScriptableObject
{
    [SerializeField] private List<AudioCueDefinition> cues = new();

    private readonly Dictionary<AudioCueId, AudioCueDefinition> lookup = new();

    private void OnEnable()
    {
        RebuildLookup();
    }

    public bool TryGet(AudioCueId id, out AudioCueDefinition definition)
    {
        if (id == AudioCueId.None)
        {
            definition = null;
            return false;
        }

        if (lookup.Count == 0 && cues != null && cues.Count > 0)
            RebuildLookup();

        return lookup.TryGetValue(id, out definition) && definition != null;
    }

    private void RebuildLookup()
    {
        lookup.Clear();

        if (cues == null)
            return;

        foreach (AudioCueDefinition cue in cues)
        {
            if (cue == null || cue.Id == AudioCueId.None)
                continue;

            lookup[cue.Id] = cue;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();

        if (cues == null)
            return;

        HashSet<AudioCueId> seenIds = new();

        foreach (AudioCueDefinition cue in cues)
        {
            if (cue == null)
                continue;

            if (cue.Id == AudioCueId.None)
            {
                Debug.LogWarning("[AudioCatalog] A cue uses AudioCueId.None.", this);
            }
            else if (!seenIds.Add(cue.Id))
            {
                Debug.LogWarning($"[AudioCatalog] Duplicate cue: {cue.Id}.", this);
            }

            if (cue.PitchMin <= 0f || cue.PitchMax <= 0f)
                Debug.LogWarning($"[AudioCatalog] {cue.Id} has non-positive pitch.", this);

            if (cue.Category == AudioCategory.UI && cue.Loop)
                Debug.LogWarning($"[AudioCatalog] UI cue {cue.Id} must not loop.", this);
        }
    }
#endif
}
