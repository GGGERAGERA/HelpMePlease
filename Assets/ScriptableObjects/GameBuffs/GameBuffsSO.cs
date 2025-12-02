using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "GameBuffs", menuName = "ScriptableObject/Buffses/GameBuffs")]
public class GameBuffsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("GameBuffs")]
    public GameObject GameBuffsPrefab; // Префаб персонажа для спавна
    //public GameObject BuffsPrefabDefaultWeapon; // Префаб персонажа для спавна

    
    [Header("GameBuffsStats")]
    public string GameBuffsName = "Buffs1";
    public int GameBuffsMaxHealth = 100;
    public float GameHPrecover = 0.6f;
    public float GameShield = 3;
    public float GameBuffsSpeed = 5f;

    public float Gamepower = 25f;
    public float GameprojectileSpeed = 20f;
    public float Gamerange = 11f;
    public float Gamereload = -5f;
    //public int Reanimate = 1;
    //public float magnet = 50f;
    //public float Luck = 32f;
    //public float upgrade = 15f;
    //public int skip = 3;


    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        GameBuffsPrefab = null;
        //BuffsName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return GameBuffsPrefab != null;
    }
}