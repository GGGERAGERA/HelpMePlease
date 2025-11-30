using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Lobby scene loaded");
    }

    public void OnLevel1Clicked()
    {
        Debug.Log("Starting Level 1...");
        SceneLoader.LoadLevel1();
    }
    
    public void OnLevel2Clicked()
    {
        Debug.Log("Starting Level 2...");
        SceneLoader.LoadLevel2();
    }
    
    public void OnLevel3Clicked()
    {
        Debug.Log("Starting Level 3...");
        SceneLoader.LoadLevel3();
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
