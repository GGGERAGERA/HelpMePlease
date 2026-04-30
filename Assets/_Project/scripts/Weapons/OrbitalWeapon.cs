using UnityEngine;

public class OrbitalWeapon : MonoBehaviour
{
    [Header("Movement")]
    public Transform player;
    public float orbitRadius = 1.2f;
    public float smoothSpeed = 10f;

    [Header("Visual Effects")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem laserBeam;

    [Header("Combat")]
    public float laserRange = 10f;
    public LayerMask enemyLayer;

    [Header("Audio")]
    public AudioClip laserSound;
    public float soundVolume = 0.7f;

    private Camera cam;
    private Vector3 targetPosition;
    private AudioSource audioSource;

    void Start()
    {
        cam = Camera.main;
        targetPosition = transform.position;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (cam == null) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x < 0 || mousePos.x > Screen.width || mousePos.y < 0 || mousePos.y > Screen.height)
            return;

        Vector3 worldMousePos = cam.ScreenToWorldPoint(mousePos);
        worldMousePos.z = 0;
        Vector2 direction = (worldMousePos - player.position).normalized;

        Vector3 wantedPos = player.position + (Vector3)direction * orbitRadius;
        targetPosition = wantedPos;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (Input.GetMouseButtonDown(0))
        {
            Shoot(direction);
        }
    }

    void Shoot(Vector2 direction)
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (laserBeam != null) laserBeam.Play();

        if (laserSound != null && audioSource != null)
            audioSource.PlayOneShot(laserSound, soundVolume);

        Vector2 origin = transform.position;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, laserRange, enemyLayer);

        int damage = PlayerStats.Instance != null ? PlayerStats.Instance.GetDamage() : 20;

        foreach (RaycastHit2D hit in hits)
        {
            EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                CameraShake.Instance?.Shake(0.1f, 0.01f); // слабая тряска при ударе по врагу
            }
                
        }
    }
}