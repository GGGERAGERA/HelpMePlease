using UnityEngine;

public static class AnomalyItemCatalog
{
    private const string ResourcePath = "RunBuild/AnomalyItems";
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

    private static void EnsureLoaded()
    {
        if (items == null)
            items = Resources.LoadAll<AnomalyItemData>(ResourcePath);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        items = null;
    }
}
