using UnityEngine;
using System.Collections.Generic;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "Projectile", menuName = "ScriptableObject/Projectiles/Projectile")]
public class ProjectileSO : ScriptableObject
{
    
    public enum EProjectileType {Direct, Homing, Melee}; 
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!

    [Header("Projectile")]
    public GameObject WeaponPrefab1; // Префаб оружия
    public GameObject ProjectileProjectilePrefab1; // Префаб проджектайла

    [Header("ProjectileStats")]
    //enum EProjectileType {Direct, Homing, Melee};
    public EProjectileType projectileType = EProjectileType.Homing;
    public string ProjectileName = "Projectile1";
    public float ProjectileRange = 5f;
    public int ProjectileDamage = 10;
    public float ProjectileReload = -5f;
    public float ProjectileBurstTime = 5f;
    public int ProjectileSpawnPoolCount = 30;
    public float ProjectileSpeed = 1f;


    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        WeaponPrefab1 = null;
        //ProjectileName = "Not Selected";
    }

    // Метод для проверки, выбрано ли оружие;
    public bool HasSelection()
    {
        return WeaponPrefab1 != null;
    }
}