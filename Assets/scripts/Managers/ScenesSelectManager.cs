using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NUnit.Framework;

public class ScenesSelectManager : MonoBehaviour
{
    //[Header("Настройки")]
    [SerializeField] private SceneSelectManagerSO GlobalSceneSelectionSO; // Ссылка на наш SO
    private const string NEXT_SCENE_KEY = "NextSceneToLoad";
    //[SerializeField] private GameObject ScenePrefab; // Какой префаб выбирает эта кнопка

    [Header("UI")]
    [SerializeField] public List<Button> AllScenesBtns;
    [SerializeField] public List<Button> OpenedScenesBtns;

    void Start()
    {
        int i = 0;
        for (i = 0; i < OpenedScenesBtns.Count; i++ )
        {
            if (i<GlobalSceneSelectionSO.openedScenes.Count)
            {
            Debug.Log($"Button {i} Active ");
            OpenedScenesBtns[i].GetComponent<CanvasGroup>().interactable = true;
            OpenedScenesBtns[i].GetComponent<CanvasGroup>().blocksRaycasts = true;
            }
            else
            {
            Debug.Log($"Button {i} Is not Active");
            OpenedScenesBtns[i].GetComponent<CanvasGroup>().interactable = false;
            OpenedScenesBtns[i].GetComponent<CanvasGroup>().blocksRaycasts = false;
            }
        }
    }



    public void SelectScene(SceneAsset Scene)
    {
        bool select = false;
        foreach (var OS in GlobalSceneSelectionSO.openedScenes)
        {
            if (OS == Scene)
            {
                select = true;
            }
        }

        if (select==true)
        {
        // Сохраняем выбранный sceneAsset в ScriptableObject
        GlobalSceneSelectionSO.selectedScene = Scene;
        //PlayerPrefs.SetString(NEXT_SCENE_KEY, Scene.name);

            if (Scene == null)
            {
            Debug.LogError("SceneSelectionSO не назначен!");
            return;
            }

        // Можно сохранить и другие данные
        //SceneSelectionSO.SceneName = ScenePrefab.name;
        Debug.Log($"Scene Chosen: {Scene.name}");
        }
        else
        Debug.Log($"Scene Not Opened Yet: {Scene.name}");
        // Здесь можно добавить визуальную обратную связь
        // Например, подсветку выбранной кнопки
    }
}
