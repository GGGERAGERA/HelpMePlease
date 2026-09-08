using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugSaveResetButton : MonoBehaviour
{
    public void ResetSave()
    {
        SceneTransitionOverlay.Load(SceneManager.GetActiveScene().name, () =>
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

        });
    }
}
