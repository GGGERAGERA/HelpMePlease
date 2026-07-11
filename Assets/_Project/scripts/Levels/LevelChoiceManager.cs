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
            int index = Random.Range(0, pool.Count);
            LevelNodeData node = pool[index];

            currentChoices.Add(node);
            pool.RemoveAt(index);
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

        foreach (LevelNodeData node in availableNodes)
        {
            if (node == null)
                continue;

            pool.Add(node);
        }

        return pool;
    }

    private void SelectNode(LevelNodeData node)
    {
        if (node == null || !isChoosing)
            return;

        isChoosing = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        RunStateManager runState = RunStateManager.EnsureExists();
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
