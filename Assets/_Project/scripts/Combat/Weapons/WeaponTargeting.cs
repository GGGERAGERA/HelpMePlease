using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponTargeting : MonoBehaviour
{
    private const int TargetBufferSize = 64;

    [SerializeField] private LayerMask enemyMask;
    [SerializeField, Min(0f)] private float targetingRadius = 8f;
    [SerializeField, Min(0.01f)] private float targetRefreshInterval = 0.1f;
    [SerializeField] private bool keepTargetWhileValid = true;

    private readonly Collider2D[] targetBuffer = new Collider2D[TargetBufferSize];
    private readonly HashSet<EnemyHealth> uniqueEnemies = new HashSet<EnemyHealth>();

    private BaseWeapon weapon;
    private EnemyHealth currentTarget;
    private ContactFilter2D targetFilter;
    private float nextTargetRefreshTime;

    public bool HasTarget => TryGetTarget(out _);
    public Transform CurrentTarget =>
        TryGetTarget(out Transform target) ? target : null;

    private void Awake()
    {
        weapon = GetComponent<BaseWeapon>();
        ConfigureTargetFilter();
    }

    private void OnEnable()
    {
        nextTargetRefreshTime = 0f;
    }

    private void Update()
    {
        if (Time.timeScale == 0f || Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.01f, targetRefreshInterval);
        RefreshTarget();
    }

    public bool TryGetTarget(out Transform target)
    {
        if (!IsValidTarget(currentTarget))
            currentTarget = null;

        target = currentTarget != null ? currentTarget.transform : null;
        return target != null;
    }

    private void RefreshTarget()
    {
        if (keepTargetWhileValid && IsValidTarget(currentTarget))
            return;

        currentTarget = null;

        Vector2 origin = GetTargetingOrigin();
        int hitCount = Physics2D.OverlapCircle(
            origin,
            targetingRadius,
            targetFilter,
            targetBuffer
        );

        uniqueEnemies.Clear();
        float nearestDistanceSquared = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D targetCollider = targetBuffer[i];
            targetBuffer[i] = null;

            if (targetCollider == null)
                continue;

            EnemyHealth enemy = targetCollider.GetComponentInParent<EnemyHealth>();

            if (!uniqueEnemies.Add(enemy) || !IsValidTarget(enemy))
                continue;

            float distanceSquared =
                ((Vector2)enemy.transform.position - origin).sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            currentTarget = enemy;
        }
    }

    private bool IsValidTarget(EnemyHealth enemy)
    {
        if (enemy == null ||
            enemy.IsDead ||
            !enemy.gameObject.activeInHierarchy)
        {
            return false;
        }

        float radiusSquared = targetingRadius * targetingRadius;
        return ((Vector2)enemy.transform.position - GetTargetingOrigin()).sqrMagnitude
            <= radiusSquared;
    }

    private Vector2 GetTargetingOrigin()
    {
        if (weapon == null)
            weapon = GetComponent<BaseWeapon>();

        Transform weaponOwner = weapon != null ? weapon.Owner : null;
        return weaponOwner != null
            ? (Vector2)weaponOwner.position
            : (Vector2)transform.position;
    }

    private void ConfigureTargetFilter()
    {
        targetFilter = new ContactFilter2D();
        targetFilter.SetLayerMask(enemyMask);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetTargetingOrigin(), targetingRadius);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        targetingRadius = Mathf.Max(0f, targetingRadius);
        targetRefreshInterval = Mathf.Max(0.01f, targetRefreshInterval);
        ConfigureTargetFilter();
    }
#endif
}
