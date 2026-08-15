using UnityEngine;

[CreateAssetMenu(
    fileName = "New EvolutionRecipe",
    menuName = "Game/Run Build/Evolution Recipe")]
public sealed class EvolutionRecipe : ScriptableObject
{
    [SerializeField] private WeaponData weapon;
    [SerializeField] private AnomalyItemData anomalyItem;
    [SerializeField, Range(1, 3)] private int requiredAnomalyLevel = 3;
    [SerializeField] private EvolutionDefinition evolution;

    public WeaponData Weapon => weapon;
    public AnomalyItemData AnomalyItem => anomalyItem;
    public int RequiredAnomalyLevel => Mathf.Clamp(requiredAnomalyLevel, 1, 3);
    public EvolutionDefinition Evolution => evolution;

    public bool Matches(
        WeaponData candidateWeapon,
        AnomalyInventory inventory)
    {
        return candidateWeapon != null && weapon == candidateWeapon &&
            inventory != null && !inventory.IsEmpty &&
            anomalyItem != null && anomalyItem.Matches(inventory.CurrentItem) &&
            inventory.Level >= RequiredAnomalyLevel && evolution != null;
    }
}
