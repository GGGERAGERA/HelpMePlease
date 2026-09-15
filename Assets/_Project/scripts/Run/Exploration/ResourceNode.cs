using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ResourceNode : MonoBehaviour
{
    private const float Reach = 2f;
    [SerializeField] private GameObject openedVisual;
    [SerializeField] private GameObject closedVisual;

    private Transform player;
    private PlayerHealth playerHealth;
    private OrbitalStationRuntime station;
    private Collider2D hitArea;
    private bool collected;
    private bool hovered;
    private int gold;
    private ExplorationSectorConfig feedback;
    private static readonly Color GoldColor = new(1f, 0.8f, 0.2f);

    public void Initialize(Transform owner)
    {
        player = owner;
        playerHealth = owner.GetComponent<PlayerHealth>();
        station = FindFirstObjectByType<OrbitalStationRuntime>();
        gold = Random.Range(5, 16);
        collected = false;
        openedVisual.SetActive(true);
        closedVisual.SetActive(false);
        hitArea = CreateHitArea();
        hitArea.isTrigger = true;
        feedback = Resources.Load<ExplorationSectorConfig>("ProductionRun/ExplorationSectorConfig");
    }

    private Collider2D CreateHitArea()
    {
        var area = gameObject.AddComponent<BoxCollider2D>();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0) return area;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        Vector3 localMin = transform.InverseTransformPoint(bounds.min);
        Vector3 localMax = transform.InverseTransformPoint(bounds.max);
        area.offset = transform.InverseTransformPoint(bounds.center);
        area.size = new Vector2(
            Mathf.Abs(localMax.x - localMin.x),
            Mathf.Abs(localMax.y - localMin.y));
        return area;
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
        if (over)
            station?.Interaction?.ShowHint("resource.title", "resource.collect");
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
        collected = true;
        hitArea.enabled = false;
        openedVisual.SetActive(false);
        closedVisual.SetActive(true);
        int before = CurrencyManager.Instance.TotalGold;
        CurrencyManager.Instance.AddGold(gold);
        PlayCollectionFeedback(CurrencyManager.Instance.TotalGold - before);
        AudioService.Instance?.PlayAt(AudioCueId.UIConfirm, transform.position);
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
}
