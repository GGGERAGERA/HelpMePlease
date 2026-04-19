using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScene : MonoBehaviour
{
    [Header("Loading Settings")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private float minLoadTime = 2f;
    
    [Header("Scene Management")]
    [SerializeField] private string firstSceneName = "SettingsScene1";
    [SerializeField] private string loadingSceneName = "LoadingScene1";
    
    private AsyncOperation loadingOperation;
    private float loadingProgress;
    private float loadingTime;
    private bool isLoadingComplete = false;
    
    // Ключ для сохранения
    private const string LAST_SCENE_KEY = "LastActiveScene";
    
    void Start()
    {
        // Запускаем процесс загрузки
        StartCoroutine(LoadTargetScene());
    }
    
    void OnEnable()
    {
        // Подписываемся на событие смены сцены
        SceneManager.activeSceneChanged += OnSceneChanged;
    }
    
    void OnDisable()
    {
        // Отписываемся от события
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }
    
    void OnSceneChanged(Scene current, Scene next)
    {
        // Автоматически сохраняем сцену при любой смене сцены
        if (next.name != loadingSceneName)
        {
            SaveCurrentScene(next.name);
        }
    }
    
    IEnumerator LoadTargetScene()
    {
        // Получаем имя сцены для загрузки
        string targetSceneName = GetTargetSceneName();
        
        // Начинаем загрузку асинхронно
        loadingOperation = SceneManager.LoadSceneAsync(targetSceneName);
        loadingOperation.allowSceneActivation = false;
        
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
        // Пытаемся получить последнюю активную сцену из PlayerPrefs
        string lastScene = PlayerPrefs.GetString(LAST_SCENE_KEY, "");
        
        // Проверяем, существует ли сохраненная сцена и не является ли она текущей сценой загрузки
        if (!string.IsNullOrEmpty(lastScene) && lastScene != loadingSceneName)
        {
            // Проверяем, существует ли такая сцена в build settings
            if (IsSceneInBuildSettings(lastScene))
            {
                return lastScene;
            }
        }
        
        // Если сохраненной сцены нет или она некорректна, используем первую сцену
        return firstSceneName;
    }
    
    bool IsSceneInBuildSettings(string sceneName)
    {
        // Проверяем все сцены в build settings
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string scene = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            
            if (scene == sceneName)
            {
                return true;
            }
        }
        return false;
    }
    
    void CalculateLoadingProgress()
    {
        // Прогресс от операции загрузки (0-0.9)
        float operationProgress = Mathf.Clamp01(loadingOperation.progress / 0.9f);
        
        // Прогресс от минимального времени (0-1 за minLoadTime секунд)
        float timeProgress = Mathf.Clamp01(loadingTime / minLoadTime);
        
        // Используем наименьший прогресс для плавной анимации
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
        // Разрешаем активацию сцены
        loadingOperation.allowSceneActivation = true;
    }
    
    // Автоматическое сохранение текущей сцены
    private void SaveCurrentScene(string sceneName)
    {
        if (sceneName != loadingSceneName)
        {
            PlayerPrefs.SetString(LAST_SCENE_KEY, sceneName);
            PlayerPrefs.Save();
            Debug.Log($"Автосохранение сцены: {sceneName}");
        }
    }
}