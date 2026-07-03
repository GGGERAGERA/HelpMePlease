using UnityEngine;

public sealed class SelectionPanelController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Tabs")]
    [SerializeField] private GameObject playerSelectPanel;
    [SerializeField] private GameObject weaponSelectPanel;
    [SerializeField] private GameObject sceneSelectPanel;

    private void Awake()
    {
        Hide();
    }

    public void ShowWeapons()
    {
        ShowOnly(weaponSelectPanel);
    }

    public void ShowCharacters()
    {
        ShowOnly(playerSelectPanel);
    }

    public void ShowScenes()
    {
        ShowOnly(sceneSelectPanel);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void ShowOnly(GameObject target)
    {
        if (root != null)
            root.SetActive(true);

        if (playerSelectPanel != null)
            playerSelectPanel.SetActive(playerSelectPanel == target);

        if (weaponSelectPanel != null)
            weaponSelectPanel.SetActive(weaponSelectPanel == target);

        if (sceneSelectPanel != null)
            sceneSelectPanel.SetActive(sceneSelectPanel == target);
    }
}