using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "Buffs", menuName = "ScriptableObject/Buffses/Buffs")]
public class BuffsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Buffs")]
    public GameObject BuffsPrefab; // Префаб персонажа для спавна
    public GameObject BuffsPrefabDefaultWeapon; // Префаб персонажа для спавна

    
    [Header("BuffsStats")]
    public string BuffsName = "Buffs1";
    public int BuffsMaxHealth = 100;
    public float HPrecover = 0.6f;
    public float Shield = 3;
    public float BuffsSpeed = 5f;

    public float power = 25f;
    public float projectileSpeed = 20f;
    public float range = 11f;
    public float reload = -5f;
    //public int Reanimate = 1;
    //public float magnet = 50f;
    //public float Luck = 32f;
    //public float upgrade = 15f;
    //public int skip = 3;


    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        BuffsPrefab = null;
        //BuffsName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return BuffsPrefab != null;
    }
}