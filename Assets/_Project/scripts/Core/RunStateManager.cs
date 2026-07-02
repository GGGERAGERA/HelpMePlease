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

    public LevelNodeData SelectedLevelNode { get; private set; }

    private readonly List<UpgradeData> pickedUpgrades = new();

    private bool hasHealthSnapshot;
    private float savedCurrentHealth;
    private float savedMaxHealth;

    private bool hasExperienceSnapshot;
    private int savedXpLevel = 1;
    private int savedCurrentExp;

    private bool upgradesAppliedToCurrentScene;

    public IReadOnlyList<UpgradeData> PickedUpgrades => pickedUpgrades;

    public static RunStateManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("RunStateManager");
        return go.AddComponent<RunStateManager>();
    }

    public void SetSelectedLevelNode(LevelNodeData node)
    {
        SelectedLevelNode = node;
        Debug.Log($"[RunState] Selected level node: {(node != null ? node.nodeName : "NULL")}");
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
        SelectedLevelNode = null;
        SelectedCharacter = character;
        SelectedWeapon = weapon;
        CurrentLevel = 1;

        pickedUpgrades.Clear();
        ClearExperienceSnapshot();
        ClearHealthSnapshot();

        Debug.Log($"[RunState] New run: character={GetName(character)}, weapon={GetName(weapon)}");
    }

    public void SavePlayerState(GameObject player)
    {
        upgradesAppliedToCurrentScene = false;
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

    public void ApplyToSpawnedPlayer(GameObject player, UpgradeApplier upgradeApplier)
    {
        if (player == null)
            return;

        if (!upgradesAppliedToCurrentScene)
        {
            if (upgradeApplier != null)
            {
                foreach (UpgradeData upgrade in pickedUpgrades)
                    upgradeApplier.Apply(upgrade);
            }
            else if (pickedUpgrades.Count > 0)
            {
                Debug.LogWarning("[RunState] UpgradeApplier not assigned. Upgrades were not restored.");
            }

            upgradesAppliedToCurrentScene = true;
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

    public void SaveExperienceState()
    {
        ExperienceManager experience = ExperienceManager.Instance;

        if (experience == null)
        {
            Debug.LogWarning("[RunState] XP save skipped: ExperienceManager not found.");
            return;
        }

        savedXpLevel = experience.CurrentLevel;
        savedCurrentExp = experience.CurrentExp;
        hasExperienceSnapshot = true;

        Debug.Log($"[RunState] Saved XP: level={savedXpLevel}, exp={savedCurrentExp}");
    }

    public void ApplyToExperienceManager(ExperienceManager experience)
    {
        if (experience == null)
            return;

        if (!hasExperienceSnapshot)
            return;

        experience.RestoreRuntimeExperience(savedXpLevel, savedCurrentExp);

        Debug.Log($"[RunState] Applied XP: level={savedXpLevel}, exp={savedCurrentExp}");
    }

    private void ClearExperienceSnapshot()
    {
        hasExperienceSnapshot = false;
        savedXpLevel = 1;
        savedCurrentExp = 0;
    }
    public void RegisterUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        pickedUpgrades.Add(upgrade);

        Debug.Log($"[RunState] Registered upgrade: {upgrade.upgradeName}. Total: {pickedUpgrades.Count}");
    }
}
