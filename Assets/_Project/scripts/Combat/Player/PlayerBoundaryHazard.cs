using System;
using UnityEngine;

public sealed class PlayerBoundaryHazard : MonoBehaviour
{
    private const string WarningTitle = "ВНИМАНИЕ";
    private const string WarningDescription =
        "ВЫ ПОКИДАЕТЕ ЗОНУ ЭКСПЕРИМЕНТА";

    [Header("Scene References")]
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private BoundaryHazardView view;

    [Header("Warning")]
    [SerializeField] private AudioClip warningSound;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.75f;
    [SerializeField, Min(0.1f)] private float warningDuration = 3f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float gracePeriod = 1.5f;
    [SerializeField, Min(0.51f)] private float damageTickInterval = 0.6f;
    [SerializeField, Min(0f)] private float firstDamagePerSecond = 5f;
    [SerializeField, Min(0f)] private float secondStageTime = 4f;
    [SerializeField, Min(0f)] private float secondDamagePerSecond = 12f;
    [SerializeField, Min(0f)] private float thirdStageTime = 8f;
    [SerializeField, Min(0f)] private float thirdDamagePerSecond = 25f;

    private Transform player;
    private PlayerHealth playerHealth;
    private float outsideTime;
    private float damageTimer;
    private bool warningShown;

    public event Action<bool> OutsideStateChanged;
    public bool IsOutside { get; private set; }

    private void Awake()
    {
        ResolveGameplayArea();
        FindPlayer();
        view?.Hide();
    }

    private void Update()
    {
        if (player == null || playerHealth == null)
        {
            FindPlayer();

            if (player == null || playerHealth == null)
                return;
        }

        if (gameplayArea == null)
            ResolveGameplayArea();

        if (gameplayArea == null)
            return;

        if (gameplayArea.IsInsidePlayableArea(player.position))
        {
            SetOutsideState(false);
            ResetHazard();
            return;
        }

        SetOutsideState(true);

        if (!warningShown)
            ShowWarning();

        if (Time.timeScale <= 0f || playerHealth.IsDead)
            return;

        outsideTime += Time.deltaTime;
        view?.SetOutsideDuration(outsideTime);

        if (outsideTime < gracePeriod)
            return;

        damageTimer += Time.deltaTime;

        if (damageTimer < damageTickInterval)
            return;

        damageTimer -= damageTickInterval;
        float damage = GetDamagePerSecond(outsideTime) * damageTickInterval;
        playerHealth.TakeDamage(damage, Vector2.zero);
    }

    private void ShowWarning()
    {
        warningShown = true;
        RunMessageService.Instance?.ShowCustom(
            WarningTitle,
            WarningDescription,
            warningDuration
        );

        if (warningSound != null)
        {
            AudioService.Instance?.PlayExternalOneShot(
                warningSound,
                player != null ? player.position : transform.position,
                warningVolume,
                AudioCategory.UI
            );
        }

        view?.SetOutsideDuration(0f);
    }

    private float GetDamagePerSecond(float duration)
    {
        if (duration >= thirdStageTime)
            return thirdDamagePerSecond;

        if (duration >= secondStageTime)
            return secondDamagePerSecond;

        return firstDamagePerSecond;
    }

    private void ResetHazard()
    {
        if (!warningShown && outsideTime <= 0f)
            return;

        outsideTime = 0f;
        damageTimer = 0f;
        warningShown = false;
        view?.Hide();
    }

    private void SetOutsideState(bool isOutside)
    {
        if (IsOutside == isOutside)
            return;

        IsOutside = isOutside;
        OutsideStateChanged?.Invoke(IsOutside);
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
    }

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }
}
