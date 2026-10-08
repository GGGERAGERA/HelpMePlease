using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class TutorialOverlayPresenter : MonoBehaviour
{
    [SerializeField] private TutorialOverlay view;
    [SerializeField] private TextMeshProUGUI caption;
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private Camera worldCamera;
    private TutorialController tutorial;
    private Vector2Int captionSize;
    private readonly List<Rect> holes = new();
    private readonly Vector3[] corners = new Vector3[4];
    private Vector2 arrowTip, arrowDirection;
    private float scale;
    private TutorialStep shownStep = TutorialStep.Completed;
    public Canvas Canvas => overlayCanvas;
    public void SetVisible(bool visible) => overlayCanvas.enabled = visible;
    public void Bind(TutorialController controller) { tutorial = controller; shownStep = TutorialStep.Completed; SetVisible(controller != null); if (controller == null) view.SetState(false, holes, Vector2.zero, Vector2.zero, 1f, .62f, 45f); }
    private void OnEnable() { if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged += OnLanguage; }
    private void OnDisable() { if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= OnLanguage; }
    private void OnLanguage(GameLanguage language) => shownStep = TutorialStep.Completed;
    private static readonly string[] Descriptions = {
        "WASD — двигаться. Оружие атакует автоматически.", "Не стой на месте. Держи дистанцию.",
        "Подбирай осколки опыта, чтобы получать усиления.", "Выбери одну награду для этого забега.",
        "Поставь оружие на свободную точку.", "Исследуй карту, усиливайся и выполняй события.",
        "", "Сектор завершён. Иди к выходу." };

    private void LateUpdate()
    {
        if (tutorial == null) return;
        bool visible = tutorial.Step != TutorialStep.Completed && !SceneTransitionOverlay.IsTransitioning &&
            !(tutorial.Step >= TutorialStep.SectorGoal && UpgradeManager.Instance != null && !UpgradeManager.Instance.IsRewardQueueIdle);
        caption.enabled = visible;
        holes.Clear();
        arrowDirection = Vector2.zero;
        if (!visible) { ApplyView(visible); return; }
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        if (captionSize != screenSize)
        {
            captionSize = screenSize;
            scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var label = caption.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0f);
            label.pivot = new Vector2(0.5f, 0f);
            label.anchoredPosition = new Vector2(0f, 26f * scale);
            label.sizeDelta = new Vector2(Screen.width - 40f * scale, 82f * scale);
            caption.fontSizeMin = 14f * scale;
            caption.fontSizeMax = 22f * scale;
        }
        if (shownStep != tutorial.Step)
        {
            shownStep = tutorial.Step;
            caption.text = shownStep == TutorialStep.FirstEvent ? LocalizationService.EnsureExists().Get("tutorial.relay") : Descriptions[(int)shownStep];
        }

        if (tutorial.Step == TutorialStep.FirstReward && tutorial.RewardPanel != null)
        {
            foreach (var card in tutorial.RewardPanel.GetComponentsInChildren<UpgradeCardView>())
            {
                var rect = (RectTransform)card.transform;
                rect.GetWorldCorners(corners);
                var canvas = rect.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                AddHole(Rect.MinMaxRect(min.x - 6f * scale, min.y - 6f * scale, max.x + 6f * scale, max.y + 6f * scale));
            }
        }
        else if (tutorial.Step == TutorialStep.OrbitalPlacement && tutorial.Station != null && tutorial.PlacementKind.HasValue)
        {
            foreach (var ring in tutorial.Station.Rings)
                foreach (var mount in ring.Mounts)
                    if (tutorial.Station.State.CanInstallModule(tutorial.PlacementKind.Value, ring.RingId, mount.MountIndex, out _))
                        WorldHole(mount.Transform.position, 24f * scale);
        }
        else if (tutorial.FocusTarget != null)
        {
            float radius = tutorial.Step == TutorialStep.Movement ? 58f * scale : 30f * scale;
            if (tutorial.Step == TutorialStep.FirstEvent && tutorial.TargetEvent != null && worldCamera != null)
                radius = Mathf.Abs(worldCamera.WorldToScreenPoint(tutorial.FocusTarget.position + Vector3.right * .8f).x -
                    worldCamera.WorldToScreenPoint(tutorial.FocusTarget.position).x) + 10f * scale;
            WorldHole(tutorial.FocusTarget.position, radius);
        }
        if (holes.Count > 0 && (tutorial.Step == TutorialStep.FirstReward || tutorial.Step == TutorialStep.OrbitalPlacement))
        {
            arrowTip = new Vector2(holes[0].center.x, holes[0].yMax + 8f * scale);
            arrowDirection = Vector2.down;
        }
        ApplyView(visible);
    }
    private void WorldHole(Vector3 position, float radius)
    {
        if (worldCamera == null) return;
        Vector2 screen = worldCamera.WorldToScreenPoint(position);
        Vector2 clamped = new(Mathf.Clamp(screen.x, 45f * scale, Screen.width - 45f * scale),
            Mathf.Clamp(screen.y, 145f * scale, Screen.height - 65f * scale));
        bool offscreen = (screen - clamped).sqrMagnitude > 1f;
        // Offscreen targets get a directional arrow, never a fake highlighted object.
        if (!offscreen) AddHole(new Rect(screen - Vector2.one * radius, Vector2.one * radius * 2f));
        if (holes.Count <= 1)
        {
            arrowTip = offscreen ? clamped : screen + Vector2.up * (radius + 8f * scale);
            arrowDirection = offscreen ? (screen - clamped).normalized : Vector2.down;
        }
    }
    private void AddHole(Rect rect)
    {
        rect = Rect.MinMaxRect(Mathf.Clamp(rect.xMin, 0, Screen.width), Mathf.Clamp(rect.yMin, 0, Screen.height), Mathf.Clamp(rect.xMax, 0, Screen.width), Mathf.Clamp(rect.yMax, 0, Screen.height));
        if (rect.width > 0 && rect.height > 0) holes.Add(rect);
    }
    private void ApplyView(bool visible) => view.SetState(visible, holes, arrowTip, arrowDirection, scale,
        tutorial.Step == TutorialStep.SectorGoal ? .18f : .62f, (tutorial.Step == TutorialStep.Exit ? 75f : 45f) * scale);
}
