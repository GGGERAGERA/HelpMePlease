using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerBuffs", menuName = "ScriptableObject/Buffses/PlayerBuffs")]
public class PlayerBuffsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Buffs")]
    public GameObject PlayerBuffsPrefab; // Префаб персонажа для спавна
    public GameObject PlayerBuffsPrefabDefaultWeapon; // Префаб персонажа для спавна

    
    [Header("BuffsStats")]
    public string PlayerBuffsName = "Buffs1";
    public int PlayerBuffsMaxHealth = 100;
    public float PlayerHPrecover = 0.6f;
    public float PlayerShield = 3;
    public float PlayerBuffsSpeed = 5f;

    public float PlayerPower = 25f;
    public float PlayerProjectileSpeed = 20f;
    public float PlayeRrange = 11f;
    public float PlayerReload = -5f;
    //public int Reanimate = 1;
    //public float magnet = 50f;
    //public float Luck = 32f;
    //public float upgrade = 15f;
    //public int skip = 3;


    
    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        PlayerBuffsPrefab = null;
        //BuffsName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return PlayerBuffsPrefab != null;
    }
}