using UnityEngine;
using Subject42.Combat.OrbitalStation;

public class CharacterSpawner : MonoBehaviour
{
    public event System.Action<GameObject> CharacterSpawned;
    public event System.Action<BaseWeapon> PrimaryWeaponChanged;
    public GameObject SpawnedPlayer { get; private set; }
    public BaseWeapon PrimaryWeapon { get; private set; }
    public CharacterData SpawnedCharacterData { get; private set; }
    public OrbitalStationRuntime Station { get; private set; }
    public void PrepareForSceneTransition() => Station?.GetComponent<OrbitalInteractionController>()?.PrepareForExternalPause();
    public CharacterCombatType CombatType => SpawnedCharacterData != null
        ? SpawnedCharacterData.combatType
        : CharacterCombatType.AutoFire;

    [Header("Default character for direct MVP launch")]
    [SerializeField] private CharacterData defaultCharacter;
    public CharacterData DefaultCharacter => defaultCharacter;
    private HUDManager hud;
    private UpgradeManager rewards;
    public void BindScene(HUDManager sceneHud, UpgradeManager sceneRewards)
    { hud = sceneHud; rewards = sceneRewards; }

    [Header("Spawn settings")]
    [SerializeField] private Transform spawnPoint;

    [Header("Weapon spawn settings")]
    [SerializeField] private string weaponPointName = "WeaponPoint";

    [SerializeField] private MetaUpgradeApplier metaUpgradeApplier;
    [SerializeField] private UpgradeApplier upgradeApplier;

    [Header("Default weapon for direct MVP launch")]
    [SerializeField] private WeaponData defaultWeapon;
    [SerializeField] private ControlOnboarding onboardingPrefab;
    private void Start()
    {
        if (!SceneTransitionOverlay.IsTransitioning) Time.timeScale = 1f;

        GameObject player = SpawnCharacter();

        if (player == null)
            return;

        // Publish the spawned player before camera readiness; tag lookup cooldown uses paused game time.
        PlayerRuntimeReference.Bind(player);
        hud?.BindPlayer(player);

        BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>(true);

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

        var orbital = OrbitalStationRuntime.Ensure(player, SpawnedCharacterData);
        Station = orbital;
        rewards?.BindOrbitalStation(orbital);
        if (onboardingPrefab != null && orbital != null && orbital.IsInitialized)
            Instantiate(onboardingPrefab, player.transform.position, Quaternion.identity)
                .Bind(player.GetComponent<CharacterMovement2D>(), orbital.GetComponent<OrbitalCenterShift>());

        SpawnedPlayer = player;
        CharacterSpawned?.Invoke(player);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Debug/Start default run if missing")]
    public void DebugStartDefaultRunIfMissing()
    {
        if (!Application.isPlaying)
            return;
        RunStateManager manager = RunStateManager.EnsureExists();
        if (manager.OrbitalStationState != null)
            return;
        Debug.Log("[CharacterSpawner] Explicit development bootstrap: default ORBITAL state for direct scene launch.", this);
        manager.DebugResetOrbitalRunState();
        RunEndService.Instance?.DebugBindRun(manager.RunId);
        GameOverManager.Instance?.DebugBindRun(manager.RunId);
        RunFlowController.Instance?.DebugBindRun(manager.RunId);
        FindFirstObjectByType<PauseMenuUI>()?.DebugBindRun(manager.RunId);
        if (SpawnedPlayer != null)
        {
            OrbitalStationRuntime station = SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>(true);
            if (station != null) station.RebuildRuntimeFromState();
            else station = OrbitalStationRuntime.Ensure(SpawnedPlayer, SpawnedCharacterData);
            Station = station;
            rewards?.BindOrbitalStation(station);
            CharacterSpawned?.Invoke(SpawnedPlayer);
        }
    }
#endif

    private GameObject SpawnCharacter()
    {
        CharacterData selectedCharacter = GetSelectedCharacter();

        if (selectedCharacter == null)
        {
            Debug.LogError("[CharacterSpawner] No selected/default character.");
            return null;
        }

        if (!selectedCharacter.HasValidProductionIdentity)
        {
            Debug.LogError(
                $"[CharacterSpawner] Invalid production CharacterData '{selectedCharacter.name}'. " +
                "Check CharacterId, production prefab, portrait, gameplay icon and orbital path.",
                selectedCharacter);
            return null;
        }

        SpawnedCharacterData = selectedCharacter;

        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;

        GameObject player = Instantiate(
            selectedCharacter.ProductionPrefab,
            spawnPosition,
            Quaternion.identity,
            transform
        );

        player.tag = "Player";

        PlayerLoadoutFactory.ApplyCharacterStats(player, selectedCharacter);

        SetPrimaryWeapon(null);
        PlayerWeaponOrbitVisual orbitVisual =
            player.GetComponent<PlayerWeaponOrbitVisual>();
        if (orbitVisual != null)
            orbitVisual.enabled = false;

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

        BaseWeapon current = FindDebugPrimaryWeapon(player);

        if (current != null && current.weaponData == weaponData)
        {
            replacement = current;
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
