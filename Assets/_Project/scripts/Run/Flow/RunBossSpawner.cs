using UnityEngine;

// Positioning and instantiation only; the run flow owns phase, delay and cancellation.
public sealed class RunBossSpawner : MonoBehaviour
{
    [SerializeField, Min(1f)] private float spawnDistanceFromPlayer = 25f;
    [SerializeField, Min(0f)] private float bossEdgePadding = 2f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 24;
    [SerializeField] private GameplayAreaService gameplayArea;

    public bool CanSpawn(RunSector sector)
    {
        if (gameplayArea == null || sector?.BossPrefab == null) return false;
        EnemyHealth health = sector.BossPrefab.GetComponent<EnemyHealth>();
        return health != null && health.IsBoss;
    }

    internal bool TrySpawn(RunSector sector, Transform player, out EnemyHealth boss)
    {
        boss = null;
        if (!CanSpawn(sector) || player == null) return false;
        if (!gameplayArea.TryGetSpawnPosition(player.position,
            spawnDistanceFromPlayer, spawnDistanceFromPlayer,
            spawnPositionAttempts, bossEdgePadding, out Vector3 position)) return false;
        boss = Instantiate(sector.BossPrefab, position, Quaternion.identity).GetComponent<EnemyHealth>();
        return true;
    }
}
