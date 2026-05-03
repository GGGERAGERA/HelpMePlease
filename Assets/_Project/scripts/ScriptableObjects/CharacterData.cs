using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Game/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("Basic Info")]
    public string characterName;
    public string description;
    public Sprite portrait;

    [Header("Prefabs")]
    public GameObject characterPrefab;
    public WeaponData startingWeapon;  // ← тип WeaponData, а не GameObject!

    [Header("Stats")]
    public int damage = 10;
    public float maxHealth = 100f;
    public float moveSpeed = 5f;

    // Для UI отображения в CharacterSelectButton (можно удалить, если используешь moveSpeed)
    public float speed => moveSpeed;  // ← свойство-прокси (не создаёт новое поле)
    public float health => maxHealth; // ← тоже полезно для UI       
}