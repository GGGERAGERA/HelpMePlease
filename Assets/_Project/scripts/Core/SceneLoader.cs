using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour, IPointerClickHandler
{
    public string gameSceneName = "MVP";

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Загрузка сцены: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadGameScene()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}