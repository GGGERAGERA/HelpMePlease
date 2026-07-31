#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LocalAnomalyMigrationValidator
{
    private const string MenuPath =
        "Tools/Local Anomalies/Validate Level Node Migration";
    private const string ActiveFlowScenePath =
        "Assets/_Project/Scenes/MVP.unity";

    [MenuItem(MenuPath)]
    private static void ValidateLevelNodes()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelNodeData");
        var levelNodes = new List<LevelNodeData>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelNodeData node =
                AssetDatabase.LoadAssetAtPath<LevelNodeData>(path);

            if (node != null)
                levelNodes.Add(node);
        }

        levelNodes.Sort((left, right) => string.Compare(
            AssetDatabase.GetAssetPath(left),
            AssetDatabase.GetAssetPath(right),
            StringComparison.Ordinal
        ));

        var activeNodes = new HashSet<LevelNodeData>();
        string[] dependencies =
            AssetDatabase.GetDependencies(ActiveFlowScenePath, true);

        foreach (string path in dependencies)
        {
            LevelNodeData node =
                AssetDatabase.LoadAssetAtPath<LevelNodeData>(path);

            if (node != null)
                activeNodes.Add(node);
        }

        int activeErrors = 0;
        int inactiveWarnings = 0;
        var activeEntries = new StringBuilder();
        var inactiveEntries = new StringBuilder();

        foreach (LevelNodeData node in levelNodes)
        {
            bool active = activeNodes.Contains(node);
            bool missing = node.LocalAnomaly == null;
            string path = AssetDatabase.GetAssetPath(node);
            string anomaly = missing
                ? "NULL"
                : $"{node.LocalAnomaly.Id} " +
                  $"({node.LocalAnomaly.AnomalyType})";

            if (active)
            {
                activeEntries.Append(missing ? "[ERROR] " : "[OK] ")
                    .Append(path)
                    .Append(" | localAnomaly: ")
                    .Append(anomaly)
                    .AppendLine();

                if (missing)
                    activeErrors++;

                continue;
            }

            if (!missing)
                continue;

            inactiveWarnings++;
            inactiveEntries.Append("[WARNING] ")
                .Append(path)
                .AppendLine(" | localAnomaly: NULL");
        }

        var report = new StringBuilder()
            .AppendLine("[Local Anomaly Migration Validation]")
            .Append("Active flow scene: ")
            .AppendLine(ActiveFlowScenePath)
            .Append("LevelNodeData found: ")
            .AppendLine(levelNodes.Count.ToString())
            .Append("Active flow nodes: ")
            .AppendLine(activeNodes.Count.ToString())
            .Append("Active flow errors: ")
            .AppendLine(activeErrors.ToString())
            .Append("Inactive assets awaiting migration: ")
            .AppendLine(inactiveWarnings.ToString())
            .AppendLine()
            .AppendLine("Active flow:")
            .Append(activeEntries);

        if (activeErrors > 0)
            Debug.LogError(report.ToString());
        else
            Debug.Log(report.ToString());

        if (inactiveWarnings > 0)
        {
            Debug.LogWarning(
                new StringBuilder()
                    .AppendLine("[Local Anomaly Migration Validation]")
                    .AppendLine("Inactive assets awaiting migration:")
                    .Append(inactiveEntries)
                    .ToString()
            );
        }
    }
}
#endif
