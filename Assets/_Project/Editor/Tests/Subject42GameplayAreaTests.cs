#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Subject42GameplayAreaTests
{
    private GameObject areaObject;
    private GameplayAreaService service;
    private BoxCollider2D area;

    [SetUp]
    public void SetUp()
    {
        SetStaticProperty(typeof(GameplayAreaService), "Instance", null);
        areaObject = new GameObject("Gameplay Area Test");
        area = areaObject.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        service = areaObject.AddComponent<GameplayAreaService>();
        InvokeLifecycle(service, "Awake");
        service.ConfigureDebugAreas(area, area);
    }

    [TearDown]
    public void TearDown()
    {
        if (areaObject != null)
            UnityEngine.Object.DestroyImmediate(areaObject);

        SetStaticProperty(typeof(GameplayAreaService), "Instance", null);
    }

    [Test]
    public void TryGetSpawnPosition_RespectsExactDistance()
    {
        area.size = new Vector2(40f, 40f);

        bool found = service.TryGetSpawnPosition(
            Vector3.zero,
            8f,
            8f,
            24,
            0f,
            out Vector3 position);

        Assert.That(found, Is.True);
        Assert.That(
            Vector2.Distance(Vector2.zero, position),
            Is.EqualTo(8f).Within(0.001f));
        Assert.That(area.OverlapPoint(position), Is.True);
    }

    [Test]
    public void TryGetSpawnPosition_ReturnsFalseForImpossibleDistance()
    {
        area.size = new Vector2(2f, 2f);

        bool found = service.TryGetSpawnPosition(
            Vector3.zero,
            8f,
            8f,
            24,
            0f,
            out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void TryGetSpawnPosition_NormalizesReversedRange()
    {
        area.size = new Vector2(40f, 40f);

        bool found = service.TryGetSpawnPosition(
            Vector3.zero,
            9f,
            3f,
            8,
            0f,
            out Vector3 position);

        Assert.That(found, Is.True);
        Assert.That(
            Vector2.Distance(Vector2.zero, position),
            Is.EqualTo(9f).Within(0.001f));
    }

    private static void InvokeLifecycle(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }

    private static void SetStaticProperty(
        Type type,
        string propertyName,
        object value)
    {
        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Static | BindingFlags.Public);
        property?.SetValue(null, value);
    }
}
#endif
