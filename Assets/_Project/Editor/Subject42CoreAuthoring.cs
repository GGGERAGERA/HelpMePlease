using UnityEditor;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

// Explicit, repeatable migration of the existing station composition.
public static class Subject42CoreAuthoring
{
    [MenuItem("Tools/Subject42/Author Core Pulse FX")]
    public static void Author()
    {
        var config = OrbitalPresentationConfig.Active;
        if (config.EnergyLinePrefab == null)
        {
            var lineObject = new GameObject("Station Energy Line");
            try
            {
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true; line.positionCount = 2;
                line.widthMultiplier = .045f; line.sharedMaterial = config.VisualMaterial;
                line.sortingLayerName = "Player"; line.sortingOrder = 13; line.enabled = false;
                var prefab = PrefabUtility.SaveAsPrefabAsset(lineObject,
                    "Assets/_Project/Resources/OrbitalStation/Authored/StationEnergyLine.prefab");
                config.EnergyLinePrefab = prefab.GetComponent<LineRenderer>();
            }
            finally { Object.DestroyImmediate(lineObject); }
        }
        string path = AssetDatabase.GetAssetPath(config.StationPrefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = root.GetComponent<OrbitalStationView>();
            if (view.CoreHalo == null)
            {
                var go = new GameObject("Core Energy Halo"); go.transform.SetParent(view.Core.transform.parent, false);
                go.transform.localPosition = view.Core.transform.localPosition;
                view.CoreHalo = go.AddComponent<SpriteRenderer>();
                view.CoreHalo.sprite = config.CircleSprite;
                view.CoreHalo.sharedMaterial = config.VisualMaterial;
                view.CoreHalo.sortingLayerID = view.Core.sortingLayerID;
                view.CoreHalo.sortingOrder = view.Core.sortingOrder - 1;
                view.CoreHalo.enabled = false;
            }
            if (view.CoreParticles == null)
            {
                var go = new GameObject("Core Energy Sparks"); go.transform.SetParent(view.EffectsRoot, false);
                var particles = go.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false; main.loop = true; main.maxParticles = 64;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f);
                var emission = particles.emission; emission.enabled = false;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .1f;
                var color = particles.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                color.color = gradient;
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = config.VisualMaterial;
                renderer.sortingLayerID = view.Core.sortingLayerID; renderer.sortingOrder = view.Core.sortingOrder + 1;
                view.CoreParticles = particles;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
    }
}
