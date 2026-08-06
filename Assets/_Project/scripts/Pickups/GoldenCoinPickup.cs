using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class GoldenCoinPickup : MonoBehaviour
{
    private enum DestructionReason
    {
        None,
        Collected,
        Expired,
        RuleClear
    }

    private const float AttractionDelay = 0.3f;
    private const float CollectDistance = 0.24f;
    private const float ScatterDamping = 7f;
    private const float RotationSpeed = 120f;

    private static readonly HashSet<GoldenCoinPickup> Active = new();
    private static readonly List<GoldenCoinPickup> ClearBuffer = new();

    private Transform player;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private Light2D[] lights;
    private float[] baseLightIntensities;
    private Vector2 scatterVelocity;
    private float elapsed;
    private float lifetimeRemaining;
    private float pickupRadius;
    private float attractSpeed;
    private float fadeDuration;
    private int value;
    private bool initialized;
    private bool collected;
    private DestructionReason destructionReason;

    public static int ActiveCount => Active.Count;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;

        lights = GetComponentsInChildren<Light2D>(true);
        baseLightIntensities = new float[lights.Length];

        for (int i = 0; i < lights.Length; i++)
            baseLightIntensities[i] = lights[i].intensity;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[GoldenCoin] Awake: object='{name}', " +
            $"renderers={renderers.Length}, lights={lights.Length}, " +
            $"active={gameObject.activeInHierarchy}.",
            this
        );
#endif
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    public void Initialize(
        Transform playerTransform,
        int coinValue,
        float lifetime,
        float fallbackPickupRadius,
        float attractionSpeed,
        float scatterSpeed,
        float expiryFadeDuration)
    {
        player = playerTransform;

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        value = Mathf.Max(1, coinValue);
        lifetimeRemaining = Mathf.Max(0.1f, lifetime);
        attractSpeed = Mathf.Max(0.1f, attractionSpeed);
        fadeDuration = Mathf.Clamp(
            expiryFadeDuration,
            0.1f,
            lifetimeRemaining
        );
        pickupRadius = Mathf.Max(0.1f, fallbackPickupRadius);

        if (player != null)
        {
            PlayerPickupRadius radius =
                player.GetComponent<PlayerPickupRadius>();

            if (radius != null)
                pickupRadius = radius.CurrentRadius;
        }

        Vector2 scatterDirection = Random.insideUnitCircle;

        if (scatterDirection.sqrMagnitude < 0.001f)
            scatterDirection = Vector2.right;

        scatterVelocity = scatterDirection.normalized *
            Mathf.Max(0f, scatterSpeed) * Random.Range(0.55f, 1f);
        elapsed = 0f;
        collected = false;
        destructionReason = DestructionReason.None;
        initialized = true;
        RestoreVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[GoldenCoin] Initialize: object='{name}', position={transform.position}, " +
            $"player='{(player != null ? player.name : "null")}', value={value}, " +
            $"lifetime={lifetimeRemaining:F2}, pickupRadius={pickupRadius:F2}.",
            this
        );
#endif
    }

    private void Update()
    {
        if (!initialized || collected)
            return;

        float deltaTime = Time.deltaTime;
        elapsed += deltaTime;
        lifetimeRemaining -= deltaTime;

        if (scatterVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += (Vector3)(scatterVelocity * deltaTime);
            scatterVelocity = Vector2.MoveTowards(
                scatterVelocity,
                Vector2.zero,
                ScatterDamping * deltaTime
            );
        }

        transform.Rotate(0f, 0f, RotationSpeed * deltaTime);

        if (player != null)
        {
            Vector2 offset = player.position - transform.position;
            float sqrDistance = offset.sqrMagnitude;

            if (elapsed >= AttractionDelay &&
                sqrDistance <= pickupRadius * pickupRadius)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    player.position,
                    attractSpeed * deltaTime
                );
                offset = player.position - transform.position;
                sqrDistance = offset.sqrMagnitude;
            }

            if (sqrDistance <= CollectDistance * CollectDistance)
            {
                Collect();
                return;
            }
        }

        UpdateExpiryVisual();

        if (lifetimeRemaining <= 0f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[GoldenCoin] Expired: object='{name}'.", this);
#endif
            DestroyForReason(DestructionReason.Expired);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            Collect();
    }

    private void Collect()
    {
        if (collected || CurrencyManager.Instance == null)
            return;

        collected = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[GoldenCoin] Collected: object='{name}', value={value}.",
            this
        );
#endif
        CurrencyManager.Instance.AddGold(value);
        DestroyForReason(DestructionReason.Collected);
    }

    private void UpdateExpiryVisual()
    {
        float fade = lifetimeRemaining < fadeDuration
            ? Mathf.Clamp01(lifetimeRemaining / fadeDuration)
            : 1f;
        float pulse = lifetimeRemaining < fadeDuration
            ? Mathf.Lerp(0.35f, 1f, Mathf.PingPong(elapsed * 7f, 1f))
            : 1f;
        float alpha = fade * pulse;

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = baseColors[i];
            color.a *= alpha;
            renderers[i].color = color;
        }

        for (int i = 0; i < lights.Length; i++)
            lights[i].intensity = baseLightIntensities[i] * alpha;
    }

    private void RestoreVisuals()
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = baseColors[i];

        for (int i = 0; i < lights.Length; i++)
            lights[i].intensity = baseLightIntensities[i];
    }

    public static void ClearAll()
    {
        ClearBuffer.Clear();

        foreach (GoldenCoinPickup coin in Active)
        {
            if (coin != null)
                ClearBuffer.Add(coin);
        }

        for (int i = 0; i < ClearBuffer.Count; i++)
            ClearBuffer[i].DestroyForReason(DestructionReason.RuleClear);

        ClearBuffer.Clear();
    }

    private void OnDisable()
    {
        Active.Remove(this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (initialized && !collected)
        {
            string reason = destructionReason != DestructionReason.None
                ? destructionReason.ToString()
                : "ExternalDestroyOrSceneUnload";
            Debug.Log(
                $"[GoldenCoin] Destroyed before pickup: object='{name}', " +
                $"reason={reason}.",
                this
            );
        }
#endif
    }

    private void DestroyForReason(DestructionReason reason)
    {
        if (destructionReason != DestructionReason.None)
            return;

        destructionReason = reason;
        Destroy(gameObject);
    }
}
