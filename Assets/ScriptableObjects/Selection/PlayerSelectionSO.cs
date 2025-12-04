using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerSelection", menuName = "ScriptableObject/Selection/Player Selection")]
public class PlayerSelectionSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Выбранный персонаж")]
    public GameObject selectedPlayerPrefab; // Префаб персонажа для спавна

    [Header("Статы игрока")]
    public PlayerStatsSO selectedPlayerStats; // ← ДОБАВЬ ЭТО ПОЛЕ!

    [Header("Иконка для выбора")]
    public Sprite playerIcon;
    
    //[Header("Дополнительные данные")]
    //public string playerName = "Hero";
    //public int playerMaxHealth = 100;
    //public float playerSpeed = 5f;
}