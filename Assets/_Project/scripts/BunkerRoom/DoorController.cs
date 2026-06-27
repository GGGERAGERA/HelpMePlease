using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DoorController : MonoBehaviour
{
    // ============================================
    //  ГЛОБАЛЬНОЕ СОСТОЯНИЕ
    // ============================================
    
    public static bool IsDoorLocked = true;

    // ============================================
    //  ENUM СОСТОЯНИЙ ДВЕРИ
    // ============================================
    
    public enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    // ============================================
    //  ССЫЛКИ
    // ============================================

    [Header("Триггеры")]
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private Collider2D openTrigger;

    [Header("UI")]
    [SerializeField] private CanvasGroup interactionPanel;
    
    [SerializeField] private Button openButton;
    [SerializeField] private Button lockButton;
    [SerializeField] private TextMeshProUGUI lockButtonText;

    [Header("Анимация")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string isOpenParam = "Open";

    [Header("Настройки")]
    [SerializeField] private float holdTimeToShowUI = 1f;
    [SerializeField] private float uiFadeSpeed = 5f;

    [Header("Отладка")]
    [SerializeField] private bool debugMode = false;

    // ============================================
    //  ВНУТРЕННИЕ ПЕРЕМЕННЫЕ
    // ============================================
    
    private Camera mainCam;
    private Transform playerTransform;
    
    private DoorState currentState = DoorState.Closed;
    private bool isPlayerInInteractionTrigger = false;
    private bool isPlayerInOpenTrigger = false;
    private bool isCursorInTrigger = false;
    private float holdTimer = 0f;
    private float currentUIAlpha = 0f;

    // ============================================
    //  ИНИЦИАЛИЗАЦИЯ
    // ============================================

    private void Awake()
    {
        mainCam = Camera.main;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        if (openButton != null)
            openButton.onClick.AddListener(OnOpenButtonClicked);
        
        if (lockButton != null)
            lockButton.onClick.AddListener(OnLockButtonClicked);

        if (interactionPanel != null)
        {
            interactionPanel.alpha = 0f;
            interactionPanel.blocksRaycasts = false;
            interactionPanel.interactable = false;
        }

        UpdateLockButtonText();
        currentState = DoorState.Closed;
    }

    // ============================================
    //  UPDATE
    // ============================================

    private void Update()
    {
        CheckCursorInTrigger();
        UpdatePlayerHoldTimer();
        UpdateUIFade();
    }

    // ============================================
    //  КУРСОР
    // ============================================

    private void CheckCursorInTrigger()
    {
        if (mainCam == null || interactionTrigger == null)
        {
            isCursorInTrigger = false;
            return;
        }

        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero);

        isCursorInTrigger = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == interactionTrigger)
            {
                isCursorInTrigger = true;
                break;
            }
        }
    }

    // ============================================
    //  ТАЙМЕР ИГРОКА
    // ============================================

    private void UpdatePlayerHoldTimer()
    {
        if (!isPlayerInInteractionTrigger)
        {
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.deltaTime;
    }

    // ============================================
    //  UI
    // ============================================

    private void UpdateUIFade()
    {
        bool shouldShowUI = isCursorInTrigger || (isPlayerInInteractionTrigger && holdTimer >= holdTimeToShowUI);

        float targetAlpha = shouldShowUI ? 1f : 0f;
        currentUIAlpha = Mathf.MoveTowards(currentUIAlpha, targetAlpha, uiFadeSpeed * Time.deltaTime);

        if (interactionPanel != null)
        {
            interactionPanel.alpha = currentUIAlpha;
            
            bool isActive = currentUIAlpha > 0.1f;
            interactionPanel.blocksRaycasts = isActive;
            interactionPanel.interactable = isActive;
        }
    }

    private void UpdateLockButtonText()
    {
        if (lockButtonText != null)
        {
            lockButtonText.text = IsDoorLocked ? "Unlock" : "Lock";
        }
    }

    // ============================================
    //  ТРИГГЕРЫ
    // ============================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (interactionTrigger != null && interactionTrigger.bounds.Contains(other.transform.position))
        {
            isPlayerInInteractionTrigger = true;
            holdTimer = 0f;
        }

        if (openTrigger != null && openTrigger.bounds.Contains(other.transform.position))
        {
            isPlayerInOpenTrigger = true;
            
            if (!IsDoorLocked && currentState == DoorState.Closed)
            {
                OpenDoor();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (interactionTrigger != null && !interactionTrigger.bounds.Contains(other.transform.position))
        {
            isPlayerInInteractionTrigger = false;
            holdTimer = 0f;
        }

        if (openTrigger != null && !openTrigger.bounds.Contains(other.transform.position))
        {
            isPlayerInOpenTrigger = false;
            
            if (currentState == DoorState.Open)
            {
                CloseDoor();
            }
        }
    }

    // ============================================
    //  УПРАВЛЕНИЕ ДВЕРЬЮ
    // ============================================

    public void OpenDoor()
    {
        if (IsDoorLocked)
        {
            if (debugMode) Debug.Log("[Door] LOCKED - cannot open");
            return;
        }

        if (currentState != DoorState.Closed)
        {
            if (debugMode) Debug.Log($"[Door] Cannot open - current state: {currentState}");
            return;
        }

        currentState = DoorState.Opening;

        if (doorAnimator != null)
        {
            doorAnimator.SetBool(isOpenParam, true);
        }

        if (debugMode) Debug.Log("[Door] Opening...");
    }

    public void CloseDoor()
    {
        if (currentState != DoorState.Open)
        {
            if (debugMode) Debug.Log($"[Door] Cannot close - current state: {currentState}");
            return;
        }

        currentState = DoorState.Closing;

        if (doorAnimator != null)
        {
            doorAnimator.SetBool(isOpenParam, false);
        }

        if (debugMode) Debug.Log("[Door] Closing...");
    }

    public void UnlockDoor()
    {
        if (!IsDoorLocked) return;

        IsDoorLocked = false;
        UpdateLockButtonText();

        if (debugMode) Debug.Log("[Door] UNLOCKED");
    }

    public void LockDoor()
    {
        if (IsDoorLocked) return;

        IsDoorLocked = true;
        UpdateLockButtonText();

        if (currentState == DoorState.Open)
        {
            CloseDoor();
        }

        if (debugMode) Debug.Log("[Door] LOCKED");
    }

    // ============================================
    //  КНОПКИ
    // ============================================

    private void OnOpenButtonClicked()
    {
        if (currentState == DoorState.Closed)
            OpenDoor();
        else if (currentState == DoorState.Open)
            CloseDoor();
    }

    private void OnLockButtonClicked()
    {
        if (IsDoorLocked)
            UnlockDoor();
        else
            LockDoor();
    }

    // ============================================
    //  АНИМАЦИОННЫЕ СОБЫТИЯ
    // ============================================

    public void OnDoorOpened()
    {
        currentState = DoorState.Open;
        if (debugMode) Debug.Log("[Door] State -> OPEN");
    }

    public void OnDoorClosed()
    {
        currentState = DoorState.Closed;
        if (debugMode) Debug.Log("[Door] State -> CLOSED");
    }

    // ============================================
    //  ПУБЛИЧНЫЕ МЕТОДЫ
    // ============================================

    public void ForceUnlockAndOpen()
    {
        IsDoorLocked = false;
        UpdateLockButtonText();
        OpenDoor();
    }

    public static void ResetDoorState()
    {
        IsDoorLocked = true;
    }
}