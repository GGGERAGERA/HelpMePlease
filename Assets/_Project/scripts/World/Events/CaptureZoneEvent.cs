using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaptureZoneEvent : WorldEvent
{
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

    [Header("Reward")]
    [SerializeField] private bool giveUpgradeChoice = true;

    private float currentHoldTime;
    private Transform player;
    private bool playerInside;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);

        HUDManager.Instance?.ShowWorldEventMarker(transform, "CAPTURE");
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

        if (playerInside)
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

        if (wasInside && !playerInside && resetProgressOnExit)
            currentHoldTime = 0f;
    }

    private void CompleteCapture()
    {
        HUDManager.Instance?.HideWorldEventMarker();

        if (giveUpgradeChoice && UpgradeManager.Instance != null)
            UpgradeManager.Instance.ShowUpgradeChoices();

        CompleteEvent();
    }

    private void UpdateUI()
    {
        float progress = Mathf.Clamp01(currentHoldTime / requiredHoldTime);
        float timeLeft = Mathf.Max(0f, requiredHoldTime - currentHoldTime);

        if (timerText != null)
            timerText.text = playerInside || currentHoldTime > 0f
                ? $"{Mathf.CeilToInt(timeLeft)}s"
                : "ENTER";

        if (progressSlider != null)
            progressSlider.value = progress;
    }

    private void OnDestroy()
    {
        HUDManager.Instance?.HideWorldEventMarker();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}
