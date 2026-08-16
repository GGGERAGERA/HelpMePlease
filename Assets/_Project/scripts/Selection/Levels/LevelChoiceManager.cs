using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelChoiceManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private LevelChoicePanelView panelView;

    [Header("Available World Rules")]
    [SerializeField] private WorldRuleData[] availableWorldRules;

    [Header("Local Anomaly")]
    [SerializeField] private LocalAnomalyData defaultLocalAnomaly;

    [Header("Stage Profiles")]
    [SerializeField] private StageProfileData[] stageProfiles;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "MVP";

    [Header("Choice Settings")]
    [SerializeField, Min(1)] private int choicesCount = 3;

    private readonly List<WorldRuleData> currentChoices = new();
    private readonly Dictionary<WorldRuleData, RunSector>
        currentSectorOptions = new();
    private bool isChoosing;

    public bool IsChoosing => isChoosing;

    private void Awake()
    {
        if (panelView == null)
        {
            panelView = FindFirstObjectByType<LevelChoicePanelView>(
                FindObjectsInactive.Include
            );
        }

        if (panelView != null)
            panelView.Hide();
    }

    public void ShowChoices()
    {
        TryShowChoices();
    }

    public bool TryShowChoices()
    {
        if (isChoosing)
            return false;

        currentChoices.Clear();
        currentSectorOptions.Clear();

        RunStateManager runState = RunStateManager.Instance;
        RunSector currentSector = runState != null
            ? runState.CurrentSector
            : null;

        if (currentSector == null)
        {
            Debug.LogError(
                "[LevelChoiceManager] CurrentSector is missing. " +
                "Sector choice cannot be opened."
            );
            return false;
        }

        if (defaultLocalAnomaly == null)
        {
            Debug.LogError(
                "[LevelChoiceManager] Default LocalAnomaly is missing. " +
                "Sector choice cannot be opened."
            );
            return false;
        }

        int nextSectorNumber = currentSector.SectorNumber + 1;
        StageProfileData nextStageProfile = GetStageProfile(nextSectorNumber);

        if (nextStageProfile == null)
        {
            Debug.LogError(
                $"[LevelChoiceManager] StageProfile for sector " +
                $"{nextSectorNumber} was not found."
            );
            return false;
        }

        List<WorldRuleData> pool = BuildPool();

        if (pool.Count < choicesCount)
        {
            Debug.LogError(
                $"[LevelChoiceManager] At least {choicesCount} unique " +
                $"World Rules are required, but only {pool.Count} are available."
            );
            return false;
        }

        while (currentChoices.Count < choicesCount && pool.Count > 0)
        {
            int selectedIndex = Random.Range(0, pool.Count);
            WorldRuleData rule = pool[selectedIndex];
            pool.RemoveAt(selectedIndex);

            RunSector option = new(
                nextSectorNumber,
                nextStageProfile,
                rule,
                defaultLocalAnomaly
            );

            currentChoices.Add(rule);
            currentSectorOptions.Add(rule, option);
        }

        if (currentChoices.Count != choicesCount)
        {
            Debug.LogError(
                "[LevelChoiceManager] A complete sector choice could not be built."
            );
            currentChoices.Clear();
            currentSectorOptions.Clear();
            return false;
        }

        if (panelView == null)
        {
            Debug.LogError("[LevelChoiceManager] PanelView is not assigned.");
            return false;
        }

#if UNITY_EDITOR
        Debug.Log(
            "[SectorChoice]\n" +
            $"Current={currentSector.SectorNumber}\n" +
            $"Next={nextSectorNumber}\n" +
            $"Rules={GetOptionRuleIds()}\n" +
            $"StageProfile='{nextStageProfile.name}'\n" +
            $"LocalAnomaly='{defaultLocalAnomaly.name}'"
        );
#endif

        RunMessageService.Instance?.Show(RunMessageType.LevelChoiceOpened);
        isChoosing = true;
        Time.timeScale = 0f;
        panelView.Show(
            currentChoices,
            currentSectorOptions,
            nextSectorNumber,
            SelectRule
        );
        return true;
    }

    private List<WorldRuleData> BuildPool()
    {
        List<WorldRuleData> pool = new();
        HashSet<string> ruleIds = new(System.StringComparer.Ordinal);

        if (availableWorldRules == null)
            return pool;

        foreach (WorldRuleData rule in availableWorldRules)
        {
            if (rule == null || rule.RuleType == WorldRuleType.None)
                continue;

            if (!ruleIds.Add(GetRuleId(rule)))
                continue;

            pool.Add(rule);
        }

        return pool;
    }

    private StageProfileData GetStageProfile(int sectorNumber)
    {
        if (stageProfiles == null || sectorNumber < 1)
            return null;

        StageProfileData match = null;

        for (int i = 0; i < stageProfiles.Length; i++)
        {
            StageProfileData profile = stageProfiles[i];

            if (profile == null || profile.SectorNumber != sectorNumber)
                continue;

            if (match != null)
            {
                Debug.LogError(
                    $"[LevelChoiceManager] More than one StageProfile " +
                    $"uses sector number {sectorNumber}."
                );
                return null;
            }

            match = profile;
        }

        return match;
    }

    private static string GetRuleId(WorldRuleData rule)
    {
        if (rule == null)
            return "<null>";

        return !string.IsNullOrWhiteSpace(rule.Id)
            ? rule.Id
            : rule.RuleType.ToString();
    }

    private void SelectRule(WorldRuleData rule)
    {
        if (rule == null || !isChoosing)
            return;

        if (!currentSectorOptions.TryGetValue(rule, out RunSector sector) ||
            sector == null)
        {
            Debug.LogError(
                "[LevelChoiceManager] The selected RunSector option is missing."
            );
            return;
        }

        isChoosing = false;
        TransitionToSector(sector);
    }

    private void TransitionToSector(RunSector sector)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        RunStateManager runState = RunStateManager.EnsureExists();
        runState.CommitCurrentSceneStats();
        runState.SaveExperienceState();
        runState.SavePlayerState(player);
        runState.SetCurrentSector(sector);

        Time.timeScale = 1f;
        panelView?.Hide();
        SceneManager.LoadScene(gameplaySceneName);
    }

#if UNITY_EDITOR
    private string GetOptionRuleIds()
    {
        List<string> ids = new(currentChoices.Count);

        for (int i = 0; i < currentChoices.Count; i++)
            ids.Add(GetRuleId(currentChoices[i]));

        return string.Join(",", ids);
    }
#endif
}
