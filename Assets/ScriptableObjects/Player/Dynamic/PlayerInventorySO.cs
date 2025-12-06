using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInventory", menuName = "ScriptableObject/PlayerInventory/PlayerDinamicInventory")]
public class PlayerInventorySO : ScriptableObject
{

    [Header("Weapons")]
    public List<GameObject> InventoryWeapons; // Префаб подобранных оружий

    [Header("Buffs")]
    public List<GameObject> InventoryBuffs; // Префаб подобранных баффов
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    //[SerializeField] private PlayerStatsSO PlayerStatsSO;
    //[SerializeField] private GlobalPleerPercsSO GlobalPlayerPercsSO;


    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
