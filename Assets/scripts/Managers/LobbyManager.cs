using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public SceneAsset LobbyScene;
    public SceneAsset LoadingScene;
    [SerializeField] public SceneSelectionSO GlobalSceneSelectionSO; // Ссылка на наш SO
    void Start()
    {
        Debug.Log("Lobby scene loaded");
    }

    public void OnLevelClicked()
    {
        Debug.Log("Starting Level...");
        SceneLoader.LoadLevel(GlobalSceneSelectionSO.selectedScene, GlobalSceneSelectionSO, LoadingScene);
    }

    public void OnLobbyClicked()
    {
        Debug.Log("Starting Level...");
        SceneLoader.LoadLobby(LobbyScene, GlobalSceneSelectionSO, LoadingScene);
    }
    
    public void OnExitClicked()
    {
        Debug.Log("Exiting game...");
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
