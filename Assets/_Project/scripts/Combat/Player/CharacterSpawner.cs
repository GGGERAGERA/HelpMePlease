using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    public event System.Action<GameObject> CharacterSpawned;
    public event System.Action<BaseWeapon> PrimaryWeaponChanged;
    public GameObject SpawnedPlayer { get; private set; }
    public BaseWeapon PrimaryWeapon { get; private set; }
    public CharacterData SpawnedCharacterData { get; private set; }
    public CharacterCombatType CombatType => SpawnedCharacterData != null
        ? SpawnedCharacterData.combatType
        : CharacterCombatType.AutoFire;

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

        HUDManager.Instance?.BindPlayer(player);

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

        AnomalyPowerRuntime.ApplyRunLoadout(player);
        AnomalySlotHUD.EnsureExists();

        AnomalyCoreRuntime coreRuntime =
            player.GetComponent<AnomalyCoreRuntime>();
        coreRuntime ??= player.AddComponent<AnomalyCoreRuntime>();
        coreRuntime.Initialize(this, PrimaryWeapon);

        EvolutionRuntimeController evolutionRuntime =
            player.GetComponent<EvolutionRuntimeController>();
        evolutionRuntime ??= player.AddComponent<EvolutionRuntimeController>();
        evolutionRuntime.Initialize(this, PrimaryWeapon);

        SpawnedPlayer = player;
        CharacterSpawned?.Invoke(player);
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

        SpawnedCharacterData = selectedCharacter;

        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;

        GameObject player = Instantiate(
            selectedCharacter.characterPrefab,
            spawnPosition,
            Quaternion.identity
        );

        player.tag = "Player";

        PlayerLoadoutFactory.ApplyCharacterStats(player, selectedCharacter);

        WeaponData selectedWeapon = GetSelectedWeapon();
        SetPrimaryWeapon(PlayerLoadoutFactory.SpawnWeapon(
            player,
            selectedWeapon,
            CombatType,
            weaponPointName));
        PlayerWeaponOrbitVisual.Ensure(player, PrimaryWeapon);

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

    private void SetPrimaryWeapon(BaseWeapon weapon)
    {
        if (PrimaryWeapon == weapon)
            return;

        PrimaryWeapon = weapon;
        PlayerWeaponOrbitVisual orbitVisual = SpawnedPlayer != null
            ? SpawnedPlayer.GetComponent<PlayerWeaponOrbitVisual>()
            : weapon != null
                ? weapon.GetComponentInParent<PlayerWeaponOrbitVisual>()
                : null;
        orbitVisual?.Bind(weapon);
        PrimaryWeaponChanged?.Invoke(weapon);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void ConfigureDebugDefaults(
        CharacterData character,
        WeaponData weapon,
        UpgradeApplier runUpgradeApplier)
    {
        defaultCharacter = character;
        defaultWeapon = weapon;
        upgradeApplier = runUpgradeApplier;
    }

    public bool TryReplaceDebugPrimaryWeapon(
        GameObject player,
        WeaponData weaponData,
        out BaseWeapon replacement)
    {
        replacement = null;

        if (player == null || weaponData == null ||
            weaponData.weaponPrefab == null)
        {
            return false;
        }

        TelekinesisDebugPrototype prototype =
            player.GetComponent<TelekinesisDebugPrototype>();
        prototype?.ResetPrototype();

        BaseWeapon current = FindDebugPrimaryWeapon(player);

        if (current != null && current.weaponData == weaponData)
        {
            replacement = current;
            prototype?.SetPrimaryWeapon(current);
            SetPrimaryWeapon(current);
            return true;
        }

        if (current != null)
            current.gameObject.SetActive(false);

        replacement = PlayerLoadoutFactory.SpawnWeapon(
            player,
            weaponData,
            CombatType,
            weaponPointName);

        if (replacement == null)
        {
            if (current != null)
                current.gameObject.SetActive(true);

            return false;
        }

        if (current != null)
        {
            replacement.CopyRuntimeUpgradeModifiersFrom(current);
            Destroy(current.gameObject);
        }

        prototype?.SetPrimaryWeapon(replacement);
        SetPrimaryWeapon(replacement);
        return true;
    }

    public BaseWeapon SpawnTelekinesisDebugWeapon(
        GameObject player,
        WeaponData weaponData,
        BaseWeapon modifierSource)
    {
        if (player == null || weaponData == null)
            return null;

        BaseWeapon weapon = PlayerLoadoutFactory.SpawnWeapon(
            player,
            weaponData,
            CombatType,
            weaponPointName);

        if (weapon != null && modifierSource != null)
            weapon.CopyRuntimeUpgradeModifiersFrom(modifierSource);

        return weapon;
    }

    public BaseWeapon SpawnTelekinesisDebugWeaponClone(
        GameObject player,
        BaseWeapon source)
    {
        if (player == null || source == null || source.weaponData == null)
            return null;

        BaseWeapon clone = PlayerLoadoutFactory.SpawnWeapon(
            player,
            source.weaponData,
            CombatType,
            weaponPointName);

        if (clone == null)
            return null;

        clone.CopyRuntimeStatsFrom(source);
        return clone;
    }

    private static BaseWeapon FindDebugPrimaryWeapon(GameObject player)
    {
        BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>(true);

        for (int i = 0; i < weapons.Length; i++)
        {
            BaseWeapon weapon = weapons[i];

            if (weapon != null && !weapon.IsTelekinesisDebugSecondary)
                return weapon;
        }

        return null;
    }
#endif
}
