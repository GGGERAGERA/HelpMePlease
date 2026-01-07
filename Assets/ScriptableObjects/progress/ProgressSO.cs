using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SearchService;
using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "Progress", menuName = "ScriptableObject/Progresses/Progress")]
public class ProgressSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Выбранная сцена")]
    public SceneAsset selectedScene; // Scene для выбора
    [Header("Выбранный персонаж")]
    public GameObject SelectedPlayer;
    

    public List<SceneAsset> openedScenes;
    public List<SceneAsset> openedPlayers; 
    public List<SceneAsset> openedCards; 
    
    public float SpeedBonus = 0.5f;
    public int HealthBonus= 50;
    public int ShieldBonus= 0;
    public int DamageBonus= 3;
    public int MoneyBonus= 1;

    // Метод для сброса выбора (опционально)
    public void ClearSelection()
    {
        selectedScene = null;
    }
    
    // Метод для проверки, выбрана ли сцена
    public bool HasSelection()
    {
        return selectedScene != null;
    }
}