using UnityEngine;
using System.Collections.Generic;

// Компонент игрока, отвечающий за управление оружием.
public class PlayerAttack : MonoBehaviour
{
    private PlayerContext _context; // Ссылка на данные игрока

    // Список оружий этого игрока (ключ = слот, значение = GameObject оружия)
    public Dictionary<int, GameObject> WeaponsList = new Dictionary<int, GameObject>();

    // Вызывается из PlayerSpawner после спавна
    public void Initialize(PlayerContext context)
    {
        _context = context;
    }

    // Возвращает контекст для дочерних компонентов (например, WeaponAttack)
    public PlayerContext GetContext()
    {
        return _context;
    }

    // Добавляет оружие в список игрока
    public void AddWeapon(GameObject newWeapon)
    {
        int slot = WeaponsList.Count;
        WeaponsList.Add(slot, newWeapon);
        Debug.Log($"Добавлено оружие: {newWeapon.name}");
    }

    // Удаляет оружие из списка
    public void RemoveWeapon(GameObject weaponToRemove)
    {
        if (weaponToRemove == null) return;

        int? keyToRemove = null;
        foreach (var kvp in WeaponsList)
        {
            if (kvp.Value == weaponToRemove)
            {
                keyToRemove = kvp.Key;
                break;
            }
        }

        if (keyToRemove.HasValue)
        {
            WeaponsList.Remove(keyToRemove.Value);
            Debug.Log($"Удалено оружие: {weaponToRemove.name}");
        }
        else
        {
            Debug.LogWarning($"Оружие {weaponToRemove.name} не найдено в списке!");
        }
    }
}