using UnityEngine;

public static class AnomalyPowerRuntime
{
    public static void ApplyRunLoadout(GameObject player)
    {
        RunStateManager runState = RunStateManager.Instance;

        if (player == null || runState == null)
            return;

        AnomalyInventory inventory = runState.AnomalyInventory;
        if (inventory.IsEmpty)
            return;

        EnsurePower(
            player,
            inventory.CurrentItem.PowerType,
            inventory.Level);
    }

    public static void EnsurePower(
        GameObject player,
        AnomalyPowerType power,
        int level = 1)
    {
        if (player == null)
            return;

        if (TryFindPower(player, power, out IAnomalyPowerRuntime existing))
        {
            existing.SetLevel(level);
            existing.Activate();
            return;
        }

        if (AnomalyPowerRuntimeRegistry.TryCreate(
                player,
                power,
                out IAnomalyPowerRuntime created))
        {
            created.SetLevel(level);
            created.Activate();
            return;
        }

        Debug.LogWarning(
            $"[AnomalyPowerRuntime] No runtime registration for '{power}'.",
            player
        );
    }

    public static bool DeactivatePower(
        GameObject player,
        AnomalyPowerType power)
    {
        if (!TryFindPower(player, power, out IAnomalyPowerRuntime runtime))
            return false;

        runtime.Deactivate();
        return true;
    }

    private static bool TryFindPower(
        GameObject player,
        AnomalyPowerType power,
        out IAnomalyPowerRuntime runtime)
    {
        runtime = null;

        if (player == null)
            return false;

        MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAnomalyPowerRuntime candidate &&
                candidate.Type == power)
            {
                runtime = candidate;
                return true;
            }
        }

        return false;
    }
}
