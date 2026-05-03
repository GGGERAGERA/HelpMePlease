using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    void Start()
    {
        SpawnCharacter();
    }

    void SpawnCharacter()
    {
        // Получаем выбранного персонажа из меню
        int selectedIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        CharacterData[] allCharacters = CharactersSelectionManager.Instance?.allCharacters;

        if (allCharacters == null || allCharacters.Length == 0)
        {
            Debug.LogError("No characters found!");
            return;
        }

        CharacterData selectedCharacter = allCharacters[selectedIndex];
        if (selectedCharacter == null || selectedCharacter.characterPrefab == null)
        {
            Debug.LogError("Selected character or its prefab is null!");
            return;
        }

        // Создаём персонажа
        GameObject player = Instantiate(selectedCharacter.characterPrefab, Vector3.zero, Quaternion.identity);
        player.tag = "Player";

        // Настраиваем характеристики
        PlayerStats stats = player.GetComponent<PlayerStats>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();

        if (stats != null)
        {
            stats.baseDamage = selectedCharacter.damage;
            stats.moveSpeed = selectedCharacter.moveSpeed;
        }
        if (health != null)
        {
            health.maxHealth = selectedCharacter.maxHealth;
            health.currentHealth = selectedCharacter.maxHealth;
        }
        if (movement != null)
        {
            movement.speed = selectedCharacter.moveSpeed;
        }

        // Спавним стартовое оружие
        if (selectedCharacter.startingWeapon != null && selectedCharacter.startingWeapon.weaponPrefab != null)
        {
            GameObject weapon = Instantiate(selectedCharacter.startingWeapon.weaponPrefab, player.transform);
            // Если на оружии есть скрипт с WeaponData — передаём данные
            OrbitalWeapon orbital = weapon.GetComponent<OrbitalWeapon>();
            if (orbital != null) orbital.weaponData = selectedCharacter.startingWeapon;

            Shoot shoot = weapon.GetComponent<Shoot>();
            if (shoot != null) shoot.weaponData = selectedCharacter.startingWeapon;

            LaserSword sword = weapon.GetComponent<LaserSword>();
            if (sword != null) sword.weaponData = selectedCharacter.startingWeapon;
        }
    }
}