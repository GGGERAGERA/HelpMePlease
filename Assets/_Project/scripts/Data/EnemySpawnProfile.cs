using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemySpawnProfile",
    menuName = "Game/Levels/Enemy Spawn Profile"
)]
public sealed class EnemySpawnProfile : ScriptableObject
{
    [SerializeField] private EnemySpawnPhase[] phases = Array.Empty<EnemySpawnPhase>();

    public EnemySpawnPhase[] Phases => phases;
}

[Serializable]
public sealed class EnemySpawnPhase
{
    [Min(0f)] public float startTime;
    [Min(0.1f)] public float spawnInterval = 1.5f;
    [Min(1)] public int maxAlive = 30;
    [Min(0.1f)] public float healthMultiplier = 1f;
    [Min(0.1f)] public float speedMultiplier = 1f;
    public EnemySpawnEntry[] enemies = Array.Empty<EnemySpawnEntry>();
}

[Serializable]
public sealed class EnemySpawnEntry
{
    public GameObject enemyPrefab;
    [Min(0f)] public float weight = 1f;
    [Tooltip("0 means no per-type limit.")]
    [Min(0)] public int maxAliveOfType;
    [Min(1)] public int minimumRunLevel = 1;
}
