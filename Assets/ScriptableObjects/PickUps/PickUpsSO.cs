using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PickUps", menuName = "ScriptableObject/PickUpses/PickUps")]
public class PickUpsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("PickUps")]
    public GameObject PickUpsPrefab; // Префаб персонажа для спавна
    public GameObject PickUpsPrefabDefaultWeapon; // Префаб персонажа для спавна

    
    [Header("PickUpsStats")]
    public string PickUpsName = "PickUps1";
    public int PickUpsMaxHealth = 100;
    public float HPrecover = 0.6f;
    public float Shield = 3;
    public float PickUpsSpeed = 5f;

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
        PickUpsPrefab = null;
        //PickUpsName = "Not Selected";
    }
    
    // Метод для проверки, выбран ли персонаж
    public bool HasSelection()
    {
        return PickUpsPrefab != null;
    }
}