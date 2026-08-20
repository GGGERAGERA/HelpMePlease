#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Subject42SingletonLifecycleTests
{
    [TearDown]
    public void TearDown()
    {
        ResetInstance<WorldRuleController>();
        ResetInstance<LevelAnomalyController>();
    }

    [Test]
    public void WorldRuleController_ReRegistersAfterDisableEnable()
    {
        VerifyDisableEnableCycle<WorldRuleController>(
            () => WorldRuleController.Instance);
    }

    [Test]
    public void LevelAnomalyController_ReRegistersAfterDisableEnable()
    {
        VerifyDisableEnableCycle<LevelAnomalyController>(
            () => LevelAnomalyController.Instance);
    }

    private static void VerifyDisableEnableCycle<T>(Func<T> getInstance)
        where T : MonoBehaviour
    {
        ResetInstance<T>();
        GameObject owner = new($"{typeof(T).Name} Lifecycle Test");

        try
        {
            T component = owner.AddComponent<T>();
            InvokeLifecycle(component, "Awake");
            InvokeLifecycle(component, "OnEnable");
            Assert.That(getInstance(), Is.SameAs(component));

            InvokeLifecycle(component, "OnDisable");
            Assert.That(getInstance(), Is.Null);

            InvokeLifecycle(component, "OnEnable");
            Assert.That(getInstance(), Is.SameAs(component));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void InvokeLifecycle(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing lifecycle {methodName}.");
        method.Invoke(target, null);
    }

    private static void ResetInstance<T>() where T : MonoBehaviour
    {
        PropertyInfo property = typeof(T).GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        property?.SetValue(null, null);
    }
}
#endif
