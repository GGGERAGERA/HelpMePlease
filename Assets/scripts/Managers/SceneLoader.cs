using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [Header("Loading Settings")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private float minLoadTime = 2f; // Минимальное время анимации
    
    private AsyncOperation loadingOperation;
    private float loadingProgress;
    private float loadingTime;
    private bool isLoadingComplete = false;
    
    // Ключ для сохранения имени следующей сцены
    private const string NEXT_SCENE_KEY = "NextSceneToLoad";
    
    void Start()
    {
        // Запускаем процесс загрузки
        StartCoroutine(LoadTargetScene());
    }
    
    IEnumerator LoadTargetScene()
    {
        // Получаем имя сцены для загрузки
        string targetSceneName = GetTargetSceneName();
        Debug.Log($"Loading scene: {targetSceneName}");
        
        // Ждём немного перед началом загрузки (для стабильности)
        yield return new WaitForSeconds(0.1f);
        
        // Начинаем загрузку асинхронно
        loadingOperation = SceneManager.LoadSceneAsync(targetSceneName);
        loadingOperation.allowSceneActivation = false; // Не переключаем сцену сразу
        
        // Сбрасываем таймеры
        loadingTime = 0f;
        loadingProgress = 0f;
        isLoadingComplete = false;
        
        // Основной цикл загрузки
        while (!isLoadingComplete)
        {
            loadingTime += Time.deltaTime;
            
            // Рассчитываем прогресс загрузки
            CalculateLoadingProgress();
            
            // Обновляем UI слайдера
            UpdateLoadingSlider();
            
            // Проверяем условия завершения загрузки
            // Ждём пока пройдёт минимальное время И загрузка почти завершена
            if (loadingTime >= minLoadTime && loadingOperation.progress >= 0.9f)
            {
                isLoadingComplete = true;
            }
            
            yield return null;
        }
        
        // Завершаем загрузку и переходим на сцену
        CompleteLoading();
    }
    
    string GetTargetSceneName()
    {
        // Пытаемся получить следующую сцену из PlayerPrefs
        string nextScene = PlayerPrefs.GetString(NEXT_SCENE_KEY, "");
        
        // Если следующая сцена не указана, загружаем лобби по умолчанию
        if (string.IsNullOrEmpty(nextScene))
        {
            return "LobbyScene";
        }
        
        return nextScene;
    }
    
    void CalculateLoadingProgress()
    {
        // Прогресс от операции загрузки (0-0.9)
        float operationProgress = Mathf.Clamp01(loadingOperation.progress / 0.9f);
        
        // Прогресс от минимального времени (0-1 за minLoadTime секунд)
        float timeProgress = Mathf.Clamp01(loadingTime / minLoadTime);
        
        // Используем наименьший прогресс для плавной анимации
        // Это гарантирует, что даже если сцена загрузилась быстро, анимация проиграется полностью
        loadingProgress = Mathf.Min(operationProgress, timeProgress);
    }
    
    void UpdateLoadingSlider()
    {
        if (loadingSlider != null)
        {
            loadingSlider.value = loadingProgress;
        }
    }
    
    void CompleteLoading()
    {
        Debug.Log("Loading complete! Activating scene...");
        
        // Разрешаем активацию сцены
        loadingOperation.allowSceneActivation = true;
    }
    
    // === СТАТИЧЕСКИЕ МЕТОДЫ ДЛЯ ПЕРЕХОДА МЕЖДУ СЦЕНАМИ ===
    
    // Метод для перехода на любую сцену через LoadingScene
    public static void LoadScene(string sceneName)
    {
        // Сохраняем следующую сцену
        PlayerPrefs.SetString(NEXT_SCENE_KEY, sceneName);
        PlayerPrefs.Save();
        
        // Загружаем сцену загрузки
        SceneManager.LoadScene("LoadingScene");
    }
    
    // Метод для перехода на лобби
    public static void LoadLobby()
    {
        LoadScene("LobbyScene");
    }
    
    // Методы для загрузки уровней
    public static void LoadLevel1()
    {
        LoadScene("SceneLvl1");
    }
    
    public static void LoadLevel2()
    {
        LoadScene("SceneLvl2");
    }
    
    public static void LoadLevel3()
    {
        LoadScene("SceneLvl3");
    }
}