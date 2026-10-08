#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[Category("Authoring")]
public sealed class ProductionBoundaryAuthoringTests
{
    [Test]
    public void ProductionAssetsHaveNoDevDependenciesOrMissingReferences()
    {
        var paths = AssetDatabase.FindAssets("", new[] { "Assets/_Project/prefabs", "Assets/_Project/Data" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab") || path.EndsWith(".asset"))
            .Concat(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path))
            .Distinct().ToArray();
        var failures = new List<string>();
        var referenced = new HashSet<string>(AssetDatabase.GetDependencies(
            paths.Where(path => !path.EndsWith(".prefab")).ToArray(), true));
        foreach (string path in paths)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                if (dependency.StartsWith("Assets/_Project/Dev/"))
                    failures.Add(path + " -> " + dependency);
            if (!path.EndsWith(".prefab") || !referenced.Contains(path)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    failures.Add(path + ": missing script on " + transform.name);
                foreach (var component in transform.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    using var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            failures.Add(path + ": missing reference " + property.propertyPath);
                }
            }
        }
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}
#endif
