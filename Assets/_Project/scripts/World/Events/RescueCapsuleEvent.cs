using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RescueCapsuleEvent : WorldEvent
{
    public float TimeRemaining => Mathf.Max(0f, defenseTime - timer);
    public float Progress => defenseTime > 0f
        ? Mathf.Clamp01(timer / defenseTime)
        : 1f;
    public bool IsActivated => activated;

    [Header("Defense Settings")]
    [SerializeField] private float activationRadius = 2.5f;
    [SerializeField] private float defenseTime = 45f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider progressSlider;

    private Transform player;
    private bool activated;
    private float timer;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);

        HUDManager.Instance?.ShowWorldEventMarker(transform, "CAPSULE");
    }

    public override void ApplyDifficultyMultiplier(float multiplier)
    {
        defenseTime *= Mathf.Max(1f, multiplier);
    }

    private void Start()
    {
        FindPlayer();
        UpdateUI();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        if (!IsStarted)
        {
            UpdateUI();
            return;
        }

        timer += Time.deltaTime;

        if (timer >= defenseTime)
        {
            CompleteCapsule();
            return;
        }

        UpdateUI();
    }

    protected override bool CanStartFrom(Vector2 playerPosition)
    {
        return Vector2.Distance(transform.position, playerPosition) <=
            activationRadius;
    }

    protected override void OnEventStarted()
    {
        Activate();
        UpdateUI();
    }

    private void Activate()
    {
        activated = true;
        timer = 0f;
    }

    private void CompleteCapsule()
    {
        HUDManager.Instance?.HideWorldEventMarker();

        CompleteEvent();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void UpdateUI()
    {
        if (timerText != null)
        {
            if (!activated)
                timerText.text = "ENTER";
            else
                timerText.text = $"{Mathf.CeilToInt(defenseTime - timer)}s";
        }

        if (progressSlider != null)
        {
            progressSlider.value = activated
                ? Mathf.Clamp01(timer / defenseTime)
                : 0f;
        }
    }

    private void OnDestroy()
    {
        FailEvent();
        HUDManager.Instance?.HideWorldEventMarker();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}
