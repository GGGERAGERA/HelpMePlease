using System;
using System.Collections.Generic;
using UnityEngine;

public static class RunWeightedSelection
{
    public static T Select<T>(IReadOnlyList<T> candidates, Func<T, float> weight, float randomValue) where T : UnityEngine.Object
    {
        float total = 0f;
        foreach (var candidate in candidates) if (candidate != null) total += Mathf.Max(0f, weight(candidate));
        if (total <= 0f) return null;
        float roll = Mathf.Clamp01(randomValue) * total;
        T last = null;
        foreach (var candidate in candidates)
        {
            if (candidate == null) continue;
            float value = Mathf.Max(0f, weight(candidate));
            if (value <= 0f) continue;
            last = candidate; roll -= value;
            if (roll < 0f) return candidate;
        }
        return last;
    }
}
