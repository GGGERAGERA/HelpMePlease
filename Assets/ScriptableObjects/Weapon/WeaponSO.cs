using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "Weapon", menuName = "ScriptableObject/Weapons/Weapon")]
public class WeaponSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!

    [Header("Weapon")]
    public GameObject WeaponPrefab; // Префаб оружия
    public GameObject WeaponProjectilePrefab; // Префаб проджектайла


    [Header("WeaponStats")]
    public string WeaponName = "Weapon1";
    public float WeaponRange = 5f;
    public int WeaponDamage = 26;
    public float WeaponReload = 0.5f;
    public float WeaponBurstTime = 5f;
    public float FireRate = 1f;

    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        WeaponPrefab = null;
        //WeaponName = "Not Selected";
    }

    // Метод для проверки, выбрано ли оружие;
    public bool HasSelection()
    {
        return WeaponPrefab != null;
    }
}