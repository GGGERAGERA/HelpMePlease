using System.Collections.Generic;
using UnityEngine;

public class LaserSword : BaseWeapon
{
    [Header("Sword Settings")]
    public float moveSpeed = 10f;
    public float returnSpeed = 12f;
    public float maxDistanceFromPlayer = 3f;

    private Transform player;
    private Vector3 targetPosition;
    private bool isAttacking;
    private HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();

    protected override void Start()
    {
        base.Start();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        targetPosition = transform.position;
    }

    protected override void Update()
    {
        base.Update();

        if (Time.timeScale == 0f)
            return;

        if (player == null)
            return;

        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            Attack();
        }

        MoveSword();
    }

    public override void Attack()
    {
        if (player == null)
            return;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        Vector3 direction = (mousePosition - player.position).normalized;

        targetPosition = player.position + direction * Mathf.Min(GetRange(), maxDistanceFromPlayer);

        isAttacking = true;
        hitEnemies.Clear();

        if (weaponData != null)
        {
            PlaySound(weaponData.attackSound);
        }

        MarkAttackTime();
    }

    private void MoveSword()
    {
        if (isAttacking)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                isAttacking = false;
            }
        }
        else
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.position,
                returnSpeed * Time.deltaTime
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        if (hitEnemies.Contains(enemyHealth))
            return;

        hitEnemies.Add(enemyHealth);

        enemyHealth.TakeDamage(GetDamage(), transform.position);
    }
}