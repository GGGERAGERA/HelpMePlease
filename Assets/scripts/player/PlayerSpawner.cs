using UnityEngine;

/// <summary>
/// Спавнер игрока. Берет выбранный персонаж из ScriptableObject и создает его на сцене.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Настройки спавна")]
    [Tooltip("Ссылка на ScriptableObject с данными выбранного игрока")]
    [SerializeField] private PlayerSelectionSO selectedPlayerSO;

    [Tooltip("Точка спавна (пустой GameObject на сцене)")]
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

    /// <summary>
    /// Метод для ручного спавна игрока (можно вызывать из UI или другого скрипта)
    /// </summary>
    public void SpawnPlayer()
    {
        if (selectedPlayerSO == null || selectedPlayerSO.selectedPlayerPrefab == null)
        {
            Debug.LogError("Не задан PlayerPrefab в PlayerStatsSO!");
            return;
        }

        if(spawnPoint==null)
        spawnPoint = selectedPlayerSO.selectedPlayerPrefab.transform;


        // Создаем игрока в точке спавна
        GameObject playerInstance = Instantiate(selectedPlayerSO.selectedPlayerPrefab, spawnPoint.position, spawnPoint.rotation);
        playerInstance.tag = "Player"; // На всякий случай (если prefab не имеет тега)
        
        // Добавляем компоненты движения и анимаций (если их нет)
        if (playerInstance.GetComponent<PlayerMovement>() == null)
        {
            playerInstance.AddComponent<PlayerMovement>();
        }
        if (playerInstance.GetComponent<PlayerAnimation>() == null)
        {
            playerInstance.AddComponent<PlayerAnimation>();
        }

        // Передаем ссылку на SO в компоненты (если нужно)
        /*PlayerMovement movement = playerInstance.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.playerStats = selectedPlayerSO;
        }

        PlayerAnimation animation = playerInstance.GetComponent<PlayerAnimation>();
        if (animation != null)
        {
            animation.playerStats = selectedPlayerSO;
        }*/

        Debug.Log($"Игрок {selectedPlayerSO.name} spawned!");
    }

    /// <summary>
/// Уничтожает все объекты на сцене с указанным тегом.
/// </summary>
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
}