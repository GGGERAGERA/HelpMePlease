using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public sealed class AudioLoopHandle
{
    private AudioService owner;
    private readonly int slotIndex;
    private readonly int generation;

    internal AudioLoopHandle(AudioService owner, int slotIndex, int generation)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
        this.generation = generation;
    }

    public bool IsPlaying =>
        owner != null && owner.IsLoopPlaying(slotIndex, generation);

    public void Stop()
    {
        if (owner == null)
            return;

        AudioService service = owner;
        owner = null;
        service.StopLoop(slotIndex, generation);
    }

    internal void Invalidate()
    {
        owner = null;
    }
}

public sealed class AudioService : MonoBehaviour
{
    private const string DefaultCatalogResourcePath = "Audio/VerticalSliceAudioCatalog";

    private sealed class PoolSlot
    {
        public AudioSource Source;
        public AudioCueId CueId;
        public AudioCueDefinition Definition;
        public Transform FollowTarget;
        public AudioLoopHandle Handle;
        public int Generation;
        public bool ManagedLoop;
    }

    public static AudioService Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private AudioCatalog catalog;
    [SerializeField, Min(4)] private int sfxPoolSize = 20;
    [SerializeField, Min(0f)] private float musicCrossfadeDuration = 0.75f;

    [Header("Optional Mixer Groups")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup uiMixerGroup;
    [SerializeField] private AudioMixerGroup ambienceMixerGroup;

    [Header("Category Volumes")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 1f;

    private readonly List<PoolSlot> pool = new();
    private readonly Dictionary<AudioCueId, float> lastPlayTimes = new();

    private AudioSource[] musicSources;
    private AudioSource ambienceSource;
    private int currentMusicSourceIndex;
    private AudioCueId currentMusicCue;
    private AudioCueId currentAmbienceCue;
    private Coroutine musicFadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
            return;

        GameObject bootstrap = new("[AudioService]");
        bootstrap.AddComponent<AudioService>();
        bootstrap.AddComponent<AudioSceneDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.AdoptCatalogIfMissing(catalog);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (catalog == null)
            catalog = Resources.Load<AudioCatalog>(DefaultCatalogResourcePath);

        CreateFixedSources();
    }

    private void Update()
    {
        RefreshPool();

        for (int i = 0; i < pool.Count; i++)
        {
            PoolSlot slot = pool[i];

            if (slot.CueId == AudioCueId.None || slot.FollowTarget == null)
                continue;

            slot.Source.transform.position = slot.FollowTarget.position;
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        StopAllManagedLoops();
        Instance = null;
    }

    public bool Play(AudioCueId cueId)
    {
        return PlayInternal(cueId, transform.position, false);
    }

    public bool PlayAt(AudioCueId cueId, Vector3 position)
    {
        return PlayInternal(cueId, position, true);
    }

    public AudioLoopHandle StartLoop(
        AudioCueId cueId,
        Transform followTarget = null,
        Vector3? position = null
    )
    {
        if (!TryPreparePlayback(cueId, out AudioCueDefinition definition, out AudioClip clip))
            return null;

        if (!definition.Loop)
            return null;

        PoolSlot slot = AcquireSlot(definition);

        if (slot == null)
            return null;

        Vector3 sourcePosition = followTarget != null
            ? followTarget.position
            : position ?? transform.position;

        ConfigureSource(slot.Source, definition, sourcePosition, followTarget != null || position.HasValue);
        slot.Source.clip = clip;
        slot.Source.loop = true;

        slot.CueId = cueId;
        slot.Definition = definition;
        slot.FollowTarget = followTarget;
        slot.ManagedLoop = true;
        slot.Generation++;
        slot.Handle = new AudioLoopHandle(this, pool.IndexOf(slot), slot.Generation);

        slot.Source.Play();
        MarkPlayed(cueId);
        return slot.Handle;
    }

    public bool PlayMusic(AudioCueId cueId)
    {
        if (currentMusicCue == cueId &&
            musicSources != null &&
            musicSources[currentMusicSourceIndex].isPlaying)
        {
            return true;
        }

        if (!TryGetPlayableCue(cueId, AudioCategory.Music, out AudioCueDefinition definition, out AudioClip clip))
            return false;

        int nextIndex = currentMusicSourceIndex == 0 ? 1 : 0;
        AudioSource previous = musicSources[currentMusicSourceIndex];
        AudioSource next = musicSources[nextIndex];

        if (musicFadeRoutine != null)
            StopCoroutine(musicFadeRoutine);

        next.Stop();
        ConfigureDedicatedSource(next, definition);
        next.clip = clip;
        next.loop = definition.Loop;
        next.volume = 0f;
        next.Play();

        currentMusicCue = cueId;
        currentMusicSourceIndex = nextIndex;
        musicFadeRoutine = StartCoroutine(
            CrossfadeMusic(previous, next, definition.Volume * GetCategoryVolume(AudioCategory.Music))
        );

        return true;
    }

    public bool PlayAmbience(AudioCueId cueId)
    {
        if (currentAmbienceCue == cueId && ambienceSource != null && ambienceSource.isPlaying)
            return true;

        if (!TryGetPlayableCue(cueId, AudioCategory.Ambience, out AudioCueDefinition definition, out AudioClip clip))
            return false;

        ambienceSource.Stop();
        ConfigureDedicatedSource(ambienceSource, definition);
        ambienceSource.clip = clip;
        ambienceSource.loop = definition.Loop;
        ambienceSource.volume = definition.Volume * GetCategoryVolume(AudioCategory.Ambience);
        ambienceSource.Play();
        currentAmbienceCue = cueId;
        return true;
    }

    public void StopAmbience()
    {
        currentAmbienceCue = AudioCueId.None;

        if (ambienceSource != null)
            ambienceSource.Stop();
    }

    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);

        switch (category)
        {
            case AudioCategory.Music:
                musicVolume = volume;
                break;
            case AudioCategory.SFX:
                sfxVolume = volume;
                break;
            case AudioCategory.UI:
                uiVolume = volume;
                break;
            case AudioCategory.Ambience:
                ambienceVolume = volume;
                break;
        }

        RefreshActiveVolumes();
    }

    internal bool IsLoopPlaying(int slotIndex, int generation)
    {
        if (slotIndex < 0 || slotIndex >= pool.Count)
            return false;

        PoolSlot slot = pool[slotIndex];
        return slot.ManagedLoop &&
               slot.Generation == generation &&
               slot.Source != null &&
               slot.Source.isPlaying;
    }

    internal void StopLoop(int slotIndex, int generation)
    {
        if (slotIndex < 0 || slotIndex >= pool.Count)
            return;

        PoolSlot slot = pool[slotIndex];

        if (!slot.ManagedLoop || slot.Generation != generation)
            return;

        slot.Source.Stop();
        ClearSlot(slot);
    }

    private bool PlayInternal(AudioCueId cueId, Vector3 position, bool positional)
    {
        if (!TryPreparePlayback(cueId, out AudioCueDefinition definition, out AudioClip clip))
            return false;

        if (definition.Loop)
            return false;

        PoolSlot slot = AcquireSlot(definition);

        if (slot == null)
            return false;

        ConfigureSource(slot.Source, definition, position, positional);
        slot.Source.clip = clip;
        slot.Source.loop = false;

        slot.CueId = cueId;
        slot.Definition = definition;
        slot.FollowTarget = null;
        slot.ManagedLoop = false;
        slot.Handle = null;

        slot.Source.Play();
        MarkPlayed(cueId);
        return true;
    }

    private bool TryPreparePlayback(
        AudioCueId cueId,
        out AudioCueDefinition definition,
        out AudioClip clip
    )
    {
        definition = null;
        clip = null;

        if (catalog == null || !catalog.TryGet(cueId, out definition))
            return false;

        if (!definition.TryGetRandomClip(out clip))
            return false;

        if (lastPlayTimes.TryGetValue(cueId, out float lastTime) &&
            Time.unscaledTime < lastTime + definition.Cooldown)
        {
            return false;
        }

        return CountActive(cueId) < definition.MaxSimultaneous;
    }

    private bool TryGetPlayableCue(
        AudioCueId cueId,
        AudioCategory expectedCategory,
        out AudioCueDefinition definition,
        out AudioClip clip
    )
    {
        definition = null;
        clip = null;

        return catalog != null &&
               catalog.TryGet(cueId, out definition) &&
               definition.Category == expectedCategory &&
               definition.TryGetRandomClip(out clip);
    }

    private PoolSlot AcquireSlot(AudioCueDefinition definition)
    {
        RefreshPool();

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].CueId == AudioCueId.None)
                return pool[i];
        }

        return null;
    }

    private int CountActive(AudioCueId cueId)
    {
        RefreshPool();
        int count = 0;

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].CueId == cueId)
                count++;
        }

        return count;
    }

    private void CreateFixedSources()
    {
        musicSources = new[]
        {
            CreateSource("Music A"),
            CreateSource("Music B")
        };

        ambienceSource = CreateSource("Ambience");

        for (int i = 0; i < Mathf.Max(4, sfxPoolSize); i++)
        {
            pool.Add(new PoolSlot
            {
                Source = CreateSource($"SFX {i + 1:00}"),
                CueId = AudioCueId.None
            });
        }
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject child = new(sourceName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.dopplerLevel = 0f;
        return source;
    }

    private void ConfigureSource(
        AudioSource source,
        AudioCueDefinition definition,
        Vector3 position,
        bool positional
    )
    {
        source.transform.position = position;
        source.outputAudioMixerGroup = GetMixerGroup(definition.Category);
        source.volume = definition.Volume * GetCategoryVolume(definition.Category);
        source.pitch = Random.Range(definition.PitchMin, definition.PitchMax);
        source.spatialBlend = positional ? definition.SpatialBlend : 0f;
        source.playOnAwake = false;
    }

    private void ConfigureDedicatedSource(AudioSource source, AudioCueDefinition definition)
    {
        source.outputAudioMixerGroup = GetMixerGroup(definition.Category);
        source.pitch = Random.Range(definition.PitchMin, definition.PitchMax);
        source.spatialBlend = 0f;
        source.playOnAwake = false;
    }

    private IEnumerator CrossfadeMusic(
        AudioSource previous,
        AudioSource next,
        float targetVolume
    )
    {
        float duration = Mathf.Max(0f, musicCrossfadeDuration);
        float previousStartVolume = previous != null ? previous.volume : 0f;

        if (duration <= 0f)
        {
            if (previous != null)
                previous.Stop();

            next.volume = targetVolume;
            musicFadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (previous != null)
                previous.volume = Mathf.Lerp(previousStartVolume, 0f, t);

            next.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        if (previous != null)
            previous.Stop();

        next.volume = targetVolume;
        musicFadeRoutine = null;
    }

    private void RefreshPool()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            PoolSlot slot = pool[i];

            if (slot.CueId != AudioCueId.None && !slot.Source.isPlaying)
                ClearSlot(slot);
        }
    }

    private void ClearSlot(PoolSlot slot)
    {
        slot.Handle?.Invalidate();
        slot.Handle = null;
        slot.CueId = AudioCueId.None;
        slot.Definition = null;
        slot.FollowTarget = null;
        slot.ManagedLoop = false;
        slot.Source.clip = null;
        slot.Source.loop = false;
    }

    private void StopAllManagedLoops()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            PoolSlot slot = pool[i];

            if (!slot.ManagedLoop)
                continue;

            slot.Source.Stop();
            ClearSlot(slot);
        }
    }

    private void RefreshActiveVolumes()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            PoolSlot slot = pool[i];

            if (slot.Definition != null)
            {
                slot.Source.volume =
                    slot.Definition.Volume * GetCategoryVolume(slot.Definition.Category);
            }
        }

        if (catalog != null &&
            catalog.TryGet(currentMusicCue, out AudioCueDefinition musicDefinition))
        {
            musicSources[currentMusicSourceIndex].volume =
                musicDefinition.Volume * GetCategoryVolume(AudioCategory.Music);
        }

        if (catalog != null &&
            catalog.TryGet(currentAmbienceCue, out AudioCueDefinition ambienceDefinition))
        {
            ambienceSource.volume =
                ambienceDefinition.Volume * GetCategoryVolume(AudioCategory.Ambience);
        }
    }

    private float GetCategoryVolume(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.Music => musicVolume,
            AudioCategory.UI => uiVolume,
            AudioCategory.Ambience => ambienceVolume,
            _ => sfxVolume
        };
    }

    private AudioMixerGroup GetMixerGroup(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.Music => musicMixerGroup,
            AudioCategory.UI => uiMixerGroup,
            AudioCategory.Ambience => ambienceMixerGroup,
            _ => sfxMixerGroup
        };
    }

    private void MarkPlayed(AudioCueId cueId)
    {
        lastPlayTimes[cueId] = Time.unscaledTime;
    }

    private void AdoptCatalogIfMissing(AudioCatalog candidate)
    {
        if (catalog == null && candidate != null)
            catalog = candidate;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        sfxPoolSize = Mathf.Max(4, sfxPoolSize);
        musicCrossfadeDuration = Mathf.Max(0f, musicCrossfadeDuration);
    }
#endif
}
