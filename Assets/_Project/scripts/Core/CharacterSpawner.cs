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
    [SerializeField] private UpgradeApplier upgradeApplier;

    [Header("Default weapon for direct MVP launch")]
    [SerializeField] private WeaponData defaultWeapon;
    private void Awake()
    {
        if (metaUpgradeApplier == null)
            metaUpgradeApplier = GetComponent<MetaUpgradeApplier>();

        if (metaUpgradeApplier == null)
            metaUpgradeApplier = FindFirstObjectByType<MetaUpgradeApplier>();
    }
    private void Start()
    {
        Time.timeScale = 1f;

        GameObject player = SpawnCharacter();

        if (player == null)
            return;

        BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>(true);

        if (metaUpgradeApplier == null)
            metaUpgradeApplier = FindFirstObjectByType<MetaUpgradeApplier>();

        if (metaUpgradeApplier != null)
        {
            Debug.Log($"[CharacterSpawner] Weapons found: {weapons.Length}");
            metaUpgradeApplier.ApplyTo(player, weapons);
        }
        else
        {
            Debug.LogWarning("[CharacterSpawner] MetaUpgradeApplier not found. Meta upgrades were not applied.");
        }

        if (RunStateManager.Instance != null)
            RunStateManager.Instance.ApplyToSpawnedPlayer(player, upgradeApplier);
    }

    private GameObject SpawnCharacter()
    {
        CharacterData selectedCharacter = GetSelectedCharacter();

        if (selectedCharacter == null)
        {
            Debug.LogError("[CharacterSpawner] No selected/default character.");
            return null;
        }

        if (selectedCharacter.characterPrefab == null)
        {
            Debug.LogError($"[CharacterSpawner] Character prefab is missing on {selectedCharacter.name}.");
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
        if (RunStateManager.Instance != null &&
            RunStateManager.Instance.SelectedCharacter != null)
        {
            return RunStateManager.Instance.SelectedCharacter;
        }

        if (RunSelectionManager.Instance != null &&
            RunSelectionManager.Instance.SelectedCharacter != null)
        {
            return RunSelectionManager.Instance.SelectedCharacter;
        }

        return defaultCharacter;
    }

    private WeaponData GetSelectedWeapon()
    {
        if (RunStateManager.Instance != null &&
            RunStateManager.Instance.SelectedWeapon != null)
        {
            return RunStateManager.Instance.SelectedWeapon;
        }

        if (RunSelectionManager.Instance != null &&
            RunSelectionManager.Instance.SelectedWeapon != null)
        {
            return RunSelectionManager.Instance.SelectedWeapon;
        }

        return defaultWeapon;
    }

    private void ApplyCharacterStats(GameObject player, CharacterData characterData)
    {
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
            Debug.LogWarning("[CharacterSpawner] No selected/default weapon.");
            return;
        }

        if (weaponData.weaponPrefab == null)
        {
            Debug.LogWarning($"[CharacterSpawner] Weapon prefab is missing on {weaponData.name}.");
            return;
        }

        Transform weaponPoint = player.transform.Find(weaponPointName);

        if (weaponPoint == null)
            weaponPoint = player.transform;

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
}
