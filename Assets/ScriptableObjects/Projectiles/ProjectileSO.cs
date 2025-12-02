using UnityEngine;


// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "Projectile", menuName = "ScriptableObject/Projectiles/Projectile")]
public class ProjectileSO : ScriptableObject
{
        public enum EProjectileType
    {
       Direct,    // летит прямо
       Homing,    // наводится на цель
       Melee      // круговая атака (спавнится у игрока, уничтожается через 0.1 сек)
    }

    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!

    [Header("Projectile")]
    public GameObject ProjectilePrefab; // Префаб оружия
    public GameObject ProjectileProjectilePrefab; // Префаб проджектайла

    [Header("ProjectileStats")]
    public EProjectileType projectileType = EProjectileType.Homing;
    public string ProjectileName = "Projectile1";
    public float ProjectileRange = 5f;
    public float ProjectileDamage = 100f;
    public float ProjectileReload = -5f;
    public float ProjectileBurstTime = 5f;

    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        ProjectilePrefab = null;
        //ProjectileName = "Not Selected";
    }

    // Метод для проверки, выбрано ли оружие;
    public bool HasSelection()
    {
        return ProjectilePrefab != null;
    }
}