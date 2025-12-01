using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerSelection", menuName = "ScriptableObject/Selection/Player Selection")]
public class PlayerSelectionSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Выбранный персонаж")]
    public GameObject selectedPlayerPrefab; // Префаб персонажа для спавна
    
    //[Header("Дополнительные данные")]
    //public string playerName = "Hero";
    //public int playerMaxHealth = 100;
    //public float playerSpeed = 5f;
    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        selectedPlayerPrefab = null;
        //playerName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return selectedPlayerPrefab != null;
    }
}