using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponCoreEnemyEffects : MonoBehaviour
{
    private float nextChainTime;
    private float nextVoidTime;

    public static WeaponCoreEnemyEffects GetOrCreate(EnemyHealth enemy)
    {
        if (enemy == null)
            return null;

        WeaponCoreEnemyEffects result =
            enemy.GetComponent<WeaponCoreEnemyEffects>();

        if (result == null)
            result = enemy.gameObject.AddComponent<WeaponCoreEnemyEffects>();

        return result;
    }

    public bool TryBeginChainCooldown(float cooldown)
    {
        if (Time.time < nextChainTime)
            return false;

        nextChainTime = Time.time + Mathf.Max(0f, cooldown);
        return true;
    }

    public bool TryBeginVoidCooldown(float cooldown)
    {
        if (Time.time < nextVoidTime)
            return false;

        nextVoidTime = Time.time + Mathf.Max(0f, cooldown);
        return true;
    }

    private void OnDisable()
    {
        nextChainTime = 0f;
        nextVoidTime = 0f;
    }
}
