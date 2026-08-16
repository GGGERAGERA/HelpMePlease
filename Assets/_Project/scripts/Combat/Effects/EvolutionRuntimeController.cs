using UnityEngine;

[DisallowMultipleComponent]
public sealed class EvolutionRuntimeController : MonoBehaviour
{
    private CharacterSpawner characterSpawner;
    private RunStateManager runState;
    private BaseWeapon currentWeapon;
    private IEvolutionRuntime currentRuntime;

    public IEvolutionRuntime CurrentRuntime => currentRuntime;
    public bool IsRuntimeActive => currentRuntime != null &&
        currentRuntime.IsActive;

    public void Initialize(
        CharacterSpawner spawner,
        BaseWeapon primaryWeapon)
    {
        Unsubscribe();
        characterSpawner = spawner;
        runState = RunStateManager.Instance;
        currentWeapon = primaryWeapon;

        if (characterSpawner != null)
            characterSpawner.PrimaryWeaponChanged += HandleWeaponChanged;

        if (runState != null)
            runState.AnomalyInventory.Changed += Refresh;

        Refresh();
    }

    public void Refresh()
    {
        if (runState == null)
            runState = RunStateManager.Instance;

        if (runState == null)
        {
            DeactivateCurrent();
            return;
        }

        EvolutionRecipe[] recipes = EvolutionRecipeCatalog.GetAll();
        runState.ResolveCurrentEvolution(
            currentWeapon != null ? currentWeapon.weaponData : null,
            recipes);

        EvolutionDefinition definition = runState.CurrentEvolution;
        if (definition == null ||
            definition.RuntimeType == EvolutionRuntimeType.None)
        {
            DeactivateCurrent();
            return;
        }

        if (currentRuntime == null ||
            currentRuntime.Type != definition.RuntimeType)
        {
            RemoveCurrentRuntime();

            if (!EvolutionRuntimeRegistry.TryCreate(
                    gameObject,
                    definition.RuntimeType,
                    out currentRuntime))
            {
                Debug.LogWarning(
                    $"[EvolutionRuntime] No runtime registered for " +
                    $"'{definition.RuntimeType}'.",
                    this);
                return;
            }
        }

        currentRuntime.Activate(
            definition,
            currentWeapon,
            runState.AnomalyInventory.Level);
    }

    private void HandleWeaponChanged(BaseWeapon weapon)
    {
        currentWeapon = weapon;
        Refresh();
    }

    private void DeactivateCurrent()
    {
        currentRuntime?.Deactivate();
    }

    private void RemoveCurrentRuntime()
    {
        DeactivateCurrent();

        if (currentRuntime is MonoBehaviour behaviour)
            Destroy(behaviour);

        currentRuntime = null;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        DeactivateCurrent();
    }

    private void Unsubscribe()
    {
        if (characterSpawner != null)
            characterSpawner.PrimaryWeaponChanged -= HandleWeaponChanged;

        if (runState != null)
            runState.AnomalyInventory.Changed -= Refresh;
    }
}
