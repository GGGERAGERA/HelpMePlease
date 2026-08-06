using UnityEngine;

public class ExperiencePickup : MonoBehaviour, IAnomalySpeedPickup
{
    [Header("Experience")]
    [SerializeField] private int expValue = 10;

    [Header("Magnet")]
    [SerializeField] private float magnetRadius = 3f;
    [SerializeField] private float magnetSpeed = 10f;
    [SerializeField] private float collectDistance = 0.25f;

    [Header("Sound (Legacy)")]
#pragma warning disable CS0414
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float pickupVolume = 0.25f;
#pragma warning restore CS0414



    private Transform player;
    private bool isCollected;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();

    public Component PickupComponent => this;
    public float AnomalySpeedMultiplier => anomalySpeed.Value;

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

        PlayerPickupRadius pickupRadius = player.GetComponent<PlayerPickupRadius>();

        float currentMagnetRadius = magnetRadius;

        if (pickupRadius != null)
        {
            currentMagnetRadius = pickupRadius.CurrentRadius;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= currentMagnetRadius)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                magnetSpeed * anomalySpeed.Value * Time.deltaTime
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

        AudioService.Instance?.PlayAt(
            AudioCueId.XPPickup,
            transform.position
        );

        Destroy(gameObject);
    }

    public void SetAnomalySpeedMultiplier(Object source, float multiplier)
    {
        anomalySpeed.Set(source, multiplier);
    }

    public void RemoveAnomalySpeedMultiplier(Object source)
    {
        anomalySpeed.Remove(source);
    }

    private void OnDisable()
    {
        AnomalySpeedPickupLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
    }
}
