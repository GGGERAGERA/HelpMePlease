using UnityEngine;

public sealed class WorldAccelerationRule : MonoBehaviour
{
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField, Min(0.1f)] private float duration = 5f;
    [SerializeField, Min(1f)] private float accelerationMultiplier = 1.5f;

    public bool IsRunning { get; private set; }
    public float TimeRemaining { get; private set; }

    public void StartRule()
    {
        if (IsRunning)
            return;

        if (enemySpawner == null || !enemySpawner.gameObject.scene.IsValid())
            enemySpawner = FindFirstObjectByType<EnemySpawner>();

        if (enemySpawner == null)
        {
            Debug.LogWarning(
                "[WorldAccelerationRule] Cannot start without EnemySpawner."
            );
            return;
        }

        TimeRemaining = Mathf.Max(0.1f, duration);
        enemySpawner.SetWorldAcceleration(
            Mathf.Max(1f, accelerationMultiplier)
        );
        IsRunning = true;
    }

    public void StopRule()
    {
        if (!IsRunning)
            return;

        if (enemySpawner != null)
            enemySpawner.SetWorldAcceleration(1f);

        IsRunning = false;
        TimeRemaining = 0f;
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        TimeRemaining -= Time.deltaTime;

        if (TimeRemaining <= 0f)
            StopRule();
    }

    private void OnDisable()
    {
        StopRule();
    }
}
