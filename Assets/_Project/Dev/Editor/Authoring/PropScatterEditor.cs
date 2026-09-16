using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PropScatter))]
public sealed class PropScatterEditor : Editor
{
    private const string GeneratedRootName = "Generated";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "generatedRoot");
        serializedObject.ApplyModifiedProperties();

        PropScatter scatter = (PropScatter)target;
        bool hasSources = HasValidSource(scatter);
        bool hasGeneratedObjects = scatter.GeneratedRoot != null;

        if (!hasSources && scatter.Count > 0)
        {
            EditorGUILayout.HelpBox("Assign at least one prefab source before generating.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!hasSources || hasGeneratedObjects || scatter.Count <= 0))
        {
            if (GUILayout.Button("GENERATE", GUILayout.Height(28f)))
            {
                Generate(scatter, false);
            }
        }

        using (new EditorGUI.DisabledScope(!hasSources || scatter.Count <= 0))
        {
            if (GUILayout.Button("REGENERATE", GUILayout.Height(24f)))
            {
                Generate(scatter, true);
            }
        }

        using (new EditorGUI.DisabledScope(!hasGeneratedObjects))
        {
            if (GUILayout.Button("CLEAR", GUILayout.Height(24f)))
            {
                Clear(scatter);
            }
        }
    }

    private static bool HasValidSource(PropScatter scatter)
    {
        foreach (GameObject source in scatter.Sources)
        {
            if (source != null && PrefabUtility.IsPartOfPrefabAsset(source))
            {
                return true;
            }
        }

        return false;
    }

    private static void Generate(PropScatter scatter, bool replaceExisting)
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(replaceExisting ? "Regenerate Prop Scatter" : "Generate Prop Scatter");

        if (replaceExisting)
        {
            ClearInternal(scatter);
        }

        GameObject rootObject = new GameObject(GeneratedRootName);
        rootObject.transform.SetParent(scatter.transform, false);
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Scatter Generated Root");

        Undo.RecordObject(scatter, "Assign Scatter Generated Root");
        scatter.SetGeneratedRoot(rootObject.transform);

        List<GameObject> validSources = GetValidSources(scatter);
        System.Random random = new System.Random(scatter.Seed);

        for (int i = 0; i < scatter.Count; i++)
        {
            GameObject source = validSources[random.Next(validSources.Count)];
            GameObject instance = PrefabUtility.InstantiatePrefab(source, rootObject.transform) as GameObject;
            if (instance == null)
            {
                continue;
            }

            instance.name = $"{source.name}_{i + 1:000}";

            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = GetPosition(scatter, random);

            if (scatter.RandomRotation)
            {
                float angle = NextFloat(random, 0f, 360f);
                instanceTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            float scale = NextFloat(random, scatter.MinScale, scatter.MaxScale);
            instanceTransform.localScale = source.transform.localScale * scale;
            Undo.RegisterCreatedObjectUndo(instance, "Create Scattered Prop");
        }

        EditorUtility.SetDirty(scatter);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void Clear(PropScatter scatter)
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Prop Scatter");
        ClearInternal(scatter);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void ClearInternal(PropScatter scatter)
    {
        Transform root = scatter.GeneratedRoot;
        if (root == null)
        {
            return;
        }

        Undo.RecordObject(scatter, "Clear Scatter Generated Root");
        scatter.SetGeneratedRoot(null);
        Undo.DestroyObjectImmediate(root.gameObject);
        EditorUtility.SetDirty(scatter);
    }

    private static List<GameObject> GetValidSources(PropScatter scatter)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject source in scatter.Sources)
        {
            if (source != null && PrefabUtility.IsPartOfPrefabAsset(source))
            {
                result.Add(source);
            }
        }

        return result;
    }

    private static Vector3 GetPosition(PropScatter scatter, System.Random random)
    {
        float halfWidth = scatter.AreaWidth * 0.5f;
        float halfHeight = scatter.AreaHeight * 0.5f;
        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return Vector3.zero;
        }

        float angle = NextFloat(random, 0f, Mathf.PI * 2f);
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        float xLimit = Mathf.Abs(cos) < 0.0001f ? float.PositiveInfinity : halfWidth / Mathf.Abs(cos);
        float yLimit = Mathf.Abs(sin) < 0.0001f ? float.PositiveInfinity : halfHeight / Mathf.Abs(sin);
        float areaLimit = Mathf.Min(xLimit, yLimit);
        float effectiveMax = Mathf.Min(scatter.MaxDistanceFromCenter, areaLimit);
        float effectiveMin = Mathf.Min(scatter.MinDistanceFromCenter, effectiveMax);

        // sqrt(u) is area-uniform. Raising the exponent progressively pulls props inward.
        float exponent = Mathf.Lerp(0.5f, 2.5f, scatter.CenterBias);
        float radialT = Mathf.Pow((float)random.NextDouble(), exponent);
        float radius = Mathf.Lerp(effectiveMin, effectiveMax, radialT);
        return new Vector3(cos * radius, sin * radius, 0f);
    }

    private static float NextFloat(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
