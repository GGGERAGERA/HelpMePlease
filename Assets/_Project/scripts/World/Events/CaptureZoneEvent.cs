using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaptureZoneEvent : WorldEvent
{
    protected override string StartDisplayName => "CAPTURE ZONE";
    protected override Color StartAccentColor =>
        new Color(0.15f, 0.82f, 1f, 1f);

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
    [SerializeField] private Slider progressSlider;

    [Header("Feedback")]
    [SerializeField, Min(0.1f)] private float feedbackPulseDuration = 0.38f;
    [SerializeField, Min(0f)] private float exitShakeDuration = 0.14f;
    [SerializeField, Min(0f)] private float exitShakeMagnitude = 0.04f;
    [SerializeField] private Color exitPulseColor =
        new(0.95f, 0.07f, 0.03f, 1f);
    [SerializeField] private Color completionPulseColor =
        new(0.08f, 0.92f, 1f, 1f);

    private float currentHoldTime;
    private Transform player;
    private bool playerInside;
    private bool completionTriggered;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        completionTriggered = false;

        ShowEventMarker(transform, "CAPTURE");
    }

    public override void ApplyDifficultyMultiplier(float multiplier)
    {
        requiredHoldTime *= Mathf.Max(1f, multiplier);
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

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
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject == null)
            {
                playerInside = false;
                return;
            }

            player = playerObject.transform;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        bool wasInside = playerInside;

        playerInside = distance <= captureRadius;

        if (IsStarted && wasInside && !playerInside)
        {
            RunMessageService.Instance?.ShowWorldEventFeedback(
                "ЗОНА ПОКИНУТА",
                "ВЕРНИТЕСЬ ДЛЯ ПРОДОЛЖЕНИЯ ЗАХВАТА",
                exitPulseColor,
                feedbackPulseDuration
            );
            CameraShake.Instance?.Shake(
                exitShakeDuration,
                exitShakeMagnitude
            );
        }

        if (wasInside && !playerInside && resetProgressOnExit)
            currentHoldTime = 0f;
    }

    private void CompleteCapture()
    {
        if (completionTriggered)
            return;

        completionTriggered = true;
        RunMessageService.Instance?.ShowWorldEventFeedback(
            "ЗОНА ЗАХВАЧЕНА",
            string.Empty,
            completionPulseColor,
            feedbackPulseDuration
        );
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
                : "ENTER";

        if (progressSlider != null)
            progressSlider.value = progress;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}
