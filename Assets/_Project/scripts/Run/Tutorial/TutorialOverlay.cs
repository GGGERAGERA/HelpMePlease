using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A single transparent-to-input canvas. The dim mesh leaves real world/UI targets uncovered.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialOverlay : MaskableGraphic
{
    private TutorialController tutorial;
    private TextMeshProUGUI caption;
    private readonly List<Rect> holes = new();
    private readonly List<float> xs = new(), ys = new();
    private readonly Vector3[] corners = new Vector3[4];
    private readonly Vector3[] quad = new Vector3[4];
    private Vector2 arrowTip, arrowDirection;
    private float scale = 1f;
    private bool visible;
    private TutorialStep shownStep = TutorialStep.Completed;
    private static readonly Color Highlight = new(0.4f, 1f, 0.86f, 1f);
    private static readonly string[] Titles = { "ДВИЖЕНИЕ", "ВРАГИ", "ОПЫТ", "УСИЛЕНИЕ", "ОРБИТА", "СЕКТОР", "СОБЫТИЕ", "ВЫХОД" };
    private static readonly string[] Descriptions = {
        "WASD — двигаться. Оружие атакует автоматически.", "Не стой на месте. Держи дистанцию.",
        "Подбирай осколки опыта, чтобы получать усиления.", "Выбери одну награду для этого забега.",
        "Поставь оружие на свободную точку.", "Исследуй карту, усиливайся и выполняй события.",
        "Оставайся в зоне до заполнения шкалы.", "Сектор завершён. Иди к выходу." };

    public static TutorialOverlay Create(TutorialController controller)
    {
        var root = new GameObject("Tutorial Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var surface = new GameObject("Focus", typeof(RectTransform));
        surface.transform.SetParent(root.transform, false);
        var bounds = (RectTransform)surface.transform;
        bounds.anchorMin = Vector2.zero; bounds.anchorMax = Vector2.one;
        bounds.offsetMin = bounds.offsetMax = Vector2.zero;
        var result = surface.AddComponent<TutorialOverlay>();
        result.tutorial = controller;
        result.raycastTarget = false;
        var textObject = new GameObject("Instruction", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(surface.transform, false);
        result.caption = textObject.GetComponent<TextMeshProUGUI>();
        result.caption.font = TMP_Settings.defaultFontAsset;
        result.caption.alignment = TextAlignmentOptions.Center;
        result.caption.color = Color.white;
        result.caption.raycastTarget = false;
        result.caption.textWrappingMode = TextWrappingModes.NoWrap;
        result.caption.enableAutoSizing = true;
        return result;
    }

    private void LateUpdate()
    {
        if (tutorial == null) return;
        visible = tutorial.Step != TutorialStep.Completed && !SceneTransitionOverlay.IsTransitioning &&
            !(tutorial.Step >= TutorialStep.SectorGoal && UpgradeManager.Instance != null && !UpgradeManager.Instance.IsRewardQueueIdle);
        caption.enabled = visible;
        holes.Clear();
        arrowDirection = Vector2.zero;
        if (!visible) { SetVerticesDirty(); return; }
        scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        var label = caption.rectTransform;
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0f);
        label.pivot = new Vector2(0.5f, 0f);
        label.anchoredPosition = new Vector2(0f, 26f * scale);
        label.sizeDelta = new Vector2(Screen.width - 40f * scale, 82f * scale);
        caption.fontSizeMin = 14f * scale;
        caption.fontSizeMax = 22f * scale;
        if (shownStep != tutorial.Step)
        {
            shownStep = tutorial.Step;
            caption.text = $"<b><color=#66FFDB>{Titles[(int)shownStep]}</color></b>\n{Descriptions[(int)shownStep]}";
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
            if (tutorial.Step == TutorialStep.FirstEvent && tutorial.TargetEvent != null && Camera.main != null)
                radius = Mathf.Abs(Camera.main.WorldToScreenPoint(tutorial.FocusTarget.position + Vector3.right * tutorial.TargetEvent.CaptureRadius).x -
                    Camera.main.WorldToScreenPoint(tutorial.FocusTarget.position).x) + 10f * scale;
            WorldHole(tutorial.FocusTarget.position, radius);
        }
        if (holes.Count > 0 && (tutorial.Step == TutorialStep.FirstReward || tutorial.Step == TutorialStep.OrbitalPlacement))
        {
            arrowTip = new Vector2(holes[0].center.x, holes[0].yMax + 8f * scale);
            arrowDirection = Vector2.down;
        }
        SetVerticesDirty();
    }

    private void WorldHole(Vector3 position, float radius)
    {
        if (Camera.main == null) return;
        Vector2 screen = Camera.main.WorldToScreenPoint(position);
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
        rect = Rect.MinMaxRect(Mathf.Clamp(rect.xMin, 0, Screen.width), Mathf.Clamp(rect.yMin, 0, Screen.height),
            Mathf.Clamp(rect.xMax, 0, Screen.width), Mathf.Clamp(rect.yMax, 0, Screen.height));
        if (rect.width > 0 && rect.height > 0) holes.Add(rect);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!visible || tutorial == null) return;
        xs.Clear(); ys.Clear();
        xs.Add(0); xs.Add(Screen.width); ys.Add(0); ys.Add(Screen.height);
        foreach (var hole in holes) { xs.Add(hole.xMin); xs.Add(hole.xMax); ys.Add(hole.yMin); ys.Add(hole.yMax); }
        xs.Sort(); ys.Sort();
        var dim = new Color(0.015f, 0.025f, 0.035f, tutorial.Step == TutorialStep.SectorGoal ? 0.18f : 0.62f);
        for (int x = 1; x < xs.Count; x++)
            for (int y = 1; y < ys.Count; y++)
            {
                var rect = Rect.MinMaxRect(xs[x - 1], ys[y - 1], xs[x], ys[y]);
                if (rect.width <= 0 || rect.height <= 0) continue;
                bool cutout = false;
                foreach (var hole in holes) if (hole.Contains(rect.center)) { cutout = true; break; }
                if (!cutout) Rectangle(mesh, rect, dim);
            }
        float border = 3f * scale;
        foreach (var hole in holes)
        {
            Rectangle(mesh, new Rect(hole.xMin, hole.yMin, hole.width, border), Highlight);
            Rectangle(mesh, new Rect(hole.xMin, hole.yMax - border, hole.width, border), Highlight);
            Rectangle(mesh, new Rect(hole.xMin, hole.yMin, border, hole.height), Highlight);
            Rectangle(mesh, new Rect(hole.xMax - border, hole.yMin, border, hole.height), Highlight);
        }
        Rectangle(mesh, new Rect(16f * scale, 18f * scale, Screen.width - 32f * scale, 98f * scale), new Color(0.02f, 0.04f, 0.05f, 0.94f));
        if (arrowDirection.sqrMagnitude > 0f)
        {
            float length = (tutorial.Step == TutorialStep.Exit ? 75f : 45f) * scale;
            Vector2 back = arrowTip - arrowDirection * length;
            Vector2 perpendicular = new(-arrowDirection.y, arrowDirection.x);
            Line(mesh, back, arrowTip, 5f * scale);
            Line(mesh, arrowTip - arrowDirection * 18f * scale + perpendicular * 13f * scale, arrowTip, 5f * scale);
            Line(mesh, arrowTip - arrowDirection * 18f * scale - perpendicular * 13f * scale, arrowTip, 5f * scale);
        }
    }

    private void Rectangle(VertexHelper mesh, Rect rect, Color tint)
    {
        quad[0] = new Vector2(rect.xMin, rect.yMin); quad[1] = new Vector2(rect.xMin, rect.yMax);
        quad[2] = new Vector2(rect.xMax, rect.yMax); quad[3] = new Vector2(rect.xMax, rect.yMin);
        AddQuad(mesh, tint);
    }
    private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
        quad[0] = from - side; quad[1] = from + side; quad[2] = to + side; quad[3] = to - side;
        AddQuad(mesh, Highlight);
    }
    private void AddQuad(VertexHelper mesh, Color tint)
    {
        int start = mesh.currentVertCount;
        Vector2 offset = rectTransform.rect.min;
        for (int i = 0; i < 4; i++) mesh.AddVert((Vector2)quad[i] + offset, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
