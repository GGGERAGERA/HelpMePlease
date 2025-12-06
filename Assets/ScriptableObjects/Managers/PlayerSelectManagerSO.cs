using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerSelection", menuName = "ScriptableObject/Selection/Player Selection")]
public class PlayerSelectManagerSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!

    [Header("Выбранный персонаж")]
    public GameObject selectedPlayerPrefab; // Префаб персонажа для спавна

    [Header("Иконка для выбора")]
    public Sprite playerIcon;
}