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

    public void ShowSelection(IBunkerSelectionSource source, BunkerPanelManager panelManager)
        => ShowSelection(source, panelManager, sharedSelectionWindow, PanelType.Selection);

    public void ShowCharacters(IBunkerSelectionSource source, BunkerPanelManager panelManager)
    {
        // Scenes without an authored character layout keep their existing selection view.
        bool hasCharacterLayout = characterSelectionWindow != null;
        ShowSelection(source, panelManager,
            hasCharacterLayout ? characterSelectionWindow : sharedSelectionWindow,
            hasCharacterLayout ? PanelType.Characters : PanelType.Selection);
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
        if (activePanel != PanelType.Selection)
            sharedSelectionWindow?.CloseView();
        else if (sharedSelectionWindow != null)
            sharedSelectionWindow.gameObject.SetActive(true);
        if (activePanel != PanelType.Characters)
            characterSelectionWindow?.CloseView();
        else if (characterSelectionWindow != null)
            characterSelectionWindow.gameObject.SetActive(true);
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
