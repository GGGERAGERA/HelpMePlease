// PlayerManager.cs
using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [Header("Глобальные данные (общие для всех игроков)")]
    public GlobalPlayerStatsSO globalStats;

    [Header("Список активных игроков")]
    private Dictionary<int, PlayerContext> _players = new();
    private int _nextPlayerId = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// Спавн нового игрока
    public PlayerContext SpawnPlayer(PlayerSelectManagerSO character, Transform spawnPoint)
    {
        if (character?.selectedPlayerPrefab == null) return null;

        GameObject playerObj = Instantiate(character.selectedPlayerPrefab, spawnPoint.position, spawnPoint.rotation);
        playerObj.tag = "Player";

        // Добавляем обязательные компоненты
        
        playerObj.GetOrAddComponent<PlayerHealth>();
        playerObj.GetOrAddComponent<PlayerMovement>();
        playerObj.GetOrAddComponent<PlayerAttack>();

        // Создаём контекст
        var context = new PlayerContext(_nextPlayerId++, playerObj, character, globalStats);
        _players[context.PlayerID] = context;

        // Передаём контекст компонентам (через ссылку)
        playerObj.GetComponent<PlayerAttack>().Initialize(context);
        playerObj.GetComponent<PlayerHealth>().Initialize(context);

        Debug.Log($"Игрок {context.PlayerID} создан!");
        return context;
    }

    public PlayerContext GetPlayer(int playerId) => _players.GetValueOrDefault(playerId);
    public PlayerContext GetLocalPlayer() => _players.Count > 0 ? _players[0] : null; // для синглплеера
}