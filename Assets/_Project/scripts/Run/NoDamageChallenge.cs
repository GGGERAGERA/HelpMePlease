using UnityEngine;

public sealed class NoDamageChallenge : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float duration = 30f;

    public event System.Action Completed;
    public event System.Action Failed;
    public event System.Action Canceled;

    public bool IsRunning { get; private set; }
    public float TimeRemaining { get; private set; }

    private PlayerHealth playerHealth;

    public void StartChallenge()
    {
        if (IsRunning)
            return;

        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();

        if (player == null || player.IsDead)
        {
            Debug.LogWarning(
                "[NoDamageChallenge] Cannot start without a living player."
            );
            return;
        }

        playerHealth = player;
        playerHealth.DamageTaken += HandleDamageTaken;
        TimeRemaining = Mathf.Max(0.1f, duration);
        IsRunning = true;
    }

    public void CancelChallenge()
    {
        if (!IsRunning)
            return;

        StopTrackingPlayer();
        IsRunning = false;
        TimeRemaining = 0f;
        Canceled?.Invoke();
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        if (playerHealth == null ||
            (RunStateManager.Instance != null && RunStateManager.Instance.IsRunEnded))
        {
            CancelChallenge();
            return;
        }

        TimeRemaining -= Time.deltaTime;

        if (TimeRemaining <= 0f)
            CompleteChallenge();
    }

    private void HandleDamageTaken()
    {
        if (!IsRunning)
            return;

        if (playerHealth == null || playerHealth.IsDead)
        {
            CancelChallenge();
            return;
        }

        StopTrackingPlayer();
        IsRunning = false;
        TimeRemaining = 0f;
        Failed?.Invoke();
    }

    private void CompleteChallenge()
    {
        StopTrackingPlayer();
        IsRunning = false;
        TimeRemaining = 0f;
        Completed?.Invoke();
    }

    private void StopTrackingPlayer()
    {
        if (playerHealth != null)
            playerHealth.DamageTaken -= HandleDamageTaken;

        playerHealth = null;
    }

    private void OnDisable()
    {
        CancelChallenge();
    }
}
