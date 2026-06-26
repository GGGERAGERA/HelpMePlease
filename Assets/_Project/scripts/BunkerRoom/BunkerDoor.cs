using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BunkerDoor : MonoBehaviour
{
    // Глобальное состояние двери (для теста)
    public static bool IsDoorLocked = true;

    [Header("Основные компоненты")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private Collider2D doorTrigger;
    [SerializeField] private GameObject interactionUI;

    [Header("Зоны подсветки")]
    [SerializeField] private SpriteRenderer[] zoneRenderers;
    [SerializeField] private float maxZoneAlpha = 0.8f;
    [SerializeField] private float minZoneAlpha = 0.1f;
    [SerializeField] private float zoneDetectionRadius = 5f;

    [Header("Части двери")]
    [SerializeField] private GameObject[] lockIndicators;
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material unlockedMaterial;

    [Header("Звуки")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip lockSound;
    [SerializeField] private AudioClip knockSound;
    [SerializeField] private AudioSource audioSource;

    [Header("UI кнопки")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button knockButton;
    [SerializeField] private Button lockToggleButton;
    [SerializeField] private Text lockButtonText;

    [Header("Настройки")]
    [SerializeField] private float holdTimeToShowUI = 2f;
    [SerializeField] private float playerInteractionRadius = 3f;

    private Camera mainCam;
    private bool isDoorOpen;
    private bool isPlayerInRange;
    private bool isCursorOnDoor;
    private float holdTimer;
    private Transform playerTransform;

    private void Awake()
    {
        mainCam = Camera.main;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        SetupUIButtons();
        ApplyLockState();
    }

    private void Start()
    {
        if (interactionUI != null)
            interactionUI.SetActive(false);
    }

    // Подписываем кнопки на методы
    private void SetupUIButtons()
    {
        if (openButton != null)
            openButton.onClick.AddListener(OnOpenButtonClicked);

        if (knockButton != null)
            knockButton.onClick.AddListener(OnKnockButtonClicked);

        if (lockToggleButton != null)
            lockToggleButton.onClick.AddListener(OnLockToggleButtonClicked);
    }

    private void UpdateLockButtonText()
    {
        if (lockButtonText == null) return;
        lockButtonText.text = IsDoorLocked ? "Разблокировать" : "Заблокировать";
    }

    private void Update()
    {
        UpdateZoneOpacity();
        UpdatePlayerHoldTimer();
        UpdateCursorInteraction();
    }

    // Обновляем прозрачность зон в зависимости от расстояния до игрока/курсора
    private void UpdateZoneOpacity()
    {
        if (zoneRenderers == null || zoneRenderers.Length == 0) return;

        float playerIntensity = 0f;
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            playerIntensity = Mathf.Clamp01(1f - (distanceToPlayer / playerInteractionRadius));
        }

        float cursorIntensity = 0f;
        if (mainCam != null)
        {
            Vector2 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            float distanceToCursor = Vector2.Distance(transform.position, mouseWorldPos);
            cursorIntensity = Mathf.Clamp01(1f - (distanceToCursor / zoneDetectionRadius));
        }

        float finalIntensity = Mathf.Max(playerIntensity, cursorIntensity);
        float alpha = Mathf.Lerp(minZoneAlpha, maxZoneAlpha, finalIntensity);

        foreach (SpriteRenderer zone in zoneRenderers)
        {
            if (zone == null) continue;
            Color color = zone.color;
            color.a = alpha;
            zone.color = color;
        }
    }

    // Отслеживаем время нахождения игрока в триггере
    private void UpdatePlayerHoldTimer()
    {
        if (!isPlayerInRange)
        {
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.deltaTime;

        if (holdTimer >= holdTimeToShowUI && interactionUI != null && !interactionUI.activeSelf)
        {
            ShowInteractionUI();
        }
    }

    // Проверяем наведение курсора на дверь
    private void UpdateCursorInteraction()
    {
        if (mainCam == null) return;

        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        bool cursorOnDoor = hit.collider != null && hit.transform == transform;

        if (cursorOnDoor && !isCursorOnDoor)
        {
            isCursorOnDoor = true;
        }
        else if (!cursorOnDoor && isCursorOnDoor)
        {
            isCursorOnDoor = false;
            if (!isPlayerInRange && interactionUI != null && interactionUI.activeSelf)
            {
                HideInteractionUI();
            }
        }

        // ЛКМ по двери - показываем UI
        if (cursorOnDoor && Input.GetMouseButtonDown(0))
        {
            ShowInteractionUI();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            holdTimer = 0f;

            if (!IsDoorLocked)
            {
                OpenDoor();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            holdTimer = 0f;

            if (!isCursorOnDoor && interactionUI != null)
            {
                HideInteractionUI();
            }

            if (isDoorOpen)
            {
                CloseDoor();
            }
        }
    }

    public void OpenDoor()
    {
        if (IsDoorLocked)
        {
            Debug.Log("Дверь заблокирована!");
            return;
        }

        if (isDoorOpen) return;

        isDoorOpen = true;

        if (doorAnimator != null)
            doorAnimator.SetBool("IsOpen", true);

        PlaySound(openSound);
    }

    public void CloseDoor()
    {
        if (!isDoorOpen) return;

        isDoorOpen = false;

        if (doorAnimator != null)
            doorAnimator.SetBool("IsOpen", false);

        PlaySound(closeSound);
    }

    public void UnlockDoor()
    {
        if (!IsDoorLocked) return;

        IsDoorLocked = false;
        PlaySound(unlockSound);
        ApplyLockState();
    }

    public void LockDoor()
    {
        if (IsDoorLocked) return;

        IsDoorLocked = true;
        PlaySound(lockSound);

        if (isDoorOpen)
            CloseDoor();

        ApplyLockState();
    }

    private void OnLockToggleButtonClicked()
    {
        if (IsDoorLocked)
            UnlockDoor();
        else
            LockDoor();

        UpdateLockButtonText();
    }

    // Меняем материалы и вид двери в зависимости от состояния
    private void ApplyLockState()
    {
        if (lockIndicators != null)
        {
            foreach (GameObject indicator in lockIndicators)
            {
                if (indicator == null) continue;

                SpriteRenderer sr = indicator.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.material = IsDoorLocked ? lockedMaterial : unlockedMaterial;
                }
            }
        }

        UpdateLockButtonText();
    }

    private void OnOpenButtonClicked()
    {
        OpenDoor();
        HideInteractionUI();
    }

    private void OnKnockButtonClicked()
    {
        PlaySound(knockSound);

        if (doorAnimator != null)
            doorAnimator.SetTrigger("Knock");
    }

    public void ShowInteractionUI()
    {
        if (interactionUI == null) return;

        interactionUI.SetActive(true);
        UpdateLockButtonText();
    }

    public void HideInteractionUI()
    {
        if (interactionUI == null) return;
        interactionUI.SetActive(false);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    public bool IsOpen() => isDoorOpen;
    public bool IsLocked() => IsDoorLocked;

    public static void ResetDoorState()
    {
        IsDoorLocked = true;
    }
}