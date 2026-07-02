using UnityEngine;

public class BunkerHallway : MonoBehaviour
{
    [Header("Настройки коридора")]
    [SerializeField] private string hallwayID = "Hallway_01"; 
    [SerializeField] private int unlockCost = 500;
    [SerializeField] private GameObject uiPanel; // Панель покупки (БЕЗ CanvasGroup, просто SetActive)
    [SerializeField] private Collider2D interactionZone; 
    [SerializeField] private GameObject[] zonesToHide; // Вставьте сюда ZoneOpacity зоны!
    [SerializeField] private BunkerDoor[] doorsInHallway; // Ссылки на двери

    private bool _isUnlocked;
    private bool _isUIOpen;

    private void Awake()
    {
        LoadState();
        InitializeState();
    }

    private void LoadState()
    {
        _isUnlocked = PlayerPrefs.GetInt($"Hallway_{hallwayID}", 0) == 1;
    }

    private void InitializeState()
    {
        if (uiPanel != null) uiPanel.SetActive(false);

        if (_isUnlocked)
        {
            DisableInteraction();
            SetZonesActive(true);
            SetDoorsInteraction(true);
        }
        else
        {
            SetZonesActive(false);
            SetDoorsInteraction(false);
        }
    }

    private void OnMouseDown()
    {
        if (!_isUnlocked) ShowUI();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isUnlocked && other.CompareTag("Player")) ShowUI();
    }

    private void ShowUI()
    {
        if (_isUIOpen) return;
        _isUIOpen = true;
        if (uiPanel != null) uiPanel.SetActive(true);
    }

    public void OnBuyConfirmed()
    {
        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendGold(unlockCost))
        {
            UnlockHallway();
        }
        else
        {
            Debug.Log("Недостаточно монет для покупки коридора!");
        }
    }

    public void CloseUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        _isUIOpen = false;
    }

    private void UnlockHallway()
    {
        _isUnlocked = true;
        PlayerPrefs.SetInt($"Hallway_{hallwayID}", 1);
        PlayerPrefs.Save();

        CloseUI();
        DisableInteraction(); 
        SetZonesActive(true);  
        SetDoorsInteraction(true); 
    }

    private void DisableInteraction()
    {
        if (interactionZone != null) interactionZone.enabled = false;
        if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = false;
    }

    private void SetZonesActive(bool isActive)
    {
        foreach (var zone in zonesToHide) if (zone != null) zone.SetActive(isActive);
    }

    private void SetDoorsInteraction(bool canInteract)
    {
        foreach (var door in doorsInHallway)
        {
            if (door != null) door.SetInteractionEnabled(canInteract);
        }
    }
}