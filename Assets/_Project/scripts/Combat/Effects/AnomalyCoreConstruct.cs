using UnityEngine;

public enum AnomalyCoreId
{
    Gravity = 0,
    Rift = 1
}

public interface IAnomalyWeaponPayload
{
    bool WeaponPayloadEnabled { get; }
    void SetWeaponPayloadEnabled(bool enabled);
}

public abstract class AnomalyCoreConstruct : MonoBehaviour
{
    public abstract void Configure(
        Transform anchor,
        BaseWeapon optionalWeapon);

    public virtual void Shutdown()
    {
        enabled = false;
    }
}
