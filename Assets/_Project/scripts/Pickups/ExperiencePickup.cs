using UnityEngine;

public class ExperiencePickup : MonoBehaviour
{
    [Header("Experience")]
    [SerializeField] private int expValue = 10;

    [Header("Magnet")]
    [SerializeField] private float magnetRadius = 3f;
    [SerializeField] private float magnetSpeed = 10f;
    [SerializeField] private float collectDistance = 0.25f;

    [Header("Sound")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float pickupVolume = 0.25f;

    private Transform player;
    private bool isCollected;

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void Update()
    {
        if (player == null || isCollected)
            return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= magnetRadius)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                magnetSpeed * Time.deltaTime
            );
        }

        if (distance <= collectDistance)
        {
            Collect();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected)
            return;

        isCollected = true;

        if (ExperienceManager.Instance != null)
        {
            ExperienceManager.Instance.AddExperience(expValue);
        }
        else
        {
            Debug.LogWarning("ExperiencePickup: ExperienceManager не найден.");
        }

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(
                pickupSound,
                transform.position,
                pickupVolume
            );
        }

        Destroy(gameObject);
    }
}
