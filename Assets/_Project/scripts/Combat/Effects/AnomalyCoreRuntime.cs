using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnomalyCoreRuntime : MonoBehaviour
{
    private readonly Dictionary<AnomalyCoreId, AnomalyCoreConstruct>
        activeConstructs = new();

    private CharacterSpawner characterSpawner;
    private BaseWeapon currentWeapon;

    public BaseWeapon CurrentWeapon => currentWeapon;

    public void Initialize(
        CharacterSpawner spawner,
        BaseWeapon primaryWeapon)
    {
        if (characterSpawner != spawner)
        {
            UnsubscribeFromWeaponChanges();
            characterSpawner = spawner;

            if (characterSpawner != null)
            {
                characterSpawner.PrimaryWeaponChanged +=
                    HandlePrimaryWeaponChanged;
            }
        }

        HandlePrimaryWeaponChanged(primaryWeapon);
    }

    public bool ActivateCore(AnomalyCoreId coreId)
    {
        if (currentWeapon == null)
            return false;

        if (activeConstructs.TryGetValue(
            coreId,
            out AnomalyCoreConstruct existing))
        {
            if (existing != null)
                return false;

            activeConstructs.Remove(coreId);
        }

        AnomalyCoreConstruct construct = CreateConstruct(coreId);
        if (construct == null)
            return false;

        construct.Configure(currentWeapon, transform);
        construct.enabled = true;
        activeConstructs.Add(coreId, construct);
        return true;
    }

    public bool DeactivateCore(AnomalyCoreId coreId)
    {
        if (!activeConstructs.TryGetValue(
            coreId,
            out AnomalyCoreConstruct construct))
        {
            return false;
        }

        activeConstructs.Remove(coreId);

        if (construct == null)
            return false;

        construct.Shutdown();
        Destroy(construct);
        return true;
    }

    public bool IsCoreActive(AnomalyCoreId coreId)
    {
        return activeConstructs.TryGetValue(
            coreId,
            out AnomalyCoreConstruct construct
        ) && construct != null;
    }

    public bool TrySetWeaponPayloadEnabled(
        AnomalyCoreId coreId,
        bool enabled)
    {
        if (!activeConstructs.TryGetValue(
            coreId,
            out AnomalyCoreConstruct construct) ||
            construct is not IAnomalyWeaponPayload payload)
        {
            return false;
        }

        payload.SetWeaponPayloadEnabled(enabled);
        return true;
    }

    public bool TryGetWeaponPayloadEnabled(
        AnomalyCoreId coreId,
        out bool enabled)
    {
        if (activeConstructs.TryGetValue(
            coreId,
            out AnomalyCoreConstruct construct) &&
            construct is IAnomalyWeaponPayload payload)
        {
            enabled = payload.WeaponPayloadEnabled;
            return true;
        }

        enabled = false;
        return false;
    }

    private AnomalyCoreConstruct CreateConstruct(AnomalyCoreId coreId)
    {
        switch (coreId)
        {
            case AnomalyCoreId.Gravity:
                return gameObject.AddComponent<GravityConstruct>();
            case AnomalyCoreId.Rift:
                return gameObject.AddComponent<RiftConstruct>();
            default:
                Debug.LogWarning(
                    $"[AnomalyCoreRuntime] Unsupported Core: {coreId}.",
                    this
                );
                return null;
        }
    }

    private void HandlePrimaryWeaponChanged(BaseWeapon weapon)
    {
        currentWeapon = weapon;

        foreach (AnomalyCoreConstruct construct in activeConstructs.Values)
        {
            if (construct != null)
                construct.Configure(currentWeapon, transform);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromWeaponChanges();

        foreach (AnomalyCoreConstruct construct in activeConstructs.Values)
        {
            if (construct == null)
                continue;

            construct.Shutdown();
            Destroy(construct);
        }

        activeConstructs.Clear();
    }

    private void UnsubscribeFromWeaponChanges()
    {
        if (characterSpawner != null)
        {
            characterSpawner.PrimaryWeaponChanged -=
                HandlePrimaryWeaponChanged;
        }
    }
}
