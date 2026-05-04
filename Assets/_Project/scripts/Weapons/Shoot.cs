using UnityEngine;

public class Shoot : BaseWeapon
{
    [Header("Shoot Settings")]
    public GameObject bulletPrefab;

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

    public override void Attack()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("Shoot: bulletPrefab is not assigned.");
            return;
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }

        Vector2 direction = GetShootDirection();

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.identity
        );

        Bullet bulletScript = bullet.GetComponent<Bullet>();

        if (bulletScript != null)
        {
            bulletScript.Initialize(GetDamage(), GetRange(), direction);
        }

        if (weaponData != null)
        {
            PlaySound(weaponData.attackSound);
        }

        MarkAttackTime();
    }

    private Vector2 GetShootDirection()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        return (mousePosition - firePoint.position).normalized;
    }
}