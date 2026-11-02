using UnityEngine;

public sealed class SelectionPanelController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private BunkerSelectionWindow sharedSelectionWindow;

    public bool IsOpen => root != null && root.activeInHierarchy;

    public void ShowSelection(IBunkerSelectionSource source, BunkerPanelManager panelManager)
    {
        if (root == null || sharedSelectionWindow == null)
        {
            Debug.LogError("[SelectionPanelController] Selection window is not assigned.", this);
            return;
        }
        root.SetActive(true);
        sharedSelectionWindow.Open(source, panelManager);
    }

    public void Hide()
    {
        sharedSelectionWindow?.CloseView();
        if (root != null)
            root.SetActive(false);
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
