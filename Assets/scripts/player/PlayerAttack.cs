using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{   [Header("Оружия")]
    public Dictionary<int, GameObject> WeaponsList = new Dictionary<int, GameObject>();

    [Header("Сам игрок")] //на всякий случай делаю публичным, потому что не знаю как будет реализована система спавна нового оружия
    public GameObject PlayerParentPrefab;

    public void AddWeapon(GameObject newWeapon)
    {
        int i = WeaponsList.Count;
        WeaponsList.Add(i, newWeapon);
        Debug.Log($"Добавлено оружие: {newWeapon.name}");
    }

    public void RemoveWeapon(GameObject weaponToRemove)
    {
    if (weaponToRemove == null) return;

    // Ищем ключ, у которого значение == weaponToRemove
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
