using UnityEngine;
using System.Collections.Generic;

// Контейнер данных игрока. Не MonoBehaviour!
// Хранит ВСЁ: статы, баффы, здоровье, деньги.
public class PlayerContext
{/*
    // Уникальный ID игрока. В синглплеере = 0, в мультиплеере — уникальный номер.
    public int PlayerID { get; private set; }

    // Ссылка на реальный GameObject игрока на сцене (для корутин, тегов и т.д.)
    public GameObject PlayerObject { get; private set; }

    // Данные персонажа (префаб + статы)

    private PlayerSelectManagerSO _selectedCharacter;
    private PlayerStatsSO _baseStats;
    public GlobalPlayerStatsSO GlobalStats { get; private set; }

    // Активные баффы (временные улучшения)
    public List<PlayerBuffs> ActiveBuffs { get; private set; } = new List<PlayerBuffs>();

    // Состояние игрока
    public int Money { get; set; }
    public int Health { get; set; }

    // Конструктор: вызывается при спавне игрока
    public PlayerContext(int playerId, GameObject playerObject, PlayerSelectManagerSO character, GlobalPlayerStatsSO globalStats)
    {
        PlayerID = playerId;
        PlayerObject = playerObject;
        _selectedCharacter = character;
        GlobalStats = globalStats;
        
        // 🔑 Инициализация статов из SO
        PlayerStatsComponent statsComp = playerObject.GetComponent<PlayerStatsComponent>();
        _baseStats = statsComp != null ? statsComp.playerStatsSO : null;

        Money = 0;
        Health = _baseStats?.playerMaxHealth ?? 100;
    }

    // Рассчитывает итоговый урон игрока
    public int GetTotalAttack()
    {
        int total = _baseStats?.playerpower ?? 0;
        total += GlobalStats?.GlobalAttackBonus ?? 0;

        foreach (var buff in ActiveBuffs)
        {
            total += buff.AttackBonus;
        }
        return total;
    }

    // Рассчитывает множитель скорости снарядов
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

    // Добавляет бафф (вызывается при подборе предмета)
    public void AddBuff(PlayerBuffsSO buffSO)
    {
        var buff = new PlayerBuffs(buffSO);
        ActiveBuffs.Add(buff);
    }

    // Удаляет бафф (например, по истечении времени)
    public void RemoveBuff(PlayerBuffs buff)
    {
        ActiveBuffs.Remove(buff);
    }
    */
}