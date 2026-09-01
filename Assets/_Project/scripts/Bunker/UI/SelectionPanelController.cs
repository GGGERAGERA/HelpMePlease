using UnityEngine;

public sealed class SelectionPanelController : MonoBehaviour
{
    private enum PanelType
    {
        None,
        Selection,
        Scenes
    }

    [SerializeField] private GameObject root;

    [Header("Panels")]
    [SerializeField] private BunkerSelectionWindow sharedSelectionWindow;
    [SerializeField] private GameObject sceneSelectPanel;

    public bool IsOpen => root != null && root.activeInHierarchy;

    public void ShowSelection(
        IBunkerSelectionSource source,
        BunkerPanelManager panelManager)
    {
        if (root == null || sharedSelectionWindow == null)
        {
            Debug.LogError(
                "[SelectionPanelController] Shared selection window is not assigned.",
                this);
            return;
        }

        SetPanelStates(PanelType.Selection);
        root.SetActive(true);
        sharedSelectionWindow.Open(source, panelManager);
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
        if (activePanel != PanelType.Selection)
            sharedSelectionWindow?.CloseView();
        else if (sharedSelectionWindow != null)
            sharedSelectionWindow.gameObject.SetActive(true);
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
