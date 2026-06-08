using TMPro;
using UnityEngine;

public class CaptureZoneEvent : WorldEvent
{
    [Header("Capture Settings")]
    [SerializeField] private float requiredHoldTime = 30f;
    [SerializeField] private bool resetProgressOnExit = false;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject visualRoot;

    [Header("Reward")]
    [SerializeField] private bool giveUpgradeChoice = true;

    private float currentHoldTime;
    private bool playerInside;

    private void Start()
    {
        playerInside = false;
        currentHoldTime = 0f;
        UpdateTimerText();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (!playerInside)
        {
            UpdateTimerText();
            return;
        }

        currentHoldTime += Time.deltaTime;
        UpdateTimerText();

        if (currentHoldTime >= requiredHoldTime)
            CompleteCapture();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = false;

        if (resetProgressOnExit)
            currentHoldTime = 0f;

        UpdateTimerText();
    }

    private void CompleteCapture()
    {
        if (giveUpgradeChoice && UpgradeManager.Instance != null)
            UpgradeManager.Instance.ShowUpgradeChoices();

        CompleteEvent();
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
            return;

        if (!playerInside && currentHoldTime <= 0f)
        {
            timerText.text = "ENTER";
            return;
        }

        float timeLeft = Mathf.Max(0f, requiredHoldTime - currentHoldTime);
        timerText.text = $"{Mathf.CeilToInt(timeLeft)}s";
    }
}