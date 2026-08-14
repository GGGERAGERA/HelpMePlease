using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Phase8AAnomalyPresentationValidation
{
    [MenuItem("Tools/Subject 42/Validate Phase 8A Anomaly Presentation")]
    public static void Run()
    {
        ValidateZonePrefabs();
        ValidateEmptyAndAssignedHooks();
        ValidateMonochromeConversion();
        Debug.Log(
            "[Phase8AValidation] PASS: zone assets, empty/assigned art hooks, " +
            "visibility, segment alignment and monochrome value preservation.");
    }

    private static void ValidateZonePrefabs()
    {
        GameObject stasis = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/WorldAnomalies/StasisZone.prefab");
        GameObject gravity = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/WorldAnomalies/GravityZone.prefab");
        Require(stasis != null &&
                stasis.GetComponentInChildren<StasisZone>(true) != null,
            "Stasis prefab/component is missing.");
        Require(gravity != null &&
                gravity.GetComponentInChildren<GravityZone>(true) != null,
            "Gravity prefab/component is missing.");
    }

    private static void ValidateEmptyAndAssignedHooks()
    {
        GameObject host = new("Phase8A_HookHost");
        GameObject accent = new("Phase8A_TestAccent");
        accent.AddComponent<SpriteRenderer>();

        try
        {
            AnomalyArtHooks empty = AnomalyArtHooks.Create(
                host.transform, default, "EMPTY TEST");
            Require(empty != null && empty.RootCount == 4,
                "Empty hook set must create four logical roots.");
            Require(empty.InstantiatedArtCount == 0,
                "Empty hook set instantiated art unexpectedly.");

            AnomalyArtHookSet assignedSet = new(
                accent, accent, accent, accent);
            AnomalyArtHooks assigned = AnomalyArtHooks.Create(
                host.transform, assignedSet, "ASSIGNED TEST");
            Require(assigned.InstantiatedArtCount == 4,
                "Assigned hook set did not instantiate all four accents.");

            assigned.SetVisible(false);
            Require(!assigned.IsVisible, "Assigned hooks did not hide.");
            assigned.SetVisible(true);
            Require(assigned.IsVisible, "Assigned hooks did not restore.");

            assigned.SetBoundarySize(new Vector2(12f, 8f));
            assigned.AlignPatternToWorldSegment(
                new Vector2(-3f, 2f), new Vector2(5f, 2f));
            Transform pattern = assigned.transform.Find("PATTERN Accents");
            Require(pattern != null && Approximately(pattern.localScale.x, 8f),
                "Pattern hook did not fit the live segment length.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(accent);
        }
    }

    private static void ValidateMonochromeConversion()
    {
        MethodInfo method = typeof(ProductionSectorDebugController).GetMethod(
            "BuildMonochromeValues",
            BindingFlags.NonPublic | BindingFlags.Static);
        Require(method != null, "Monochrome conversion method is missing.");

        AnomalyVisualTuningValues original = new()
        {
            PrimaryColor = new Color(1f, 0f, 0f, 0.7f),
            SecondaryColor = new Color(0f, 1f, 0f, 0.4f),
            FillColor = new Color(0f, 0f, 1f, 0.2f),
            BoundaryWidth = 0.37f,
            PulseSpeed = 1.23f,
            PatternSpeed = 2.34f
        };
        AnomalyVisualTuningCapabilities capabilities =
            AnomalyVisualTuningCapabilities.PrimaryColor |
            AnomalyVisualTuningCapabilities.SecondaryColor |
            AnomalyVisualTuningCapabilities.FillColor;
        object result = method.Invoke(null, new object[] { original, capabilities });
        AnomalyVisualTuningValues monochrome =
            (AnomalyVisualTuningValues)result;

        Require(Approximately(monochrome.PrimaryColor.r,
                monochrome.SecondaryColor.r) &&
                Approximately(monochrome.PrimaryColor.a, 0.7f) &&
                Approximately(monochrome.SecondaryColor.a, 0.4f) &&
                Approximately(monochrome.FillColor.a, 0.2f),
            "Monochrome conversion did not preserve color alpha.");
        Require(Approximately(monochrome.BoundaryWidth, 0.37f) &&
                Approximately(monochrome.PulseSpeed, 1.23f) &&
                Approximately(monochrome.PatternSpeed, 2.34f),
            "Monochrome conversion changed geometry or motion values.");
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                $"[Phase8AValidation] {message}");
    }
}
