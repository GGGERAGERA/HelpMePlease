using UnityEngine;

public class ExperiencePickup : MonoBehaviour, IAnomalySpeedPickup,
    IAnomalyExternalVelocity
{
    public static event System.Action<ExperiencePickup> Spawned;
    public static event System.Action<ExperiencePickup> Collected;
    [Header("Experience")]
    [SerializeField] private int expValue = 10;
    [SerializeField] private ExperiencePickupVisual visual;

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
    protected PlayerHealth PlayerHealth { get; private set; }
    protected virtual bool GrantsExperience => true;
    protected virtual bool CanCollect => true;
    private bool isCollected;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    public Component PickupComponent => this;
    public Component ExternalVelocityComponent => this;
    public float AnomalySpeedMultiplier => anomalySpeed.Value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly System.Collections.Generic.HashSet<ExperiencePickup> active = new();
    public static System.Collections.Generic.IReadOnlyCollection<ExperiencePickup> DebugActive => active;
#endif
    protected virtual void OnEnable()
    {
        isCollected = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GrantsExperience) active.Add(this);
#endif
        if (GrantsExperience) Spawned?.Invoke(this);
    }

    protected virtual void Start()
    {
        Transform playerObject = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);

        if (playerObject != null)
        {
            player = playerObject;
            playerPickupRadius =
                playerObject.GetComponent<PlayerPickupRadius>();
            PlayerHealth = playerObject.GetComponent<PlayerHealth>();
        }
    }

    protected virtual void Update()
    {
        if (isCollected || Time.deltaTime <= 0f ||
            (PlayerHealth != null && PlayerHealth.IsDead))
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
        ExperienceManager manager = ExperienceManager.Instance;
        bool globalMagnet = GrantsExperience && manager != null &&
            manager.gameObject.scene == gameObject.scene && manager.IsGlobalMagnetActive;

        if ((distance <= currentMagnetRadius || globalMagnet) && CanCollect)
        {
            float speed = globalMagnet
                ? Mathf.Max(magnetSpeed, manager.GetGlobalMagnetSpeed(distance))
                : magnetSpeed;
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                speed * anomalySpeed.Value * Time.deltaTime
            );
        }

        if (Vector2.Distance(transform.position, player.position) <= collectDistance)
        {
            Collect();
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected || Time.timeScale <= 0f ||
            (PlayerHealth != null && PlayerHealth.IsDead) || !CanCollect)
            return;

        isCollected = true;

        ApplyPickup();
        Despawn();
    }

    protected virtual void ApplyPickup()
    {

        Collected?.Invoke(this);

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

        visual?.PlayPickupFeedback(ExperienceManager.Instance);
    }

    // XP pickups currently use Instantiate/Destroy; only their feedback FX is pooled.
    // Clearing must suppress pickup callbacks until deferred destruction completes.
    public void Despawn()
    {
        isCollected = true;
        gameObject.SetActive(false);
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

    protected virtual void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        active.Remove(this);
#endif
        AnomalySpeedPickupLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
    }
}
