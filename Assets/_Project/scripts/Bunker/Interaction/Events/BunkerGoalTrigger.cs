using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BunkerGoalTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string ballTag = "Ball";

    [Header("Objects")]
    [Tooltip("Корневой объект мяча, который будет скрыт после гола.")]
    [SerializeField] private GameObject ballRoot;

    [Tooltip("Только визуальная часть ворот. Не назначай сюда объект Trigger.")]
    [SerializeField] private GameObject goalVisualRoot;

    [Header("Reward")]
    [SerializeField, Min(0)] private int minGoldReward = 100;
    [SerializeField, Min(0)] private int maxGoldReward = 200;

    [Header("Fullscreen Event")]
    [SerializeField] private Sprite fullscreenSprite;
    [SerializeField, Min(0f)] private float imageDuration = 5f;

    [Header("Respawn")]
    [SerializeField] private bool respawnAfterEvent;
    [SerializeField, Min(0f)] private float respawnDelay = 10f;

    private bool triggered;

    private BunkerEventManager Events =>
        BunkerContext.Instance != null
            ? BunkerContext.Instance.Events
            : null;

    private BunkerNotificationManager Notifications =>
        BunkerContext.Instance != null
            ? BunkerContext.Instance.Notifications
            : null;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (maxGoldReward < minGoldReward)
            maxGoldReward = minGoldReward;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || !other.CompareTag(ballTag))
            return;

        triggered = true;

        if (ballRoot == null)
            ballRoot = other.gameObject;

        AwardGold();

        Events?.ShowFullscreenImage(fullscreenSprite, imageDuration);

        if (ballRoot != null)
            ballRoot.SetActive(false);

        if (goalVisualRoot != null)
            goalVisualRoot.SetActive(false);

        if (respawnAfterEvent)
            StartCoroutine(RespawnRoutine());
    }

    private void AwardGold()
    {
        CurrencyManager currency = CurrencyManager.Instance;

        if (currency == null)
        {
            Debug.LogError("[BunkerGoalTrigger] CurrencyManager is missing.");
            Notifications?.ShowError("Не удалось начислить награду.");
            return;
        }

        int reward = Random.Range(minGoldReward, maxGoldReward + 1);

        currency.AddGold(reward);
        Notifications?.ShowSuccess($"+{reward} золота за гол!");
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSecondsRealtime(imageDuration + respawnDelay);

        if (ballRoot != null)
            ballRoot.SetActive(true);

        if (goalVisualRoot != null)
            goalVisualRoot.SetActive(true);

        triggered = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minGoldReward = Mathf.Max(0, minGoldReward);
        maxGoldReward = Mathf.Max(minGoldReward, maxGoldReward);
        imageDuration = Mathf.Max(0f, imageDuration);
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }
#endif
}