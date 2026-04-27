using UnityEngine;
using System.Collections.Generic;

public class LaserSword : MonoBehaviour
{
    [Header("Movement")]
    public float followSpeed = 30f;
    public float rotationSpeed = 720f;

    [Header("Combat")]
    public int damage = 30;
    public float attackCooldown = 0.5f;
    private float lastAttackTime;
    private HashSet<EnemyHealth> recentlyHit = new HashSet<EnemyHealth>();

    [Header("Effects")]
    public ParticleSystem slashEffect;
    public AudioClip slashSound;

    private Camera cam;
    private Vector2 targetPosition;
    private Rigidbody2D rb;

    void Start()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        targetPosition = transform.position;
    }

    void Update()
    {
        // Получить позицию мыши
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
        {
            Vector3 worldMousePos = cam.ScreenToWorldPoint(mousePos);
            worldMousePos.z = 0;
            targetPosition = worldMousePos;
        }

        // Весь код перемещения и поворота перенесём в FixedUpdate
    }

    void FixedUpdate()
    {
        // Движение
        Vector2 newPosition = Vector2.Lerp(rb.position, targetPosition, followSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);

        // Поворот (правильный способ через угол)
        Vector2 direction = (targetPosition - rb.position).normalized;
        if (direction.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotationSpeed * Time.fixedDeltaTime);
            rb.rotation = newAngle;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Enemy") && Time.time >= lastAttackTime + attackCooldown)
        {
            EnemyHealth enemy = collision.collider.GetComponent<EnemyHealth>();
            if (enemy != null && !recentlyHit.Contains(enemy))
            {
                recentlyHit.Add(enemy);
                lastAttackTime = Time.time;
                enemy.TakeDamage(damage);

                if (slashEffect != null)
                {
                    var effect = Instantiate(slashEffect, transform.position, Quaternion.identity);
                    Destroy(effect.gameObject, 1f);
                }
                if (slashSound != null)
                    AudioSource.PlayClipAtPoint(slashSound, transform.position);

                Invoke(nameof(ClearHitList), 0.2f);
            }
        }
    }

    void ClearHitList() => recentlyHit.Clear();
}