#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CameraFollowZoomTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject rig;
    private Camera camera;
    private CameraFollow follow;

    private void Set(string name, object value) => typeof(CameraFollow).GetField(name, Private).SetValue(follow, value);
    private object Call(string name, params object[] args) => typeof(CameraFollow).GetMethod(name, Private).Invoke(follow, args);
    private float Offset => (float)typeof(CameraFollow).GetField("userZoomOffset", Private).GetValue(follow);

    [SetUp]
    public void SetUp()
    {
        rig = new GameObject("Zoom test", typeof(Camera));
        rig.hideFlags = HideFlags.HideAndDontSave;
        camera = rig.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 7f;
        camera.aspect = 16f / 9f;
        follow = rig.AddComponent<CameraFollow>();
        Call("CaptureProductionOrthographicSize");
        follow.target = rig.transform;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(rig);

    private void Step(float autoSize, float wheel = 0f)
    {
        Set("pendingWheel", wheel);
        Call("ApplyUserZoom", autoSize, 1f / 60f);
    }

    private void Settle(float autoSize)
    {
        for (int i = 0; i < 180; i++) Step(autoSize);
    }

    private void Bunker()
    {
        Set("minZoom", 5f);
        Set("maxZoom", 7f);
        Set("wheelSensitivity", .5f);
        Set("scaleZoomLimitsWithAutoFraming", false);
        Set("maxViewWidth", 24.88889f);
    }

    [Test]
    public void Bunker_RepeatedWheelIsSmoothAndReversesAtBothLimits()
    {
        Bunker();
        Step(7f, 1f);
        Assert.That(camera.orthographicSize, Is.InRange(6.5f, 7f));
        Assert.That(camera.orthographicSize, Is.GreaterThan(6.5f));
        for (int i = 0; i < 30; i++) Step(7f, 1f);
        Settle(7f);
        Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(.001f));
        Step(7f, -1f);
        Assert.That(camera.orthographicSize, Is.GreaterThan(5f));
        for (int i = 0; i < 30; i++) Step(7f, -1f);
        Settle(7f);
        Assert.That(camera.orthographicSize, Is.EqualTo(7f).Within(.001f));
        Step(7f, 1f);
        Assert.That(camera.orthographicSize, Is.LessThan(7f));
        Assert.That(follow.ProductionOrthographicSize, Is.EqualTo(7f));
    }

    [TestCase(7f)]
    [TestCase(14f)]
    [TestCase(45f)]
    public void Orbital_ManualRangeTracksAutoFraming(float autoSize)
    {
        Step(autoSize, 100f);
        Settle(autoSize);
        Assert.That(camera.orthographicSize, Is.EqualTo(autoSize * .8f).Within(.001f));
        Step(autoSize, -100f);
        Settle(autoSize);
        Assert.That(camera.orthographicSize, Is.EqualTo(autoSize * 1.5f).Within(.001f));
        Call("ResetUserZoom");
        Settle(autoSize);
        Assert.That(camera.orthographicSize, Is.EqualTo(autoSize).Within(.001f));
    }

    [Test]
    public void Orbital_GrowthPreservesUserOffsetWithoutFeedingItBackIntoBase()
    {
        Step(7f, 1f);
        Settle(7f);
        float originalOffset = Offset;
        for (int i = 0; i < 180; i++) Step(Mathf.Lerp(7f, 35f, i / 179f));
        Assert.That(Offset, Is.EqualTo(originalOffset).Within(.001f));
        Assert.That(camera.orthographicSize, Is.EqualTo(31.5f).Within(.001f));
        Assert.That(follow.ProductionOrthographicSize, Is.EqualTo(7f));
        Call("OnDisable");
        Settle(7f);
        Assert.That(Offset, Is.Zero);
        Assert.That(camera.orthographicSize, Is.EqualTo(7f));
    }

    [TestCase(4f / 3f)]
    [TestCase(16f / 9f)]
    [TestCase(21f / 9f)]
    [TestCase(32f / 9f)]
    public void Bunker_AspectChangeNeverExceedsOriginalOverview(float aspect)
    {
        Bunker();
        camera.aspect = aspect;
        Step(7f, -100f);
        Settle(7f);
        Assert.That(camera.orthographicSize, Is.LessThanOrEqualTo(7f));
        Assert.That(camera.orthographicSize * 2f * aspect, Is.LessThanOrEqualTo(24.889f));
        Step(7f, 100f);
        Settle(7f);
        Assert.That(camera.orthographicSize, Is.GreaterThan(0f));
        Assert.That(camera.orthographicSize * 2f * aspect, Is.LessThanOrEqualTo(24.889f));
    }

    [Test]
    public void Football_WorldBoundsFocusKeepsAuthoredSizeAndRestoresUserView()
    {
        Bunker();
        Step(7f, 2f);
        Settle(7f);
        float userSize = camera.orthographicSize;
        Assert.That(follow.BeginWorldBoundsFocus(rig, new Vector2(10f, 20f), 15f), Is.True);
        for (int i = 0; i < 10; i++) Call("LateUpdate");
        Assert.That(camera.orthographicSize, Is.EqualTo(15f));
        Assert.That(camera.transform.position.x, Is.EqualTo(10f));
        follow.EndWorldBoundsFocus(rig);
        Assert.That(camera.orthographicSize, Is.EqualTo(userSize));
    }

    [Test]
    public void Ui_OnlyScrollReceivingHierarchyBlocksCamera()
    {
        EventSystem previous = EventSystem.current;
        var events = new GameObject("Zoom events", typeof(EventSystem));
        var canvasObject = new GameObject("Zoom UI", typeof(Canvas), typeof(GraphicRaycaster));
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        try
        {
            EventSystem.current = events.GetComponent<EventSystem>();
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            Assert.That(Call("IsPointerOverScrollHandler", point), Is.True);
            panel.GetComponent<ScrollRect>().enabled = false;
            Assert.That(Call("IsPointerOverScrollHandler", point), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(events);
            EventSystem.current = previous;
        }
    }
}
#endif
