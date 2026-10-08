using System;
using UnityEngine;

// Optional inputs. Development adapters own these values; gameplay never knows the adapter type.
public static class RunDevelopmentOverrides
{
    public static Func<bool> SuppressTutorial { get; set; }
    public static AnomalyPowerType? SpecialPower { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SuppressTutorial = null;
        SpecialPower = null;
    }
}
