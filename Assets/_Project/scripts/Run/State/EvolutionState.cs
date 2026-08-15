using System.Collections.Generic;

public static class EvolutionResolver
{
    public static EvolutionRecipe Resolve(
        WeaponData weapon,
        AnomalyInventory anomalyInventory,
        IReadOnlyList<EvolutionRecipe> recipes)
    {
        if (weapon == null || anomalyInventory == null || recipes == null)
            return null;

        for (int i = 0; i < recipes.Count; i++)
        {
            EvolutionRecipe recipe = recipes[i];
            if (recipe != null && recipe.Matches(weapon, anomalyInventory))
                return recipe;
        }

        return null;
    }
}

/// <summary>
/// Derived run state. Evolution occupies neither an upgrade slot nor the
/// anomaly slot; it is resolved from the selected weapon and anomaly data.
/// </summary>
public sealed class EvolutionState
{
    public EvolutionRecipe CurrentRecipe { get; private set; }
    public EvolutionDefinition CurrentEvolution => CurrentRecipe != null
        ? CurrentRecipe.Evolution
        : null;
    public bool HasEvolution => CurrentEvolution != null;

    public void Refresh(
        WeaponData weapon,
        AnomalyInventory anomalyInventory,
        IReadOnlyList<EvolutionRecipe> recipes)
    {
        CurrentRecipe = EvolutionResolver.Resolve(
            weapon, anomalyInventory, recipes);
    }

    public void Clear()
    {
        CurrentRecipe = null;
    }
}
