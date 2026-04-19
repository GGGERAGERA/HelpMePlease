using UnityEngine;

public class SceneLoader : MonoBehaviour
{
    public string gameSceneName = "MVP";
    
    public void LoadGameScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }
}
