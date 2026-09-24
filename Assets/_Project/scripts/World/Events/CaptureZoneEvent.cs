using TMPro;
using UnityEngine;

public class CaptureZoneEvent : WorldEvent
{
    public event System.Action PlayerEntered;
    public float CaptureRadius => captureRadius;
    public float TimeRemaining => Mathf.Max(0f, requiredHoldTime - currentHoldTime);
    public float Progress => requiredHoldTime > 0f
        ? Mathf.Clamp01(currentHoldTime / requiredHoldTime)
        : 1f;
    public bool IsPlayerInside => playerInside;

    [Header("Capture Settings")]
    [SerializeField] private float requiredHoldTime = 30f;
    [SerializeField] private float captureRadius = 3f;
    [SerializeField] private bool resetProgressOnExit = false;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;

    private float currentHoldTime;
    private Transform player;
    private bool playerInside;
    private bool completionTriggered;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void ConfigureDebugHoldTime(float seconds)
    {
        requiredHoldTime = Mathf.Max(0.1f, seconds);
        currentHoldTime = 0f;
        UpdateUI();
    }
#endif

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        completionTriggered = false;

        ShowEventMarker(transform, "event.capture");
    }

    public override void ApplyDifficultyMultiplier(float multiplier)
    {
        requiredHoldTime *= Mathf.Max(1f, multiplier);
    }

    private void Start()
    {
        player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);

        currentHoldTime = 0f;
        playerInside = false;

        UpdateUI();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        UpdatePlayerInsideState();

        if (IsStarted && playerInside)
        {
            currentHoldTime += Time.deltaTime;

            if (currentHoldTime >= requiredHoldTime)
            {
                CompleteCapture();
                return;
            }
        }

        UpdateUI();
    }

    protected override bool CanStartFrom(Vector2 playerPosition)
    {
        return Vector2.Distance(transform.position, playerPosition) <=
            captureRadius;
    }

    protected override void OnEventStarted()
    {
        currentHoldTime = 0f;
        UpdateUI();
    }

    private void UpdatePlayerInsideState()
    {
        if (player == null)
        {
            player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);

            if (player == null)
            {
                playerInside = false;
                return;
            }
        }

        float distance = Vector2.Distance(transform.position, player.position);
        bool wasInside = playerInside;

        playerInside = distance <= captureRadius;
        if (!wasInside && playerInside) PlayerEntered?.Invoke();

        if (wasInside && !playerInside && resetProgressOnExit)
            currentHoldTime = 0f;
    }

    private void CompleteCapture()
    {
        if (completionTriggered)
            return;

        completionTriggered = true;
        CompleteEvent();
    }

    protected override void CleanupEvent()
    {
        if (!IsFailed)
            GetComponent<CaptureZoneVisual>()?.PlayCompletion();
    }

    private void UpdateUI()
    {
        float progress = Mathf.Clamp01(currentHoldTime / requiredHoldTime);
        float timeLeft = Mathf.Max(0f, requiredHoldTime - currentHoldTime);

        if (timerText != null)
            timerText.text = IsStarted &&
                (playerInside || currentHoldTime > 0f)
                ? $"{Mathf.CeilToInt(timeLeft)}s"
                : LocalizationService.EnsureExists().Get("event.enter");

    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}
