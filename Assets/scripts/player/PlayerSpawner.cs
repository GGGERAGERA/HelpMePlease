using UnityEngine;
using System.Collections.Generic;

// Спавнер игрока. Отвечает ТОЛЬКО за создание игрока на сцене.
// В мультиплеере будет делегировать спавн PlayerManager'у.
public class PlayerSpawner : MonoBehaviour
{
    [Header("Настройки спавна")]
    public GlobalPlayerStatsSO globalPlayerStatsSO1; // Глобальные улучшения (покупки)
    public PlayerSelectManagerSO selectedPlayerPrefabSO1; // Выбранный персонаж
    public Transform spawnPoint; // Точка спавна

    [Header("Для отладки")]
    public bool autoSpawnOnStart = true;

    // 🔮 Мультиплеер: этот словарь позже перенесётся в PlayerManager
    private Dictionary<int, PlayerContext> _players = new Dictionary<int, PlayerContext>();
    private int _nextPlayerId = 0;

    private void Awake()
    {
        DestroyAllObjectsWithTag("Player");
    }

    private void Start()
    {
        if (autoSpawnOnStart && selectedPlayerPrefabSO1 != null)
        {
            SpawnPlayer(selectedPlayerPrefabSO1, spawnPoint);
            Debug.Log("Player spawned");
        }
    }

    // Спавнит одного игрока. В мультиплеере вызывается для каждого игрока.
    public PlayerContext SpawnPlayer(PlayerSelectManagerSO characterSO, Transform spawnPoint)
    {
        if (characterSO?.selectedPlayerPrefab == null)
        {
            Debug.LogError("Не задан префаб игрока в PlayerSelectManagerSO!");
            return null;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("Точка спавна не задана. Используем (0,0,0).");
            spawnPoint = transform;
        }

        // Создаём ИНСТАНС игрока на сцене
        GameObject playerInstance = Instantiate(
            characterSO.selectedPlayerPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );
        playerInstance.tag = "Player";

        // Добавляем обязательные компоненты, если их нет
        playerInstance.GetOrAddComponent<PlayerAttack>();
        playerInstance.GetOrAddComponent<PlayerHealth>();

        // Создаём контекст данных для этого игрока
        var context = new PlayerContext(
            playerId: _nextPlayerId++,
            playerObject: playerInstance, // ← ВАЖНО: именно инстанс, а не префаб!
            character: characterSO,
            globalStats: globalPlayerStatsSO1
        );

        // Сохраняем контекст (для мультиплеера)
        _players[context.PlayerID] = context;

        // Передаём контекст компонентам игрока
        playerInstance.GetComponent<PlayerAttack>().Initialize(context);
        playerInstance.GetComponent<PlayerHealth>().Initialize(context);

        return context;
    }

    // Получить игрока по ID (пригодится в мультиплеере)
    public PlayerContext GetPlayer(int id)
    {
        return _players.GetValueOrDefault(id);
    }

    // Уничтожает всех игроков на сцене (при перезапуске уровня)
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

    //🔮 Как добавить мультиплеер позже:
    //1) Создай PlayerManager.cs (скопируй _players и _nextPlayerId из PlayerSpawner).
    //2) В PlayerSpawner.SpawnPlayer() замени:
    /*
        // Было:
        _players[context.PlayerID] = context;
        // Стало:
        PlayerManager.Instance.RegisterPlayer(context);
    */
}