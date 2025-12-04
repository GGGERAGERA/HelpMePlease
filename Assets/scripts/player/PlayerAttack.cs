using UnityEngine;
using System.Collections.Generic;
public class PlayerAttack : MonoBehaviour
{
    private PlayerContext _context;
    public Dictionary<int, GameObject> WeaponsList = new();

    public void Initialize(PlayerContext context)
    {
        _context = context;
    }

    public PlayerContext GetContext() => _context;

    public void AddWeapon(GameObject newWeapon)
    {
        int i = WeaponsList.Count;
        WeaponsList.Add(i, newWeapon);
        Debug.Log($"Добавлено оружие: {newWeapon.name}");
    }

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