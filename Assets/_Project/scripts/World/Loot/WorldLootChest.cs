using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class WorldLootChest : Interactable
{
    public enum ChestState
    {
        Closed,
        Opening,
        RewardReel,
        Claimed
    }

    [Header("Visuals")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openedVisual;
    [SerializeField] private Animator animator;
    [SerializeField] private string closedStateName = "animCaseIdle1";
    [SerializeField] private string openingStateName = "animCaseOpen1";

    [Header("Opening")]
    [SerializeField, Min(0.1f)] private float interactionRadius = 1.15f;
    [SerializeField, Min(0f)] private float openingDelay = 3.35f;
    [SerializeField, Min(0f)] private float claimedDestroyDelay = 0.15f;

    [Header("Reward Pool")]
    [SerializeField] private WorldLootRewardDefinition[] rewardPool;

    private Collider2D interactionCollider;
    private Coroutine openingRoutine;
    private ChestState state;

    public override bool CanInteract => state == ChestState.Closed;
    public ChestState State => state;
    public IReadOnlyList<WorldLootRewardDefinition> RewardPool => rewardPool;

    private void Awake()
    {
        interactionCollider = GetComponent<Collider2D>();
        interactionCollider.isTrigger = true;

        if (interactionCollider is CircleCollider2D circle)
            circle.radius = Mathf.Max(0.1f, interactionRadius);

        state = ChestState.Closed;
        ApplyClosedVisual();
    }

    public override void Interact()
    {
        if (!CanInteract)
            return;

        state = ChestState.Opening;
        interactionCollider.enabled = false;
        PlayOpeningVisual();
        openingRoutine = StartCoroutine(WaitForOpening());
    }

    public void NotifyOpeningAnimationComplete()
    {
        if (state != ChestState.Opening)
            return;

        if (openingRoutine != null)
        {
            StopCoroutine(openingRoutine);
            openingRoutine = null;
        }

        OpenRewardReel();
    }

    public void ConfigureRewardPool(
        WorldLootRewardDefinition[] definitions)
    {
        if (state != ChestState.Closed)
            return;

        rewardPool = definitions;
    }

    private IEnumerator WaitForOpening()
    {
        float elapsed = 0f;

        while (elapsed < openingDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        openingRoutine = null;
        OpenRewardReel();
    }

    private void OpenRewardReel()
    {
        if (state != ChestState.Opening)
            return;

        state = ChestState.RewardReel;
        ApplyOpenedVisual();

        if (WorldLootRewardReel.TryShow(rewardPool, HandleRewardClaimed))
            return;

        Debug.LogError(
            $"[WorldLootChest] Reward Reel could not open for '{name}'. " +
            "Check the configured reward pool.",
            this
        );
    }

    private void HandleRewardClaimed(WorldLootRewardDefinition reward)
    {
        if (this == null || !isActiveAndEnabled ||
            state != ChestState.RewardReel || reward == null)
            return;

        state = ChestState.Claimed;
        ApplyOpenedVisual();
        StartCoroutine(DestroyAfterClaim());
    }

    private IEnumerator DestroyAfterClaim()
    {
        float elapsed = 0f;

        while (elapsed < claimedDestroyDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private void PlayOpeningVisual()
    {
        if (animator != null && !string.IsNullOrWhiteSpace(openingStateName))
        {
            animator.speed = 1f;
            animator.Play(openingStateName, 0, 0f);
        }
    }

    private void ApplyClosedVisual()
    {
        if (closedVisual != null)
            closedVisual.SetActive(true);

        if (openedVisual != null && openedVisual != closedVisual)
            openedVisual.SetActive(false);

        if (animator != null && !string.IsNullOrWhiteSpace(closedStateName))
        {
            animator.speed = 1f;
            animator.Play(closedStateName, 0, 0f);
        }
    }

    private void ApplyOpenedVisual()
    {
        if (closedVisual != null && openedVisual != null &&
            closedVisual != openedVisual)
        {
            closedVisual.SetActive(false);
        }

        if (openedVisual != null)
            openedVisual.SetActive(true);

        if (animator != null)
            animator.speed = 0f;
    }

    private void OnDisable()
    {
        if (openingRoutine == null)
            return;

        StopCoroutine(openingRoutine);
        openingRoutine = null;
    }
}

public static class WorldLootChestSpawner
{
    private const string DefaultResourcePath =
        "WorldLoot/WorldLootChestV1";

    public static WorldLootChest SpawnChest(Vector2 position)
    {
        WorldLootChest prefab =
            Resources.Load<WorldLootChest>(DefaultResourcePath);
        return SpawnChest(prefab, position);
    }

    public static WorldLootChest SpawnChest(
        WorldLootChest prefab,
        Vector2 position)
    {
        if (prefab == null)
        {
            Debug.LogError(
                $"[WorldLootChestSpawner] Chest prefab is missing at " +
                $"Resources/{DefaultResourcePath}."
            );
            return null;
        }

        return Object.Instantiate(prefab, position, Quaternion.identity);
    }
}
