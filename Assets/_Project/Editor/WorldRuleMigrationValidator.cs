#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WorldRuleMigrationValidator
{
    private const string MenuPath =
        "Tools/World Rules/Validate Level Node Migration";

    [MenuItem(MenuPath)]
    private static void ValidateLevelNodes()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelNodeData");
        var levelNodes = new List<LevelNodeData>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelNodeData levelNode =
                AssetDatabase.LoadAssetAtPath<LevelNodeData>(path);

            if (levelNode != null)
                levelNodes.Add(levelNode);
        }

        levelNodes.Sort((left, right) => string.Compare(
            AssetDatabase.GetAssetPath(left),
            AssetDatabase.GetAssetPath(right),
            StringComparison.Ordinal
        ));

        int missingCount = 0;
        var missingEntries = new StringBuilder();

        foreach (LevelNodeData levelNode in levelNodes)
        {
            if (levelNode.WorldRule != null)
                continue;

            missingCount++;
            missingEntries.Append("- ")
                .Append(AssetDatabase.GetAssetPath(levelNode))
                .Append(" | weatherType: ")
                .Append(levelNode.weatherType)
                .AppendLine();
        }

        var report = new StringBuilder()
            .AppendLine("[World Rule Migration Validation]")
            .Append("LevelNodeData found: ")
            .AppendLine(levelNodes.Count.ToString())
            .Append("Migrated (worldRule assigned): ")
            .AppendLine((levelNodes.Count - missingCount).ToString())
            .Append("Missing worldRule: ")
            .AppendLine(missingCount.ToString());

        if (missingCount > 0)
        {
            report.AppendLine()
                .AppendLine("Assets awaiting migration:")
                .Append(missingEntries);
        }
        else
        {
            report.AppendLine()
                .AppendLine("All LevelNodeData assets have a World Rule.");
        }

        Debug.Log(report.ToString());
    }
}
#endif
