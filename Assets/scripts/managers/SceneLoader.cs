using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI percentText;

    // STATIС переменная — доступна ВСЕГДА, даже между сценами
    private static bool _isGameStartCorrect = false;

    [Header("Settings")]
    [SerializeField] private float minLoadTime = 2f;

    private string _targetSceneName;

    public void Start()
    {
        if (SceneManager.GetActiveScene().name == "PreLoadingScene")
        {
            Debug.Log("Мы в стартовой сцене, игра запущена правильно!");
            _isGameStartCorrect = true; // Сохраняем флаг
        }
        else
        {
            if (_isGameStartCorrect)
            {
                Debug.Log("Мы в другой сцене! Но игра была запущена корректно!");
            }
            else
            {
                Debug.LogWarning("ИГРА ЗАПУЩЕНА НЕПРАВИЛЬНО!");
                SceneManager.LoadScene("PreLoadingScene");
            }
        }
    }

    // === МЕТОДЫ ЗАГРУЗКИ — проверяют флаг ===

    public void LoadLevel(string sceneName)
    {
        // Проверяем перед загрузкой
        if (!_isGameStartCorrect)
        {
            Debug.LogError("Нельзя загружать уровень — игра запущена неправильно!");
            return;
        }

        _targetSceneName = sceneName;
        StartCoroutine(LoadAsync());
    }

    public void LoadLobby()
    {
        if (!_isGameStartCorrect)
        {
            Debug.LogError("Нельзя загрузить лобби — игра запущена неправильно!");
            return;
        }

        _targetSceneName = "LobbyScene";
        StartCoroutine(LoadAsync());
    }

    private IEnumerator LoadAsync()
    {
        // Твоя логика загрузки
        Debug.Log($"Загружаем: {_targetSceneName}");
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(_targetSceneName);
        
        while (!asyncLoad.isDone)
        {
            // Обновляй слайдер тут
            yield return null;
        }
    }
}