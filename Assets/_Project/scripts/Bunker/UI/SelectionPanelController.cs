using UnityEngine;

public sealed class SelectionPanelController : MonoBehaviour
{
    private enum PanelType
    {
        None,
        Selection,
        Characters,
        Scenes
    }

    [SerializeField] private GameObject root;

    [Header("Panels")]
    [SerializeField] private BunkerSelectionWindow sharedSelectionWindow;
    [SerializeField] private BunkerSelectionWindow characterSelectionWindow;
    [SerializeField] private GameObject sceneSelectPanel;

    public bool IsOpen => root != null && root.activeInHierarchy;

    // Keep both serialized fields for older scene references. The approved
    // character prefab is now the single composed window for all stations.
    private BunkerSelectionWindow StationWindow => characterSelectionWindow != null
        ? characterSelectionWindow : sharedSelectionWindow;

    public void ShowSelection(IBunkerSelectionSource source, BunkerPanelManager panelManager)
        => ShowSelection(source, panelManager, StationWindow, PanelType.Selection);

    public void ShowCharacters(IBunkerSelectionSource source, BunkerPanelManager panelManager)
    {
        ShowSelection(source, panelManager);
    }

    private void ShowSelection(IBunkerSelectionSource source, BunkerPanelManager panelManager,
        BunkerSelectionWindow window, PanelType panelType)
    {
        if (root == null || window == null)
        {
            Debug.LogError("[SelectionPanelController] Selection window is not assigned.", this);
            return;
        }
        SetPanelStates(panelType);
        root.SetActive(true);
        window.Open(source, panelManager);
    }

    public void ShowScenes() => Show(PanelType.Scenes);

    public void Hide()
    {
        SetPanelStates(PanelType.None);

        if (root != null)
            root.SetActive(false);
    }

    private void Show(PanelType panelType)
    {
        GameObject target = GetPanel(panelType);

        if (root == null)
        {
            Debug.LogError("[SelectionPanelController] Root is not assigned.", this);
            return;
        }

        if (target == null)
        {
            Debug.LogError($"[SelectionPanelController] Panel '{panelType}' is not assigned.", this);
            return;
        }

        SetPanelStates(panelType);
        root.SetActive(true);
    }

    private void SetPanelStates(PanelType activePanel)
    {
        bool selecting = activePanel == PanelType.Selection;
        if (sharedSelectionWindow != null && sharedSelectionWindow != StationWindow)
            sharedSelectionWindow.CloseView();
        if (!selecting) StationWindow?.CloseView();
        else if (StationWindow != null) StationWindow.gameObject.SetActive(true);
        SetActive(sceneSelectPanel, activePanel == PanelType.Scenes);
    }

    private GameObject GetPanel(PanelType panelType)
    {
        return panelType switch
        {
            PanelType.Scenes => sceneSelectPanel,
            _ => null
        };
    }

    private static void SetActive(GameObject panel, bool isActive)
    {
        if (panel != null && panel.activeSelf != isActive)
            panel.SetActive(isActive);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (root == null)
            Debug.LogWarning("[SelectionPanelController] Root is not assigned.", this);
        if (sharedSelectionWindow == null)
            Debug.LogWarning("[SelectionPanelController] Shared selection window is not assigned.", this);
    }
#endif
}
