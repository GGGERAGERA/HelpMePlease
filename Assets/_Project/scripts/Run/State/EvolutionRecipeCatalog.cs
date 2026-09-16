using UnityEngine;

public static class EvolutionRecipeCatalog
{

    private static EvolutionRecipe[] recipes;

    public static void Configure(EvolutionRecipe[] definitions)
    {
        if (definitions == null || definitions.Length == 0) throw new System.ArgumentException("Assign evolution recipes in ProductionSceneComposition.");
        recipes = (EvolutionRecipe[])definitions.Clone();
    }

    public static EvolutionRecipe[] GetAll()
    {
        if (recipes == null)
            throw new System.InvalidOperationException("Evolution recipes are not assigned by ProductionSceneComposition.");

        return (EvolutionRecipe[])recipes.Clone();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        recipes = null;
    }
}
