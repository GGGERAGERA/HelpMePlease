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
            return null;
        }

        if (selectedCharacter.characterPrefab == null)
        {
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
        if (RunSelectionManager.Instance != null &&
            RunSelectionManager.Instance.SelectedCharacter != null)
        {
            return RunSelectionManager.Instance.SelectedCharacter;
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
            return;
        }

        if (weaponData.weaponPrefab == null)
        {
            return;
        }

        Transform weaponPoint = player.transform.Find(weaponPointName);

        if (weaponPoint == null)
        {
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
        if (RunSelectionManager.Instance != null &&
            RunSelectionManager.Instance.SelectedWeapon != null)
        {
            return RunSelectionManager.Instance.SelectedWeapon;
        }

        return defaultWeapon;
    }
}