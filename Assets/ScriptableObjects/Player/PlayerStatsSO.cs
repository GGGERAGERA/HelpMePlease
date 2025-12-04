using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerStats", menuName = "ScriptableObject/Player/PlayerStats")]
public class PlayerStatsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Player")]
    public GameObject PlayerPrefab; // Префаб персонажа для спавна
    
    public GameObject PlayerPrefabDefaultWeapon; // Префаб дефолтного оружия для спавна!

    
    [Header("PlayerStats")]
    public string playerName = "Hero1";
    public int playerMaxHealth = 100;
    public float HPrecover = 0.6f;
    public float Shield = 3;
    public float playerSpeed = 5f;
    public int playerWeight = 100;

    public int playerpower = 25;
    public float playerprojectileSpeed = 20f;
    public float playerrange = 11f;
    public float playerreload = -5f;
    public int playerReanimate = 1;
    public float playermagnet = 50f;
    public float playerLuck = 32f;
    public float playerupgrade = 15f;
    public int playerskip = 3;


    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        PlayerPrefab = null;
        //playerName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return PlayerPrefab != null;
    }
}