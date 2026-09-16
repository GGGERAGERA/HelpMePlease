using UnityEngine;

public enum CharacterId
{
    None = 0,
    Gera = 1,
    DiMag = 2,
    Vika = 3
}

[CreateAssetMenu(fileName = "New Character", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{

    [Header("Unlock")]
    public UnlockableContentData unlockData;

    [Header("Identity")]
    [SerializeField] private CharacterId characterId;
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
    [SerializeField] private Sprite gameplayIcon;

    [Header("Prefabs")]
    [SerializeField] private GameObject productionPrefab;

    [Header("Base Character Stats")]
    public int maxHealth = 100;
    public float moveSpeed = 5f;

    public CharacterId Id => characterId;
    public Sprite Portrait => portrait;
    public Sprite GameplayIcon => gameplayIcon;
    public GameObject ProductionPrefab => productionPrefab;

    public bool HasValidProductionIdentity =>
        characterId != CharacterId.None &&
        System.Enum.IsDefined(typeof(CharacterId), characterId) &&
        System.Enum.IsDefined(
            typeof(Subject42.Combat.OrbitalStation.OrbitalPathType),
            orbitalPath) &&
        ProductionPrefab != null &&
        Portrait != null &&
        gameplayIcon != null;

}
