using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    [Header("Default character for direct MVP launch")]
    [SerializeField] private CharacterData defaultCharacter;

    [Header("Spawn settings")]
    [SerializeField] private Transform spawnPoint;

    [Header("Weapon spawn settings")]
    [SerializeField] private string weaponPointName = "WeaponPoint";

    [SerializeField] private MetaUpgradeApplier metaUpgradeApplier;

    [Header("Default weapon for direct MVP launch")]
    [SerializeField] private WeaponData defaultWeapon;

    private void Start()
    {
        Time.timeScale = 1f;

        GameObject player = SpawnCharacter();

        if (player == null)
            return;

        BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>();

        if (metaUpgradeApplier != null)
            metaUpgradeApplier.ApplyTo(player, weapons);
    }

    private GameObject SpawnCharacter()
    {
        CharacterData selectedCharacter = GetSelectedCharacter();

        if (selectedCharacter == null)
        {
            Debug.LogError("CharacterSpawner: No selected character and no defaultCharacter assigned.");
            return null;
        }

        if (selectedCharacter.characterPrefab == null)
        {
            Debug.LogError("CharacterSpawner: characterPrefab is not assigned in CharacterData: " + selectedCharacter.characterName);
            return null;
        }

        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;

        GameObject player = Instantiate(
            selectedCharacter.characterPrefab,
            spawnPosition,
            Quaternion.identity
        );

        player.tag = "Player";

        ApplyCharacterStats(player, selectedCharacter);

        WeaponData selectedWeapon = GetSelectedWeapon();
        SpawnWeapon(player, selectedWeapon);

        return player;
    }

    private CharacterData GetSelectedCharacter()
    {
        if (CharactersSelectionManager.Instance != null)
        {
            CharacterData selectedFromManager = CharactersSelectionManager.Instance.GetSelectedCharacter();

            if (selectedFromManager != null)
                return selectedFromManager;

            CharacterData[] allCharacters = CharactersSelectionManager.Instance.allCharacters;

            if (allCharacters != null && allCharacters.Length > 0)
            {
                int selectedIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);

                if (selectedIndex >= 0 && selectedIndex < allCharacters.Length)
                {
                    if (allCharacters[selectedIndex] != null)
                        return allCharacters[selectedIndex];
                }
            }
        }

        return defaultCharacter;
    }

    private void ApplyCharacterStats(GameObject player, CharacterData characterData)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();

        if (stats != null)
            stats.moveSpeed = characterData.moveSpeed;

        PlayerHealth health = player.GetComponent<PlayerHealth>();

        if (health != null)
        {
            health.maxHealth = characterData.maxHealth;
            health.currentHealth = characterData.maxHealth;
        }

        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();

        if (movement != null)
            movement.speed = characterData.moveSpeed;
    }

    private void SpawnWeapon(GameObject player, WeaponData weaponData)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("CharacterSpawner: weaponData is null.");
            return;
        }

        if (weaponData.weaponPrefab == null)
        {
            Debug.LogWarning("CharacterSpawner: weaponPrefab is not assigned in WeaponData: " + weaponData.weaponName);
            return;
        }

        Transform weaponPoint = player.transform.Find(weaponPointName);

        if (weaponPoint == null)
        {
            Debug.LogWarning("CharacterSpawner: WeaponPoint not found. Weapon will spawn in player center.");
            weaponPoint = player.transform;
        }

        GameObject weapon = Instantiate(
            weaponData.weaponPrefab,
            weaponPoint.position,
            weaponPoint.rotation,
            player.transform
        );

        weapon.transform.position = weaponPoint.position;
        weapon.transform.rotation = weaponPoint.rotation;

        AssignWeaponData(weapon, weaponData);

        Debug.Log("Spawned weapon: " + weaponData.weaponName);
    }

    private void AssignWeaponData(GameObject weapon, WeaponData weaponData)
    {
        BaseWeapon baseWeapon = weapon.GetComponent<BaseWeapon>();

        if (baseWeapon != null)
        {
            baseWeapon.Initialize(weaponData);
        }
        else
        {
            Debug.LogWarning("CharacterSpawner: spawned weapon has no BaseWeapon component.");
        }
    }

    private WeaponData GetSelectedWeapon()
    {
        if (WeaponSelectionManager.Instance != null)
        {
            WeaponData selectedWeapon = WeaponSelectionManager.Instance.GetSelectedWeapon();

            if (selectedWeapon != null)
                return selectedWeapon;
        }

        return defaultWeapon;
    }
}