using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Root Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject metaShopPanel;
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private GameObject aboutPanel;

    [Header("Selection Steps")]
    [SerializeField] private GameObject playerSelectPanel;
    [SerializeField] private GameObject weaponSelectPanel;
    [SerializeField] private GameObject sceneSelectPanel;

    private bool isStartingGame;

    private GameObject currentRootPanel;

    private void Start()
    {
        RunSelectionManager.Instance?.ClearRunSelection();
        currentRootPanel = mainPanel;
        OpenMainPanel();
    }

    public void OpenMainPanel()
    {
        ShowRootPanel(mainPanel);
    }

    public void OpenMetaShopPanel()
    {
        ShowRootPanel(metaShopPanel);
    }

    public void OpenAboutPanel()
    {
        ShowRootPanel(aboutPanel);
    }

    public void StartSelectionFlow()
    {
        ShowRootPanel(selectionPanel);
        ShowSelectionStep(playerSelectPanel);
    }

    public void OpenWeaponSelection()
    {
        ShowRootPanel(selectionPanel);
        ShowSelectionStep(weaponSelectPanel);
    }

    public void BackToPlayerSelection()
    {
        ShowRootPanel(selectionPanel);
        ShowSelectionStep(playerSelectPanel);
    }

    public void StartGame()
    {
        if (isStartingGame)
            return;

        if (RunSelectionManager.Instance == null)
        {
            Debug.LogWarning("StartGame blocked: RunSelectionManager not found.");
            return;
        }

        if (!RunSelectionManager.Instance.HasCharacter())
        {
            Debug.LogWarning("StartGame blocked: no character selected.");
            return;
        }

        if (!RunSelectionManager.Instance.HasWeapon())
        {
            Debug.LogWarning("StartGame blocked: no weapon selected.");
            return;
        }

        isStartingGame = true;

        Time.timeScale = 1f;
        SceneManager.LoadScene("MVP");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }

    private void ShowRootPanel(GameObject target)
    {
        if (target == null)
            return;

        if (currentRootPanel != target)
        {
            UISoundPlayer.Instance?.PlayPanelSwitch();
            currentRootPanel = target;
        }

        SetActive(mainPanel, target == mainPanel);
        SetActive(metaShopPanel, target == metaShopPanel);
        SetActive(selectionPanel, target == selectionPanel);
        SetActive(aboutPanel, target == aboutPanel);
    }

    private void ShowSelectionStep(GameObject target)
    {
        SetActive(playerSelectPanel, target == playerSelectPanel);
        SetActive(weaponSelectPanel, target == weaponSelectPanel);
        SetActive(sceneSelectPanel, target == sceneSelectPanel);
    }

    private void SetActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}