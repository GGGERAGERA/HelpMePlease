// PlayerContext.cs
using UnityEngine;
using System.Collections.Generic;

public class PlayerContext
{
    // 🔑 Уникальный ID игрока (для мультиплеера)
    public int PlayerID { get; private set; }

    // 🎮 Ссылка на GameObject (только для удобства)
    public GameObject PlayerObject { get; private set; }

    // 📊 Данные
    public PlayerSelectionSO SelectedCharacter => _selectedCharacter;
    private PlayerSelectionSO _selectedCharacter;

    public PlayerStatsSO BaseStats => _selectedCharacter?.selectedPlayerStats;
    public GlobalPlayerStatsSO GlobalStats { get; private set; }
    public List<PlayerBuffs> ActiveBuffs { get; private set; } = new();

    // 💰 Состояние
    public int Money { get; set; }
    public int Health { get; set; }

    // 🛠 Конструктор
    public PlayerContext(int playerId, GameObject playerObject, PlayerSelectionSO character, GlobalPlayerStatsSO globalStats)
{
    PlayerID = playerId;
    PlayerObject = playerObject;
    _selectedCharacter = character;
    GlobalStats = globalStats; // ← ДОБАВЬ ЭТО!

    // Инициализация
    Money = 0;
    Health = BaseStats?.playerMaxHealth ?? 100;
}
    // 🧪 Удобные методы
    public int GetTotalAttack()
    {
        int total = BaseStats?.playerpower ?? 0;
        total += GlobalStats?.GlobalAttackBonus ?? 0;

        foreach (var buff in ActiveBuffs)
        {
            total += buff.AttackBonus;
        }
        return total;
    }

    public float GetProjectileSpeedMultiplier()
    {
        float mult = 1f;
        mult += GlobalStats?.GlobalPprojectileSpeed ?? 0f;
        foreach (var buff in ActiveBuffs)
        {
            mult += buff.ProjectileSpeedBonus;
        }
        return mult;
    }

    public void AddBuff(PlayerBuffsSO buffSO)
    {
        var buff = new PlayerBuffs(buffSO);
        ActiveBuffs.Add(buff);
        // Запуск корутины на PlayerObject, если нужен таймер
    }

    public void RemoveBuff(PlayerBuffs buff)
    {
        ActiveBuffs.Remove(buff);
    }
}