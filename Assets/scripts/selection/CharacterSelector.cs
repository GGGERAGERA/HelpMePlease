using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


public class CharacterSelector : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private PlayerSelectionSO playerSelectionSO; // Ссылка на наш SO
    //[SerializeField] private GameObject playerPrefab; // Какой префаб выбирает эта кнопка
    
    //[Header("UI")]
    [SerializeField] public Button selectButton;
    

    
    public void SelectCharacter(GameObject playerPrefab)
    {
        // Сохраняем выбранный префаб в ScriptableObject
        playerSelectionSO.selectedPlayerPrefab = playerPrefab;

        if (playerSelectionSO == null)
        {
            Debug.LogError("PlayerSelectionSO не назначен!");
            return;
        }
        
        // Можно сохранить и другие данные
        //playerSelectionSO.playerName = playerPrefab.name;
        
        Debug.Log($"Выбран персонаж: {playerPrefab.name}");
        
        // Здесь можно добавить визуальную обратную связь
        // Например, подсветку выбранной кнопки
    }
}

