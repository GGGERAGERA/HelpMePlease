using System.Collections;
using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Laser FX")]
    [SerializeField] private GameObject laserBeamFxPrefab; // fx_Laser1
    [SerializeField] private GameObject laserHitFxPrefab;  // fx_Laser2
    [SerializeField] private float laserFxLifetime = 0.12f;

    [Header("Laser Hit")]
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private float laserWidth = 0.6f;

    public override void Attack()
    {
        if (!CanAttack())
            return;

        MarkAttackTime();

        Vector2 direction = GetAimDirection();
        FireLaser(direction);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);
    }
    protected override void Update()
    {
        base.Update();

        if (Time.timeScale == 0f)
            return;

        if (Input.GetMouseButton(0) && CanAttack())
        {
            Attack();
        }
    }

    private Vector2 GetAimDirection()
    {
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPosition.z = 0f;

        Vector2 direction = mouseWorldPosition - firePoint.position;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.right;

        return direction.normalized;
    }

    private void FireLaser(Vector2 direction)
    {
        float range = GetRange();
        float damage = GetDamage();

        bool isCritical = RollCritical();

        if (isCritical)
            damage *= GetCritMultiplier();

        Vector2 origin = firePoint.position;
        Vector2 endPoint = origin + direction * range;

        SpawnLaserFx(origin, endPoint, direction);
        DamageEnemies(origin, direction, range, damage, isCritical);
    }

    private void DamageEnemies(Vector2 origin, Vector2 direction, float range, float damage, bool isCritical)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,
            laserWidth * 0.5f,
            direction,
            range,
            enemyLayerMask
        );

        foreach (RaycastHit2D hit in hits)
        {
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                continue;

            enemy.TakeDamage(damage, hit.point, isCritical);
        }
    }

    private void SpawnLaserFx(Vector2 origin, Vector2 endPoint, Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float distance = Vector2.Distance(origin, endPoint);

        if (laserBeamFxPrefab != null)
        {
            GameObject beam = Instantiate(laserBeamFxPrefab, firePoint);

            beam.transform.localPosition = Vector3.zero;
            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localScale = Vector3.one;

            beam.transform.SetParent(null, true);

            beam.transform.position = origin;
            beam.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            beam.transform.localScale = new Vector3(distance, 1f, 1f);

            Destroy(beam, laserFxLifetime);
        }

        if (laserHitFxPrefab != null)
        {
            GameObject hitFx = Instantiate(
                laserHitFxPrefab,
                endPoint,
                Quaternion.identity
            );

            Destroy(hitFx, laserFxLifetime);
        }
    }
}