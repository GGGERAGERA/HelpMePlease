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
    
    [Header("Выбранный персонаж")]
    public SceneAsset selectedScene; // Scene для выбора

    public List<SceneAsset> openedScenes;
    public List<SceneAsset> openedPlayers; 
    public List<SceneAsset> openedCards; 
    
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