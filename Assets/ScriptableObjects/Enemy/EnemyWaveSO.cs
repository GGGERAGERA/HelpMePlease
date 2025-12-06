using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "EnemySpawn", menuName = "ScriptableObject/EnemySpawns/EnemySpawn")]
public class EnemyWaveSO : ScriptableObject
{
    [System.Serializable]
    public class EnemyGroup
    {
        public EnemySO enemyType;
        public int count;
        public float spawnDelay; // между спавнами
    }

    //public List<EnemyGroup> enemies;
    public float waveDelay; // задержка перед волной

    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        //EnemySpawnPrefab = null;
        //EnemySpawnName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public void HasSelection()
    {
        //return EnemySpawnPrefab != null;
    }
}