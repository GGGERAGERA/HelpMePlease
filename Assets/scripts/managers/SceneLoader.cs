using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    [Header("UI Элементы")]
    public Slider progressSlider;
    //public Text progressText;
    //public GameObject logo;
    private float smoothVelocity = 0f; // ← Обязательно!

    [Header("Настройки")]
    public float minLoadTime = 2.5f; // Мин. время анимации (чтобы не мелькало)
    public string nextSceneName = "LobbyScene";

    void Start()
    {
        StartCoroutine(LoadNextScene());
    }

    IEnumerator LoadNextScene()
    {
        // 1. Сначала показываем логотип
        //logo.SetActive(true);

        // 2. Начинаем асинхронную загрузку
        AsyncOperation operation = SceneManager.LoadSceneAsync(nextSceneName);
        operation.allowSceneActivation = false; // Не активируем сцену сразу

        // 3. Ждём мин. время (чтобы анимация не мелькала)
        yield return new WaitForSeconds(minLoadTime);

        // 4. Показываем прогресс загрузки
        float currentProgress = 0f;
        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            currentProgress = Mathf.SmoothDamp(currentProgress, targetProgress, ref smoothVelocity, 0.2f);
            progressSlider.value = currentProgress;
            //progressText.text = Mathf.Round(currentProgress * 100) + "%";
            yield return null;
        }

        // 5. Активируем сцену
        operation.allowSceneActivation = true;
    }
}