using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "Enemy", menuName = "ScriptableObject/Enemys/Enemy")]
public class EnemySO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Enemy")]
    public GameObject EnemyPrefab; // Префаб персонажа для спавна
    public GameObject EnemyPrefabDefaultWeapon; // Префаб персонажа для спавна
    public enum EEnemyType { Basic, Boss, Flying }
    public EEnemyType enemyType = EEnemyType.Basic;
    
    [Header("EnemyStats")]
    public string EnemyName = "Enemy1";
    public int EnemyMaxHealth = 100;
    //public float HPrecover = 0.6f;
    //public float Shield = 3;
    public float EnemySpeed = 5f;

    public int EnemyPower = 5;
    public int projectileSpeed = 0;
    public float range = 11f;
    public float reload = 0.5f;
    //public int Reanimate = 1;
    //public float magnet = 50f;
    //public float Luck = 32f;
    //public float upgrade = 15f;
    //public int skip = 3;


    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        EnemyPrefab = null;
        //EnemyName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return EnemyPrefab != null;
    }
}