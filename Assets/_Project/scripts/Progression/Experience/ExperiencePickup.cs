using UnityEngine;

public class ExperiencePickup : MonoBehaviour, IAnomalySpeedPickup,
    IAnomalyExternalVelocity
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
    private PlayerPickupRadius playerPickupRadius;
    private bool isCollected;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    public Component PickupComponent => this;
    public Component ExternalVelocityComponent => this;
    public float AnomalySpeedMultiplier => anomalySpeed.Value;

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
            playerPickupRadius =
                playerObject.GetComponent<PlayerPickupRadius>();
        }
    }

    private void Update()
    {
        if (isCollected)
            return;

        transform.position += (Vector3)(
            anomalyExternalVelocity.Value * Time.deltaTime
        );

        if (player == null)
            return;

        float currentMagnetRadius = magnetRadius;

        if (playerPickupRadius != null)
            currentMagnetRadius = playerPickupRadius.CurrentRadius;

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

    public void SetAnomalyExternalVelocity(
        Object source,
        Vector2 velocity)
    {
        anomalyExternalVelocity.Set(source, velocity);
    }

    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyExternalVelocity.Remove(source);
    }

    private void OnDisable()
    {
        AnomalySpeedPickupLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
    }
}
