using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{

    [Header("Unlock")]
    public UnlockableContentData unlockData;

    [Header("Identity")]
    public string characterName;

    public Subject42.Combat.OrbitalStation.OrbitalPathType orbitalPath =
        Subject42.Combat.OrbitalStation.OrbitalPathType.Circle;

    [Header("Localized Presentation")]
    public string nameKey;
    public string orbitalPatternKey;
    public string traitKey;
    public string descriptionKey;

    public string LocalizedName => LocalizationService.EnsureExists().Get(nameKey);
    public string LocalizedCombatTypeDisplayName => LocalizationService.EnsureExists().Get(orbitalPatternKey);
    public string LocalizedCombatTypeDescription => LocalizationService.EnsureExists().Get(traitKey);
    public string LocalizedDescription => LocalizationService.EnsureExists().Get(descriptionKey);

    [Header("Combat")]
    public CharacterCombatType combatType = CharacterCombatType.AutoFire;

    public Sprite portrait;

    [Header("Prefabs")]
    public GameObject characterPrefab;

    [Header("Base Character Stats")]
    public int maxHealth = 100;
    public float moveSpeed = 5f;

}
