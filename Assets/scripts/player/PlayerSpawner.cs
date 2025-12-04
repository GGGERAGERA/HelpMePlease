using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Настройки спавна")]
    [SerializeField] private PlayerSelectionSO selectedPlayerSO;
    [SerializeField] private Transform spawnPoint;

    [Header("Для отладки")]
    [SerializeField] private bool autoSpawnOnStart = true;

    private void Awake()
    {
        DestroyAllObjectsWithTag("Player");
    }

    private void Start()
    {
        if (autoSpawnOnStart && selectedPlayerSO != null)
        {
            SpawnPlayer();
        }
    }

    /// Метод для ручного спавна игрока
    public void SpawnPlayer()
    {
        if (selectedPlayerSO == null)
        {
            Debug.LogError("Не задан PlayerSelectionSO!");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("Точка спавна не задана. Используем (0,0,0).");
            spawnPoint = transform;
        }

        // 👇 ДЕЛЕГИРУЕМ СПАВН PlayerManager'у!
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SpawnPlayer(selectedPlayerSO, spawnPoint);
        }
        else
        {
            Debug.LogError("PlayerManager не найден на сцене!");
        }
    }

    public static void DestroyAllObjectsWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
    public void OnCharacterSelected(PlayerSelectionSO character)
    {
    // Сохраняем выбранный персонаж
    PlayerPrefs.SetString("SelectedCharacter", character.name);
    // Спавним
    SpawnPlayer(); // или через PlayerManager напрямую
    }
}