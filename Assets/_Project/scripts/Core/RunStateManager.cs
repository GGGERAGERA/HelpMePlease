using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent state of the current run. Stores data, not scene objects.
/// New gameplay scenes can be reloaded safely, then this state is applied to the newly spawned player.
/// </summary>
public sealed class RunStateManager : MonoBehaviour
{
    public static RunStateManager Instance { get; private set; }

    public CharacterData SelectedCharacter { get; private set; }
    public WeaponData SelectedWeapon { get; private set; }
    public int CurrentLevel { get; private set; } = 1;

    private readonly List<UpgradeData> pickedUpgrades = new();

    private bool hasHealthSnapshot;
    private float savedCurrentHealth;
    private float savedMaxHealth;

    public IReadOnlyList<UpgradeData> PickedUpgrades => pickedUpgrades;

    public static RunStateManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("RunStateManager");
        return go.AddComponent<RunStateManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginNewRun(CharacterData character, WeaponData weapon)
    {
        SelectedCharacter = character;
        SelectedWeapon = weapon;
        CurrentLevel = 1;

        pickedUpgrades.Clear();
        ClearHealthSnapshot();

        Debug.Log($"[RunState] New run: character={GetName(character)}, weapon={GetName(weapon)}");
    }

    public void RegisterUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        pickedUpgrades.Add(upgrade);
        Debug.Log($"[RunState] Registered upgrade: {upgrade.name}. Total: {pickedUpgrades.Count}");
    }

    public void SavePlayerState(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("[RunState] Save skipped: player is null.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();

        if (health == null)
        {
            Debug.LogWarning("[RunState] Save skipped: PlayerHealth not found.");
            return;
        }

        savedCurrentHealth = health.CurrentHealth;
        savedMaxHealth = health.MaxHealth;
        hasHealthSnapshot = true;

        Debug.Log($"[RunState] Saved player health: {savedCurrentHealth}/{savedMaxHealth}");
    }

    public void AdvanceLevel()
    {
        CurrentLevel++;
        Debug.Log($"[RunState] Advanced to level {CurrentLevel}");
    }

    public void ApplyToSpawnedPlayer(GameObject player)
    {
        if (player == null)
            return;

        UpgradeApplier applier = FindFirstObjectByType<UpgradeApplier>();

        if (applier != null)
        {
            foreach (UpgradeData upgrade in pickedUpgrades)
                applier.Apply(upgrade);
        }
        else if (pickedUpgrades.Count > 0)
        {
            Debug.LogWarning("[RunState] UpgradeApplier not found. Upgrades were not restored.");
        }

        if (hasHealthSnapshot)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();

            if (health != null)
                health.SetRuntimeHealth(savedMaxHealth, savedCurrentHealth);
        }

        Debug.Log($"[RunState] Applied to spawned player. Upgrades: {pickedUpgrades.Count}, hasHealth: {hasHealthSnapshot}");
    }

    private void ClearHealthSnapshot()
    {
        hasHealthSnapshot = false;
        savedCurrentHealth = 0f;
        savedMaxHealth = 0f;
    }

    private string GetName(Object obj)
    {
        return obj != null ? obj.name : "NULL";
    }
}
