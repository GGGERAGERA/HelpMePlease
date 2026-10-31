using System;
using System.Collections.Generic;
using UnityEngine;

public enum BunkerBarkCategory { Random, Event, Room }

[Serializable]
public sealed class BunkerBarkLine
{
    [TextArea(1, 3)] public string text;
    public BunkerBarkCategory category;
    [Tooltip("Room trigger ID or event ID. Unused for Random.")]
    public string key;
    [Range(0f, 1f)] public float probability = 1f;
}

/// <summary>Optional scene-local flavour. Never owns input, progression or time scale.</summary>
[DisallowMultipleComponent]
public sealed class BunkerAmbientBarks : MonoBehaviour
{
    public static BunkerAmbientBarks Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private Transform player;
    [SerializeField] private BunkerBarkView view;
    [SerializeField] private BunkerPanelManager panels;
    [SerializeField] private BunkerIntroController intro;
    [SerializeField] private BunkerMinigame[] minigames;
    [Tooltip("Fullscreen events, run summary and any additional modal UI roots.")]
    [SerializeField] private GameObject[] blockingInterfaces;

    [Header("Timing (seconds of unobstructed bunker time)")]
    [SerializeField] private Vector2 randomCooldown = new(60f, 120f);
    [SerializeField, Min(0f)] private float roomCooldown = 90f;
    [SerializeField, Range(3f, 4f)] private float duration = 3.6f;
    [SerializeField, Range(.05f, .5f)] private float fade = .2f;

    [Header("Editable lines: Room has priority over Event, then Random")]
    [SerializeField] private List<BunkerBarkLine> lines = new();

    private readonly List<BunkerBarkLine> candidates = new();
    private readonly Dictionary<string, float> roomReadyAt = new(StringComparer.Ordinal);
    private string lastText;
    private float quietTime;
    private float randomRemaining;
    private float elapsed;
    private BunkerBarkCategory activeCategory;
    private Transform speaker;

    public Transform Player => player;
    public bool IsSpeaking { get; private set; }
    public string CurrentText => IsSpeaking ? lastText : string.Empty;

    public bool IsBlocked
    {
        get
        {
            if (!isActiveAndEnabled || player == null || !player.gameObject.activeInHierarchy ||
                Time.timeScale <= 0f || SceneTransitionOverlay.IsTransitioning ||
                (panels != null && panels.IsAnyPanelOpen) || (intro != null && intro.IsPlaying)) return true;
            if (minigames != null)
                foreach (var game in minigames)
                    if (game != null && game.isActiveAndEnabled && game.IsRunning) return true;
            if (blockingInterfaces != null)
                foreach (var root in blockingInterfaces)
                    if (root != null && root.activeInHierarchy) return true;
            return false;
        }
    }

    private void Awake()
    {
        Instance = this;
        ScheduleRandom();
        Hide();
    }

    private void OnDisable() => Hide();
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void LateUpdate()
    {
        if (IsBlocked)
        {
            Hide(); // Drop, never queue stale room/event lines behind a menu.
            return;
        }
        quietTime += Time.deltaTime;
        randomRemaining -= Time.deltaTime;
        if (IsSpeaking)
        {
            elapsed += Time.deltaTime;
            if (speaker == null || !speaker.gameObject.activeInHierarchy || elapsed >= duration) Hide();
            else view.Present(speaker, Mathf.Min(Mathf.Clamp01(elapsed / fade), Mathf.Clamp01((duration - elapsed) / fade)));
        }
        if (!IsSpeaking && randomRemaining <= 0f)
        {
            ScheduleRandom();
            TryPlay(BunkerBarkCategory.Random, null, player);
        }
    }

    /// <summary>Call with the Inspector event key; optional speaker supports future NPCs.</summary>
    public bool TryPlayEvent(string eventId, Transform eventSpeaker = null) =>
        !string.IsNullOrWhiteSpace(eventId) && TryPlay(BunkerBarkCategory.Event, eventId, eventSpeaker);

    /// <summary>One-off story/NPC text with the same UI, repeat and priority guards.</summary>
    public bool TrySpeak(string text, Transform eventSpeaker = null) =>
        CanPlay(BunkerBarkCategory.Event) && Show(text, BunkerBarkCategory.Event, eventSpeaker);

    // Void wrapper is also selectable in Inspector UnityEvents.
    public void PlayEvent(string eventId) => TryPlayEvent(eventId);

    public bool TryEnterRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) ||
            (roomReadyAt.TryGetValue(roomId, out float readyAt) && quietTime < readyAt)) return false;
        if (!TryPlay(BunkerBarkCategory.Room, roomId, player)) return false;
        roomReadyAt[roomId] = quietTime + roomCooldown;
        return true;
    }

    private bool CanPlay(BunkerBarkCategory category) => view != null && !IsBlocked &&
        (!IsSpeaking || category > activeCategory);

    private bool TryPlay(BunkerBarkCategory category, string key, Transform eventSpeaker)
    {
        if (!CanPlay(category)) return false;
        candidates.Clear();
        foreach (var line in lines)
            if (line != null && line.category == category && (category == BunkerBarkCategory.Random || line.key == key) &&
                !string.IsNullOrWhiteSpace(line.text) && line.text != lastText && line.probability > 0f &&
                UnityEngine.Random.value < line.probability)
                candidates.Add(line);
        return candidates.Count > 0 && Show(candidates[UnityEngine.Random.Range(0, candidates.Count)].text, category, eventSpeaker);
    }

    private bool Show(string text, BunkerBarkCategory category, Transform eventSpeaker)
    {
        if (string.IsNullOrWhiteSpace(text) || text == lastText) return false;
        lastText = text;
        speaker = eventSpeaker != null ? eventSpeaker : player;
        activeCategory = category;
        elapsed = 0f;
        IsSpeaking = true;
        view.SetText(text);
        view.Present(speaker, 0f);
        ScheduleRandom();
        return true;
    }

    private void ScheduleRandom() => randomRemaining = UnityEngine.Random.Range(randomCooldown.x, randomCooldown.y);
    private void Hide() { IsSpeaking = false; if (view != null) view.Hide(); }

#if UNITY_EDITOR
    private void OnValidate()
    {
        randomCooldown.x = Mathf.Clamp(randomCooldown.x, 60f, 120f);
        randomCooldown.y = Mathf.Clamp(randomCooldown.y, randomCooldown.x, 120f);
        duration = Mathf.Clamp(duration, 3f, 4f);
        fade = Mathf.Clamp(fade, .05f, .5f);
    }
#endif
}
