#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Subject42.Combat.OrbitalStation;

// Debug setters live with their real owners. No UI edits private fields or save keys.
public partial class CurrencyManager
{
    public bool DebugSetCores(int amount)
    {
        if (!PlayerPrefs.IsSandboxActive) return false;
        TotalCores = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(CoresKey, TotalCores);
        PlayerPrefs.Save();
        NotifyCoreUpdates();
        return true;
    }
}

public sealed partial class MetaProgressionManager
{
    public bool DebugSetLevel(MetaUpgradeType type, int level)
    {
        if (!PlayerPrefs.IsSandboxActive || !System.Enum.IsDefined(typeof(MetaUpgradeType), type)) return false;
        SetLevel(type, level);
        PlayerPrefs.SetInt(GetInvestmentKey(type), 0);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
        return true;
    }

    public bool DebugSetModuleLevel(OrbitalModuleKind kind, int level)
    {
        if (!PlayerPrefs.IsSandboxActive || !IsMetaOrbitalModule(kind)) return false;
        PlayerPrefs.SetInt(GetOrbitalModuleLevelKey(kind), Mathf.Clamp(level, DefaultOrbitalModuleLevel, MaxUpgradeLevel));
        PlayerPrefs.SetInt(GetOrbitalModuleInvestmentKey(kind), 0);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
        return true;
    }
}

public sealed partial class AnomalyMetaProgressionManager
{
    public bool DebugSetLevel(AnomalyStabilizerData anomaly, int level)
    {
        if (!PlayerPrefs.IsSandboxActive || anomaly == null) return false;
        PlayerPrefs.SetInt(GetLevelKey(anomaly), Mathf.Clamp(level, DefaultLevel, anomaly.MaxMetaLevel));
        PlayerPrefs.SetInt(GetInvestmentKey(anomaly), 0);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
        return true;
    }
}

public sealed partial class MissionService
{
    // Forward-only test staging. Accept, return and payout still use normal APIs.
    // Rewinding a paid mission requires restoring the entire test profile.
    public bool DebugAdvanceTestStage(string missionId)
    {
        if (!PlayerPrefs.IsSandboxActive || !IsAvailable(missionId)) return false;
        switch (GetState(missionId))
        {
            case MissionState.Available: return Accept(missionId);
            case MissionState.Active:
                var record = Record(missionId);
                foreach (var objective in Find(missionId).Objectives)
                    if (!record.committedObjectives.Contains(objective.Id)) record.committedObjectives.Add(objective.Id);
                record.state = MissionState.ObjectiveCompleted;
                Save(); Changed?.Invoke(); return true;
            case MissionState.ObjectiveCompleted: ArriveAtBunker(); return true;
            case MissionState.ReadyToTurnIn: return Claim(missionId);
            default: return false;
        }
    }
}

public static partial class WeaponControlSettings
{
    // Profile changes are scene transitions, not application restarts.
    public static void DebugReloadFromStorage()
    {
        loaded = false;
        EnsureLoaded();
        ModeChanged?.Invoke(currentMode);
    }
}
#endif
