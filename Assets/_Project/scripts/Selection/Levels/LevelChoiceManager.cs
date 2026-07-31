using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelChoiceManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private LevelChoicePanelView panelView;

    [Header("Available Nodes")]
    [SerializeField] private LevelNodeData[] availableNodes;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "MVP";

    [Header("Choice Settings")]
    [SerializeField, Min(1)] private int choicesCount = 3;

    private readonly List<LevelNodeData> currentChoices = new();
    private bool isChoosing;

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
        if (isChoosing)
            return;

        currentChoices.Clear();

        List<LevelNodeData> pool = BuildPool();

        while (currentChoices.Count < choicesCount && pool.Count > 0)
        {
            LevelNodeData node = TakeWeightedChoice(pool);

            if (node != null)
                currentChoices.Add(node);
        }

        if (currentChoices.Count == 0)
        {
            Debug.LogWarning("[LevelChoiceManager] No level nodes available.");
            return;
        }

        if (panelView == null)
        {
            Debug.LogError("[LevelChoiceManager] PanelView is not assigned.");
            return;
        }
        RunMessageService.Instance?.Show(RunMessageType.LevelChoiceOpened);
        isChoosing = true;
        Time.timeScale = 0f;
        panelView.Show(currentChoices, SelectNode);
    }

    private List<LevelNodeData> BuildPool()
    {
        List<LevelNodeData> pool = new();

        if (availableNodes == null)
            return pool;

        RunStateManager runState = RunStateManager.Instance;
        int nextRunLevel = runState != null
            ? runState.CurrentLevel + 1
            : 2;
        LevelNodeData currentNode = runState != null
            ? runState.SelectedLevelNode
            : null;

        foreach (LevelNodeData node in availableNodes)
        {
            if (node == null)
                continue;

            if (nextRunLevel < node.MinimumRunLevel)
                continue;

            if (node.MaximumRunLevel > 0 && nextRunLevel > node.MaximumRunLevel)
                continue;

            if (node.ChoiceWeight <= 0f)
                continue;

            if (!node.AllowSameWeatherAsCurrent &&
                currentNode != null &&
                HasSameGlobalModifier(node, currentNode))
            {
                continue;
            }

            pool.Add(node);
        }

        return pool;
    }

    private static bool HasSameGlobalModifier(
        LevelNodeData candidate,
        LevelNodeData current
    )
    {
        WorldRuleData candidateRule = candidate.WorldRule;
        WorldRuleData currentRule = current.WorldRule;

        if (candidateRule != null && currentRule != null)
        {
            if (candidateRule.RuleType == WorldRuleType.None ||
                currentRule.RuleType == WorldRuleType.None)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(candidateRule.Id) &&
                !string.IsNullOrWhiteSpace(currentRule.Id))
            {
                return string.Equals(
                    candidateRule.Id,
                    currentRule.Id,
                    System.StringComparison.Ordinal
                );
            }

            return candidateRule.RuleType == currentRule.RuleType;
        }

        return false;
    }

    private LevelNodeData TakeWeightedChoice(List<LevelNodeData> pool)
    {
        float totalWeight = 0f;

        for (int i = 0; i < pool.Count; i++)
            totalWeight += pool[i].ChoiceWeight;

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;

        for (int i = 0; i < pool.Count; i++)
        {
            roll -= pool[i].ChoiceWeight;

            if (roll <= 0f)
            {
                LevelNodeData selected = pool[i];
                pool.RemoveAt(i);
                return selected;
            }
        }

        int lastIndex = pool.Count - 1;
        LevelNodeData fallback = pool[lastIndex];
        pool.RemoveAt(lastIndex);
        return fallback;
    }

    private void SelectNode(LevelNodeData node)
    {
        if (node == null || !isChoosing)
            return;

        isChoosing = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        RunStateManager runState = RunStateManager.EnsureExists();
        runState.CommitCurrentSceneStats();
        runState.SaveExperienceState();
        runState.SavePlayerState(player);
        runState.AdvanceLevel();
        runState.SetSelectedLevelNode(node);

        Time.timeScale = 1f;

        if (panelView != null)
            panelView.Hide();

        string targetScene = string.IsNullOrWhiteSpace(node.SceneName)
            ? gameplaySceneName
            : node.SceneName;

        SceneManager.LoadScene(targetScene);
    }
}
