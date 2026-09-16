#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProductionSceneCompositionAuthoring
{
    public const string PrefabPath = "Assets/_Project/prefabs/Bootstrap/ProductionSceneComposition.prefab";

    public static ProductionSceneComposition EnsureScene(Scene scene)
    {
        var composition = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ProductionSceneComposition>(true)).FirstOrDefault();
        if (composition == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new System.InvalidOperationException("Authored production scene composition prefab is missing.");
            composition = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<ProductionSceneComposition>();
        }
        var serialized = new SerializedObject(composition);
        BindLocal<LocalizationService>("localization");
        BindLocal<AudioService>("audio");
        BindLocal<UnlockProgressService>("unlocks");
        BindLocal<BunkerStationProgressionService>("bunkerProgression");
        BindLocal<SceneTransitionOverlay>("transition");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return composition;

        void BindLocal<T>(string field) where T : Component
        {
            var local = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component.gameObject.activeInHierarchy);
            if (local != null) serialized.FindProperty(field).objectReferenceValue = local;
        }
    }
}
#endif
