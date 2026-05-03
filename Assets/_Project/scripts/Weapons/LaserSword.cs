using UnityEngine;
using System.Collections.Generic;

public class LaserSword : MonoBehaviour
{
    [Header("Movement")]
    public float followSpeed = 30f;
    public float rotationSpeed = 720f;

    [Header("Combat")]
    public float attackCooldown = 0.5f;
    private float lastAttackTime;
    private HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();

    [Header("Effects")]
    public ParticleSystem slashEffect;
    public AudioClip slashSound;
    public float soundVolume = 0.7f;

    private Camera cam;
    private Vector2 targetPosition;
    private Rigidbody2D rb;
    private AudioSource audioSource;

    public WeaponData weaponData;

    void Start()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        targetPosition = transform.position;
    }

    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
        {
            Vector3 worldMousePos = cam.ScreenToWorldPoint(mousePos);
            worldMousePos.z = 0;
            targetPosition = worldMousePos;
        }
    }

    void FixedUpdate()
    {
        rb.MovePosition(Vector2.Lerp(rb.position, targetPosition, followSpeed * Time.fixedDeltaTime));

        Vector2 direction = (targetPosition - rb.position).normalized;
        if (direction.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rb.rotation = Mathf.LerpAngle(rb.rotation, targetAngle, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") && Time.time >= lastAttackTime + attackCooldown)
        {
            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy != null && !hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);
                lastAttackTime = Time.time;

                int damage = weaponData != null ? weaponData.damage : 30;
                enemy.TakeDamage(damage);

                if (slashEffect != null)
                {
                    var effect = Instantiate(slashEffect, transform.position, Quaternion.identity);
                    Destroy(effect.gameObject, 0.5f);
                }
                if (slashSound != null && audioSource != null)
                    audioSource.PlayOneShot(slashSound, soundVolume);

                Invoke(nameof(ClearHitList), 0.2f);
            }
        }
    }

    void ClearHitList() => hitEnemies.Clear();
}