using UnityEngine;

public abstract class AnomalyEvolutionRuntimeBase :
    MonoBehaviour,
    IEvolutionRuntime
{
    private IAnomalyEvolutionPower power;

    public abstract EvolutionRuntimeType Type { get; }
    protected abstract AnomalyPowerType PowerType { get; }
    public bool IsActive { get; private set; }

    public void Activate(
        EvolutionDefinition definition,
        BaseWeapon weapon,
        int anomalyLevel)
    {
        if (definition == null || weapon == null || anomalyLevel < 2)
        {
            Deactivate();
            return;
        }

        AnomalyPowerRuntime.EnsurePower(gameObject, PowerType, anomalyLevel);
        power = FindEvolutionPower(PowerType);
        if (power == null)
        {
            Debug.LogWarning(
                $"[EvolutionRuntime] '{PowerType}' does not expose the " +
                "evolution payload contract.",
                this);
            Deactivate();
            return;
        }

        power.ConfigureEvolutionPayload(weapon, definition, anomalyLevel);
        IsActive = true;
    }

    public void Deactivate()
    {
        power?.DisableEvolutionPayload();
        power = null;
        IsActive = false;
    }

    private IAnomalyEvolutionPower FindEvolutionPower(
        AnomalyPowerType expectedType)
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAnomalyPowerRuntime anomaly &&
                anomaly.Type == expectedType &&
                behaviours[i] is IAnomalyEvolutionPower evolutionPower)
            {
                return evolutionPower;
            }
        }

        return null;
    }

    protected virtual void OnDestroy()
    {
        Deactivate();
    }
}

[DisallowMultipleComponent]
public sealed class GravityEvolutionRuntime : AnomalyEvolutionRuntimeBase
{
    public override EvolutionRuntimeType Type =>
        EvolutionRuntimeType.GravityHybrid;
    protected override AnomalyPowerType PowerType =>
        AnomalyPowerType.GravityOrb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        EvolutionRuntimeRegistry.Register(
            EvolutionRuntimeType.GravityHybrid,
            owner => owner.GetComponent<GravityEvolutionRuntime>() ??
                owner.AddComponent<GravityEvolutionRuntime>());
    }
}

[DisallowMultipleComponent]
public sealed class ArcEvolutionRuntime : AnomalyEvolutionRuntimeBase
{
    public override EvolutionRuntimeType Type =>
        EvolutionRuntimeType.ArcHybrid;
    protected override AnomalyPowerType PowerType =>
        AnomalyPowerType.ArcNode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        EvolutionRuntimeRegistry.Register(
            EvolutionRuntimeType.ArcHybrid,
            owner => owner.GetComponent<ArcEvolutionRuntime>() ??
                owner.AddComponent<ArcEvolutionRuntime>());
    }
}

[DisallowMultipleComponent]
public sealed class BeamEvolutionRuntime : AnomalyEvolutionRuntimeBase
{
    public override EvolutionRuntimeType Type =>
        EvolutionRuntimeType.BeamHybrid;
    protected override AnomalyPowerType PowerType =>
        AnomalyPowerType.RedBeam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        EvolutionRuntimeRegistry.Register(
            EvolutionRuntimeType.BeamHybrid,
            owner => owner.GetComponent<BeamEvolutionRuntime>() ??
                owner.AddComponent<BeamEvolutionRuntime>());
    }
}
