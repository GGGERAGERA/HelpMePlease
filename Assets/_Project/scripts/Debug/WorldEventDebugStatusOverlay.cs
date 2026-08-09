using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class WorldEventDebugStatusOverlay : MonoBehaviour
{
    public enum EventDebugState
    {
        None,
        Ready,
        Running,
        Complete,
        Blocked
    }

    [SerializeField, Min(1f)] private float nearbyDistance = 4.5f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;
    [SerializeField, Min(0.1f)] private float completionDisplaySeconds = 2f;

    private WorldEventSpawner eventSpawner;
    private Transform player;
    private PlayerInteractor playerInteractor;
    private float nextRefreshTime;
    private float completionVisibleUntil;
    private EventDebugState state;
    private string blockedReason = string.Empty;
    private GUIStyle stateStyle;
    private GUIStyle detailStyle;
    private bool overlayVisible = true;

    public EventDebugState State => state;
    public string StateLabel => state.ToString().ToUpperInvariant();
    public string BlockedReason => blockedReason;
    public bool ShowStartPrompt => state == EventDebugState.Ready;
    public bool OverlayVisible => overlayVisible;

    public void SetOverlayVisible(bool visible) => overlayVisible = visible;

    public void Configure(WorldEventSpawner spawner)
    {
        if (eventSpawner != null)
            eventSpawner.EventCompleted -= HandleEventCompleted;

        eventSpawner = spawner;
        if (eventSpawner != null)
            eventSpawner.EventCompleted += HandleEventCompleted;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshState();
    }

    private void RefreshState()
    {
        ResolvePlayer();
        blockedReason = string.Empty;

        if (eventSpawner == null || player == null)
        {
            state = EventDebugState.None;
            return;
        }

        WorldEvent runningEvent = eventSpawner.ActiveEvent;
        if (runningEvent != null && !runningEvent.IsCompleted)
        {
            state = EventDebugState.Running;
            return;
        }

        WorldEvent nearest = null;
        float nearestDistanceSquared = nearbyDistance * nearbyDistance;
        System.Collections.Generic.IReadOnlyList<WorldEvent> events =
            eventSpawner.SpawnedEvents;
        for (int i = 0; i < events.Count; i++)
        {
            WorldEvent candidate = events[i];
            if (candidate == null || candidate.IsCompleted)
                continue;

            float distanceSquared = (
                (Vector2)candidate.transform.position -
                (Vector2)player.position
            ).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearest = candidate;
        }

        if (nearest == null)
        {
            state = Time.unscaledTime < completionVisibleUntil
                ? EventDebugState.Complete
                : EventDebugState.None;
            return;
        }

        if (nearest.IsStarted)
        {
            state = EventDebugState.Running;
            return;
        }

        if (!nearest.CanInteract)
        {
            state = EventDebugState.Blocked;
            blockedReason = eventSpawner.ActiveEvent != null
                ? "Another Event is running"
                : "Move closer / CanStartEvent=false";
            return;
        }

        if (playerInteractor == null)
        {
            state = EventDebugState.Blocked;
            blockedReason = "No PlayerInteractor";
            return;
        }

        if (playerInteractor.GetCurrentInteractable() != nearest)
        {
            state = EventDebugState.Blocked;
            blockedReason = "PlayerInteractor cannot see Event collider";
            return;
        }

        state = EventDebugState.Ready;
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerInteractor = playerObject.GetComponent<PlayerInteractor>();
    }

    private void HandleEventCompleted(WorldEvent completedEvent)
    {
        completionVisibleUntil =
            Time.unscaledTime + completionDisplaySeconds;
        state = EventDebugState.Complete;
        blockedReason = string.Empty;
    }

    private void OnGUI()
    {
        if (!overlayVisible || state == EventDebugState.None)
            return;

        EnsureStyles();
        float width = 360f;
        float height = state == EventDebugState.Blocked ? 78f : 58f;
        Rect panel = new(
            Screen.width * 0.5f - width * 0.5f,
            Screen.height - height - 24f,
            width,
            height
        );
        Color previous = GUI.color;
        GUI.color = new Color(0.025f, 0.045f, 0.06f, 0.94f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 0.72f, 0.78f, 0.95f);
        DrawBorder(panel);
        GUI.color = previous;

        string headline = state switch
        {
            EventDebugState.Ready => "EVENT: READY    [E] START",
            EventDebugState.Running => "EVENT: RUNNING",
            EventDebugState.Complete => "EVENT: COMPLETE",
            _ => "EVENT: BLOCKED"
        };
        GUI.Label(
            new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 26f),
            headline,
            stateStyle
        );
        if (state == EventDebugState.Blocked)
        {
            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 37f,
                    panel.width - 24f, 24f),
                "Reason: " + blockedReason,
                detailStyle
            );
        }
    }

    private static void DrawBorder(Rect rect)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f),
            Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
            Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height),
            Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height),
            Texture2D.whiteTexture);
    }

    private void EnsureStyles()
    {
        if (stateStyle != null)
            return;

        stateStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.35f, 0.95f, 1f) }
        };
        detailStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            normal = { textColor = new Color(1f, 0.72f, 0.3f) }
        };
    }

    private void OnDestroy()
    {
        if (eventSpawner != null)
            eventSpawner.EventCompleted -= HandleEventCompleted;
    }
}
#endif
