#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed partial class Subject42AuthoredUiTests
{

    [Category("Extended")]
    [Test]
    public void RunResultHostHasNoLegacyResultHierarchy()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/UI/RunResultPanel.prefab");
        Assert.That(prefab.transform.childCount, Is.Zero,
            "The authored death window is attached by MVP; old victory/stats/buttons must not coexist.");
        var view = new SerializedObject(prefab.GetComponent<RunResultView>());
        Assert.That(view.FindProperty("death").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<DeathResultPresentation>()));
        Assert.That(view.FindProperty("titleText"), Is.Null);
        var death = new SerializedObject(prefab.GetComponent<DeathResultPresentation>());
        Assert.That(death.FindProperty("legacyObjects"), Is.Null);
    }

    [Category("Extended")]
    [Test]
    public void BunkerSummaryDoesNotOwnRunTeardown()
    {
        string source = File.ReadAllText("Assets/_Project/scripts/Bunker/UI/BunkerRunSummaryPresenter.cs");
        Assert.That(source, Does.Not.Contain("ClearFinishedRunCompatibilityState"));
    }

    [Category("Extended")]
    [Test]
    public void ProductionScenesHaveRequiredUiReferencesAndOneEventSystem()
    {
        var report = Subject42ProjectValidator.ValidateAuthoredUi();
        Assert.That(report.ErrorCount, Is.Zero, report.FormatErrors());
    }

    [Category("Extended")]
    [TestCase("TacticalMapShell", 43)]
    [TestCase("WorldLootReelView", 13)]
    [TestCase("DeathResultWindow", 57)]
    public void ShellsAreAuthoredWithoutDynamicItems(string name, int transforms)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
        foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.font, Is.Not.Null, text.name);
            Assert.That(text.fontSharedMaterial, Is.Not.Null, text.name);
        }
        foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            Assert.That(component, Is.Not.Null, "Missing prefab script");
    }

    [Category("Extended")]
    [TestCase(typeof(TacticalMapHUD), "Authored shell or scene")]
    [TestCase(typeof(WorldLootRewardReel), "Authored shell references")]
    [TestCase(typeof(DeathResultPresentation), "Authored result references")]
    [TestCase(typeof(BunkerContext), "Authored progression/loadout")]
    public void MissingMandatoryReferencesDisableWithoutConstructingUi(Type type, string error)
    {
        var root = new GameObject("Missing UI fixture");
        root.SetActive(false);
        try
        {
            var component = (MonoBehaviour)root.AddComponent(type);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(error)));
            Call(component, "Awake");
            Assert.That(component.enabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Category("Extended")]
    [Test]
    public void MissingGameplayHostReferencesDisableWithoutAddingSystems()
    {
        var root = new GameObject("Missing host fixture");
        root.SetActive(false);
        try
        {
            var host = root.AddComponent<LevelModifiersApplier>();
            var start = (IEnumerator)typeof(LevelModifiersApplier).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
            Assert.That(start.MoveNext(), Is.True);
            LogAssert.Expect(LogType.Error, new Regex("Authored scene-host references are missing"));
            Assert.That(start.MoveNext(), Is.False);
            Assert.That(host.enabled, Is.False);
            Assert.That(root.GetComponent<RunThreatController>(), Is.Null);
            Assert.That(root.GetComponent<ProductionExplorationSectorController>(), Is.Null);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
