using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugSaveResetButton : MonoBehaviour
{
    public void ResetSave()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
