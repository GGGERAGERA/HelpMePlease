using UnityEngine;

[DisallowMultipleComponent]
public sealed class PistolGravityEvolutionRuntime :
    MonoBehaviour,
    IEvolutionRuntime
{
    private GravityConstruct gravity;

    public EvolutionRuntimeType Type => EvolutionRuntimeType.PistolGravity;
    public bool IsActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        EvolutionRuntimeRegistry.Register(
            EvolutionRuntimeType.PistolGravity,
            owner => owner.GetComponent<PistolGravityEvolutionRuntime>() ??
                owner.AddComponent<PistolGravityEvolutionRuntime>());
    }

    public void Activate(EvolutionDefinition definition, BaseWeapon weapon)
    {
        if (definition == null || weapon == null)
        {
            Deactivate();
            return;
        }

        gravity ??= GetComponent<GravityConstruct>();
        if (gravity == null)
        {
            Debug.LogWarning(
                "[PistolGravityEvolution] Gravity runtime is missing.", this);
            Deactivate();
            return;
        }

        gravity.ConfigureWeaponPayload(
            weapon, definition.PayloadFireRateMultiplier);
        gravity.SetWeaponPayloadEnabled(true);
        IsActive = true;
    }

    public void Deactivate()
    {
        if (gravity != null)
            gravity.SetWeaponPayloadEnabled(false);

        IsActive = false;
    }

    private void OnDestroy()
    {
        Deactivate();
    }
}
