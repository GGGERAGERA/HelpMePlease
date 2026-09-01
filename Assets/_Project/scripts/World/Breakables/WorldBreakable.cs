using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldBreakable : MonoBehaviour,
    ITacticalMapMarkerProvider
{
    private static readonly HashSet<WorldBreakable> Active = new();

    public static event System.Action MarkerStateChanged;
    public static event System.Action<WorldBreakable> Broken;
    public static event System.Action<WorldBreakable> RewardIssued;
    public static IReadOnlyCollection<WorldBreakable> ActiveInstances => Active;

    [Header("Durability")]
    [SerializeField, Min(1f)] private float maxHealth = 40f;

    [Header("Prefab State")]
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject breakFx;
    [SerializeField] private Collider2D intactCollider;
    [SerializeField] private SpriteRenderer[] hitRenderers;

    [Header("Hit Feedback")]
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.06f;
    [SerializeField] private Color hitFlashColor = Color.white;

    [Header("Production Gold Loot")]
    [SerializeField] private WorldBreakableLootProfile lootProfile;
    [SerializeField] private GoldenCoinPickup goldPrefab;
    [SerializeField, Min(0f)] private float nothingWeight = 20f;
    [SerializeField, Min(0f)] private float smallGoldWeight = 55f;
    [SerializeField, Min(0f)] private float largeGoldWeight = 15f;
    [SerializeField, Min(1)] private int smallGoldCount = 1;
    [SerializeField, Min(1)] private int largeGoldCount = 3;
    [SerializeField, Min(1)] private int goldValue = 5;
    [SerializeField, Min(0.1f)] private float lootLifetime = 20f;
    [SerializeField, Min(0.1f)] private float pickupRadius = 2.5f;
    [SerializeField, Min(0.1f)] private float attractionSpeed = 7f;
    [SerializeField, Min(0f)] private float scatterSpeed = 1.8f;
    [SerializeField, Min(0.1f)] private float fadeDuration = 2f;

    private float currentHealth;
    private bool broken;
    private Coroutine hitRoutine;
    private Color[] baseColors;
    private bool eventRewardImproved;
    private bool eventRewardNumericOnly;

    public bool IsBroken => broken;
    public WorldBreakableLootProfile LootProfile => lootProfile;

    public void InitializeEventReward(bool improved, bool numericOnly)
    {
        eventRewardImproved = improved;
        eventRewardNumericOnly = numericOnly;
    }

    private void Awake()
    {
        currentHealth = Mathf.Max(1f, maxHealth);
        baseColors = new Color[hitRenderers != null ? hitRenderers.Length : 0];

        for (int i = 0; i < baseColors.Length; i++)
        {
            if (hitRenderers[i] != null)
                baseColors[i] = hitRenderers[i].color;
        }

        ApplyIntactState();
    }

    private void OnEnable()
    {
        if (!broken && Active.Add(this))
            MarkerStateChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (Active.Remove(this))
            MarkerStateChanged?.Invoke();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (broken || other == null)
            return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead || player.CurrentHealth <= 0f)
            return;

        Break();
    }

    public bool TakeDamage(float damage, Vector2 hitPoint)
    {
        if (broken || damage <= 0f)
            return false;

        currentHealth -= damage;

        if (currentHealth <= 0f)
            Break();
        else
            PlayHitReaction();

        return true;
    }

    private void Break()
    {
        if (broken)
            return;

        broken = true;

        if (Active.Remove(this))
            MarkerStateChanged?.Invoke();

        if (hitRoutine != null)
            StopCoroutine(hitRoutine);

        RestoreHitColors();

        if (intactVisual != null)
            intactVisual.SetActive(false);
        if (brokenVisual != null)
            brokenVisual.SetActive(true);
        if (intactCollider != null)
            intactCollider.enabled = false;

        if (breakFx != null)
        {
            breakFx.SetActive(true);
            ParticleSystem[] particles =
                breakFx.GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);
        }

        Broken?.Invoke(this);
        DropLoot();
    }

    public void CollectTacticalMapMarkers(
        List<TacticalMapMarkerDescriptor> markers)
    {
        if (markers == null || broken || !isActiveAndEnabled ||
            !gameObject.activeInHierarchy)
        {
            return;
        }

        markers.Add(new TacticalMapMarkerDescriptor(
            TacticalMapMarkerKind.Breakable,
            transform.position
        ));
    }

    private void ApplyIntactState()
    {
        broken = false;

        if (intactVisual != null)
            intactVisual.SetActive(true);
        if (brokenVisual != null)
            brokenVisual.SetActive(false);
        if (breakFx != null)
            breakFx.SetActive(false);
        if (intactCollider != null)
            intactCollider.enabled = true;
    }

    private void PlayHitReaction()
    {
        if (hitFlashDuration <= 0f || baseColors.Length == 0)
            return;

        if (hitRoutine != null)
            StopCoroutine(hitRoutine);

        hitRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        for (int i = 0; i < hitRenderers.Length; i++)
        {
            if (hitRenderers[i] != null)
                hitRenderers[i].color = hitFlashColor;
        }

        yield return new WaitForSeconds(hitFlashDuration);
        RestoreHitColors();
        hitRoutine = null;
    }

    private void RestoreHitColors()
    {
        for (int i = 0; i < baseColors.Length; i++)
        {
            if (hitRenderers[i] != null)
                hitRenderers[i].color = baseColors[i];
        }
    }

    private void DropLoot()
    {
        if (lootProfile != null)
        {
            DropProfileLoot();
            return;
        }

        DropGoldLoot(
            nothingWeight,
            smallGoldWeight,
            largeGoldWeight,
            smallGoldCount,
            largeGoldCount,
            goldValue
        );
    }

    private void DropProfileLoot()
    {
        if (lootProfile.RewardKind ==
            WorldBreakableRewardKind.WorldEventUpgradeChoices)
        {
            IssueWorldEventReward();
            return;
        }

        DropGoldLoot(
            lootProfile.NothingWeight,
            lootProfile.SmallGoldWeight,
            lootProfile.LargeGoldWeight,
            lootProfile.SmallGoldCount,
            lootProfile.LargeGoldCount,
            lootProfile.GoldValue
        );
    }

    private void IssueWorldEventReward()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;
        if (upgradeManager == null)
        {
            Debug.LogError(
                "[WorldBreakable] Guaranteed event reward could not be " +
                "issued because UpgradeManager is unavailable.",
                this
            );
            return;
        }

        int choiceCount = lootProfile.EventChoiceCount;
        if (eventRewardNumericOnly)
        {
            upgradeManager.ShowNumericChestRewardChoices(choiceCount, null);
        }
        else
        {
            upgradeManager.ShowChestRewardChoices(
                choiceCount,
                eventRewardImproved,
                null
            );
        }

        RewardIssued?.Invoke(this);
    }

    private void DropGoldLoot(
        float configuredNothingWeight,
        float configuredSmallWeight,
        float configuredLargeWeight,
        int configuredSmallCount,
        int configuredLargeCount,
        int configuredGoldValue)
    {
        if (goldPrefab == null)
            return;

        float totalWeight = configuredNothingWeight +
            configuredSmallWeight + configuredLargeWeight;
        if (totalWeight <= 0f)
            return;

        float roll = UnityEngine.Random.value * totalWeight;
        if (roll < configuredNothingWeight)
            return;

        int count = roll < configuredNothingWeight + configuredSmallWeight
            ? configuredSmallCount
            : configuredLargeCount;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        Transform player = playerObject != null ? playerObject.transform : null;

        for (int i = 0; i < count; i++)
        {
            GoldenCoinPickup coin = Instantiate(
                goldPrefab,
                transform.position,
                Quaternion.identity
            );
            coin.Initialize(
                player,
                configuredGoldValue,
                lootLifetime,
                pickupRadius,
                attractionSpeed,
                scatterSpeed,
                fadeDuration
            );
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        smallGoldCount = Mathf.Max(1, smallGoldCount);
        largeGoldCount = Mathf.Max(1, largeGoldCount);
        goldValue = Mathf.Max(1, goldValue);
    }
#endif
}
