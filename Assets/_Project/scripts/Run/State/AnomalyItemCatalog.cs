using UnityEngine;

public static class AnomalyItemCatalog
{

    private static AnomalyItemData[] items;

    public static AnomalyItemData Find(AnomalyPowerType powerType)
    {
        EnsureLoaded();

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].PowerType == powerType)
                return items[i];
        }

        return null;
    }

    public static AnomalyItemData[] GetAll()
    {
        EnsureLoaded();
        return (AnomalyItemData[])items.Clone();
    }

    public static void Configure(AnomalyItemData[] definitions)
    {
        if (definitions == null || definitions.Length == 0) throw new System.ArgumentException("Assign anomaly items in ProductionSceneComposition.");
        items = (AnomalyItemData[])definitions.Clone();
    }

    private static void EnsureLoaded()
    {
        if (items == null)
            throw new System.InvalidOperationException("Anomaly items are not assigned by ProductionSceneComposition.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        items = null;
    }
}
