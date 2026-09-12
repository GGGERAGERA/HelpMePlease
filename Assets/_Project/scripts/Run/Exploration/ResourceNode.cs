using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.EventSystems;

// MVP placeholder: proximity + left click, no pickup/inventory lifecycle.
public sealed class ResourceNode : MonoBehaviour
{
    private const float Reach = 2f;
    private Transform player;
    private PlayerHealth playerHealth;
    private OrbitalStationRuntime station;
    private CircleCollider2D hitArea;
    private Material material;
    private bool collected;
    private bool hovered;
    private int gold;
    private Transform diamond;
    private ExplorationSectorConfig feedback;
    private bool wasInReach;
    private float pulseAge = 0.3f;
    private static readonly Color GoldColor = new(1f, 0.8f, 0.2f);

    public void Initialize(Transform owner)
    {
        player = owner;
        playerHealth = owner.GetComponent<PlayerHealth>();
        station = FindFirstObjectByType<OrbitalStationRuntime>();
        gold = Random.Range(5, 16);
        hitArea = gameObject.AddComponent<CircleCollider2D>();
        hitArea.isTrigger = true;
        hitArea.radius = 0.45f;
        material = AnomalyPowerVisuals.CreateMaterial("Resource Node Placeholder");
        var line = AnomalyPowerVisuals.CreateLine(transform, "Diamond",
            new Color(1f, 0.8f, 0.2f), 0.12f, 5, material);
        line.useWorldSpace = false;
        line.SetPositions(new[] { Vector3.up * .4f, Vector3.right * .4f,
            Vector3.down * .4f, Vector3.left * .4f, Vector3.up * .4f });
        diamond = line.transform;
        feedback = Resources.Load<ExplorationSectorConfig>("ProductionRun/ExplorationSectorConfig");
    }

    private bool CanCollect => !collected && isActiveAndEnabled && player != null &&
        CurrencyManager.Instance != null && Time.timeScale > 0f &&
        playerHealth != null && !playerHealth.IsDead &&
        !SceneTransitionOverlay.IsTransitioning && !OrbitalDevelopmentInput.IsGameplayInputBlocked &&
        (UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle) &&
        (station == null || (station.InputOwner != null &&
            station.InputOwner.CanUseDebugPlacement && !station.IsDebugPlacementActive)) &&
        Vector2.Distance(player.position, transform.position) <= Reach;

    private void LateUpdate()
    {
        var camera = Camera.main;
        bool over = camera != null && CanCollect &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) &&
            hitArea.OverlapPoint(camera.ScreenToWorldPoint(Input.mousePosition));
        bool inReach = CanCollect;
        if ((inReach && !wasInReach) || (over && !hovered)) pulseAge = 0f;
        pulseAge = Mathf.Min(0.3f, pulseAge + Time.deltaTime);
        diamond.localScale = Vector3.one *
            (1f + 0.2f * Mathf.Sin(pulseAge / 0.3f * Mathf.PI));
        wasInReach = inReach;
        if (over)
            station?.Interaction?.ShowHint("RESOURCE NODE", "ЛКМ · собрать золото");
        else if (hovered)
            station?.Interaction?.ClearHint();
        hovered = over;
        if (over && Input.GetMouseButtonDown(0))
            TryCollectAt(camera.ScreenToWorldPoint(Input.mousePosition));
    }

    // Shared pointer hit-test used by the runtime click and the short Play Mode smoke.
    public bool TryCollectAt(Vector2 pointerWorld)
    {
        if (!CanCollect || !hitArea.OverlapPoint(pointerWorld)) return false;
        collected = true; // Lock before callbacks; Destroy is deferred until end of frame.
        hitArea.enabled = false;
        int before = CurrencyManager.Instance.TotalGold;
        CurrencyManager.Instance.AddGold(gold);
        PlayCollectionFeedback(CurrencyManager.Instance.TotalGold - before);
        AudioService.Instance?.PlayAt(AudioCueId.UIConfirm, transform.position);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private void PlayCollectionFeedback(int amount)
    {
        if (feedback == null) return;
        if (feedback.ResourceNodePickupEffect != null)
            ExperienceManager.Instance?.PlayPickupEffect(
                feedback.ResourceNodePickupEffect, transform.position, GoldColor);
        if (feedback.ResourceNodePopupPrefab == null) return;
        // Same popup lifecycle as golden-enemy rewards; survives the collected node.
        var popup = Instantiate(feedback.ResourceNodePopupPrefab,
            transform.position + Vector3.up * 0.65f, Quaternion.identity);
        var text = popup.GetComponent<TMPro.TextMeshPro>();
        if (text != null)
        {
            text.text = $"+{amount}";
            text.color = GoldColor;
        }
    }

    private void OnDisable()
    {
        if (hovered && station != null) station.Interaction?.ClearHint();
        hovered = false;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
