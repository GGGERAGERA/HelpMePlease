using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "EnemySpawn", menuName = "ScriptableObject/EnemySpawns/EnemySpawn")]
public class EnemySpawnSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("EnemySpawn")]
    public GameObject EnemySpawnPrefab; // Префаб персонажа для спавна

    
    [Header("EnemySpawnStats")]
    public string EnemySpawnName = "EnemySpawn1";
    public int EnemySpawnMaxHealth = 100;
    public float HPrecover = 0.6f;
    public float Shield = 3;
    public float EnemySpawnSpeed = 5f;

    public float power = 25f;
    public float projectileSpeed = 20f;
    public float range = 11f;
    public float reload = -5f;
    //public int Reanimate = 1;
    //public float magnet = 50f;
    //public float Luck = 32f;
    //public float upgrade = 15f;
    //public int skip = 3;


    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        EnemySpawnPrefab = null;
        //EnemySpawnName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return EnemySpawnPrefab != null;
    }
}