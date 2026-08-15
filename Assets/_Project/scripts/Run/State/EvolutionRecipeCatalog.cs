using UnityEngine;

public static class EvolutionRecipeCatalog
{
    private const string ResourcePath = "RunBuild/Evolutions";
    private static EvolutionRecipe[] recipes;

    public static EvolutionRecipe[] GetAll()
    {
        if (recipes == null)
            recipes = Resources.LoadAll<EvolutionRecipe>(ResourcePath);

        return (EvolutionRecipe[])recipes.Clone();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        recipes = null;
    }
}
