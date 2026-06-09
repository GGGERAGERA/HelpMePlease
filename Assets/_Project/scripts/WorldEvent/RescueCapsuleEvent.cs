using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RescueCapsuleEvent : WorldEvent
{
    [Header("Defense Settings")]
    [SerializeField] private float activationRadius = 2.5f;
    [SerializeField] private float defenseTime = 45f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider progressSlider;

    [Header("Reward")]
    [SerializeField] private bool giveUpgradeChoice = true;

    private Transform player;
    private bool activated;
    private float timer;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);

        HUDManager.Instance?.ShowBossText("RESCUE CAPSULE DETECTED", 3f);
        HUDManager.Instance?.ShowWorldEventMarker(transform, "CAPSULE");
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

        if (!activated)
        {
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= activationRadius)
                Activate();

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

    private void Activate()
    {
        activated = true;
        timer = 0f;

        HUDManager.Instance?.ShowBossText("DEFEND THE CAPSULE", 3f);
    }

    private void CompleteCapsule()
    {
        HUDManager.Instance?.HideWorldEventMarker();

        if (giveUpgradeChoice && UpgradeManager.Instance != null)
            UpgradeManager.Instance.ShowUpgradeChoices();

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
        HUDManager.Instance?.HideWorldEventMarker();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}