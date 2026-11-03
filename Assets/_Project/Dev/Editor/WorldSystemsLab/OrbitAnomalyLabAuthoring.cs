#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

public static class OrbitAnomalyLabAuthoring
{
    [MenuItem("Tools/Subject42/Dev/WorldSystemsLab/Prepare Orbit Anomaly Experiment")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve the edited scene before preparing the lab.");
        var scene = EditorSceneManager.OpenScene(WorldSystemsLabController.ScenePath, OpenSceneMode.Single);
        var world = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldSystemsLabController>(true)).Single();
        var experiment = world.GetComponent<WorldSystemsLabOrbitExperiment>();
        if (experiment != null) return; // Idempotent: never overwrite Inspector tuning.
        var lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).Where(l => l.enabled && l.lightType == Light2D.LightType.Global).ToArray();
        foreach (var light in lights) light.enabled = false;
        var sourceScene = EditorSceneManager.OpenPreviewScene(EnemyOrbitalLabController.ScenePath);
        try
        {
            var source = sourceScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyOrbitalLabController>(true)).Single();
            var root = new GameObject("Segmented Orbit (anomaly experiment only)"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root,scene);
            var orbit = root.AddComponent<EnemyOrbitalLabController>(); EditorUtility.CopySerialized(source,orbit);
            var worldData = new SerializedObject(world);
            orbit.ScenePlayer = ((Transform)worldData.FindProperty("player").objectReferenceValue).GetComponent<PlayerHealth>();
            orbit.CameraRig = (CameraFollow)worldData.FindProperty("cameraRig").objectReferenceValue;
            orbit.Rewards = null; orbit.LabUpgradePanel = null;
            orbit.MaximumCenterOffset = 0f; orbit.ShowCenterMarker = false;
            orbit.SegmentedEnemyDensity = 12;
            experiment = world.gameObject.AddComponent<WorldSystemsLabOrbitExperiment>();
            var data = new SerializedObject(experiment);
            data.FindProperty("world").objectReferenceValue = world;
            data.FindProperty("orbit").objectReferenceValue = orbit;
            string[] names = { "Stasis", "Berserk", "Glitch", "Gravity", "ExplosiveZone" };
            var zones = data.FindProperty("zoneAssets"); zones.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                zones.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(
                    "Assets/_Project/Data/Anomalies/LocalAnomaly_" + names[i] + ".asset");
            data.ApplyModifiedPropertiesWithoutUndo();
            worldData.FindProperty("orbitExperiment").objectReferenceValue = experiment;
            worldData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world); EditorUtility.SetDirty(orbit); EditorUtility.SetDirty(experiment);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(sourceScene);
            foreach (var light in lights) if (light != null) light.enabled = true;
        }
    }
}
#endif
