using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BunkerDoor : MonoBehaviour
{
    [Header("Состояние")]
    [SerializeField] private bool isLocked = true;

    [Header("Зоны")]
    [SerializeField] private Collider2D showUIZone;
    [SerializeField] private Collider2D openZone;

    [Header("Движущиеся части двери")]
    [Tooltip("Сюда закинь все двигающиеся части: DoorsUpP1, DoorsLowP1 и т.д.")]
    [SerializeField] private Transform[] doorParts;
    
    [Tooltip("Целевые позиции для каждой части при открытии. Создай пустые GameObject'ы и размести их там, куда должна приехать часть двери. Длина должна совпадать с doorParts")]
    [SerializeField] private Transform[] openPositions;
    
    [Tooltip("Длительность анимации открытия/закрытия")]
    [SerializeField] private float openDuration = 0.6f;
    
    [Tooltip("Кривая плавности. По умолчанию EaseInOut")]
    [SerializeField] private AnimationCurve openCurve;

    [Header("UI")]
    [SerializeField] private CanvasGroup uiPanel;
    [SerializeField] private Button unlockButton;
    [SerializeField] private Button lockButton;
    [SerializeField] private Button knockButton;

    [Header("Визуальные индикаторы")]
    [SerializeField] private GameObject[] lockedVisuals;
    [SerializeField] private GameObject[] unlockedVisuals;

    [Header("Эффекты")]
    [SerializeField] private GameObject openEffect;
    [SerializeField] private GameObject closeEffect;
    [SerializeField] private Transform effectSpawnPoint;

    [Header("Настройки UI")]
    [SerializeField] private float uiShowDelay = 1f;
    [SerializeField] private float uiFadeSpeed = 5f;

    private Transform playerTransform;
    private bool isOpen;
    private bool isPlayerInShowUIZone;
    private bool isPlayerInOpenZone;
    private float hoverTimer;
    private Coroutine doorCoroutine;

    private Vector3[] closedPositions;
    private Vector3[] targetOpenPositions;

    private void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        if (unlockButton != null)
            unlockButton.onClick.AddListener(OnUnlockClicked);

        if (lockButton != null)
        {
            lockButton.onClick.AddListener(OnLockClicked);
            lockButton.gameObject.SetActive(false);
        }

        if (knockButton != null)
            knockButton.onClick.AddListener(OnKnockClicked);

        if (uiPanel != null)
        {
            uiPanel.alpha = 0f;
            uiPanel.blocksRaycasts = false;
            uiPanel.interactable = false;
        }

        if (openCurve == null || openCurve.length == 0)
            openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        ValidateArrays();
        CalculateDoorPositions();
        ApplyLockVisuals();
    }

    private void ValidateArrays()
    {
        if (doorParts == null || doorParts.Length == 0)
        {
            Debug.LogWarning("[BunkerDoor] doorParts массив пуст!");
            return;
        }

        int partCount = doorParts.Length;

        if (openPositions == null || openPositions.Length != partCount)
        {
            Debug.LogWarning($"[BunkerDoor] openPositions должен иметь {partCount} элементов. Создан автоматически.");
            openPositions = new Transform[partCount];
            for (int i = 0; i < partCount; i++)
                openPositions[i] = doorParts[i]; // По умолчанию — остаться на месте
        }
    }

    private void CalculateDoorPositions()
    {
        if (doorParts == null || doorParts.Length == 0) return;

        closedPositions = new Vector3[doorParts.Length];
        targetOpenPositions = new Vector3[doorParts.Length];

        for (int i = 0; i < doorParts.Length; i++)
        {
            if (doorParts[i] != null)
            {
                closedPositions[i] = doorParts[i].localPosition;
                
                if (openPositions[i] != null)
                    targetOpenPositions[i] = openPositions[i].localPosition;
                else
                    targetOpenPositions[i] = closedPositions[i];
            }
        }
    }

    private void Update()
    {
        CheckPlayerInZones();
        UpdateHoverTimer();
        UpdateUIFade();
        CheckAutoOpenClose();
    }

    private void CheckPlayerInZones()
    {
        if (playerTransform == null) return;

        if (showUIZone != null)
        {
            bool inZone = showUIZone.bounds.Contains(playerTransform.position);

            if (inZone && !isPlayerInShowUIZone)
            {
                isPlayerInShowUIZone = true;
                hoverTimer = 0f;
            }
            else if (!inZone && isPlayerInShowUIZone)
            {
                isPlayerInShowUIZone = false;
                hoverTimer = 0f;
            }
        }

        if (openZone != null)
        {
            bool inZone = openZone.bounds.Contains(playerTransform.position);

            if (inZone && !isPlayerInOpenZone)
                isPlayerInOpenZone = true;
            else if (!inZone && isPlayerInOpenZone)
                isPlayerInOpenZone = false;
        }
    }

    private void UpdateHoverTimer()
    {
        if (isPlayerInShowUIZone)
            hoverTimer += Time.deltaTime;
        else
            hoverTimer = 0f;
    }

    private void UpdateUIFade()
    {
        if (uiPanel == null) return;

        bool shouldShow = isPlayerInShowUIZone && hoverTimer >= uiShowDelay;
        float targetAlpha = shouldShow ? 1f : 0f;
        float currentAlpha = Mathf.MoveTowards(uiPanel.alpha, targetAlpha, uiFadeSpeed * Time.deltaTime);

        uiPanel.alpha = currentAlpha;
        uiPanel.blocksRaycasts = currentAlpha > 0.1f;
        uiPanel.interactable = currentAlpha > 0.1f;
    }

    private void CheckAutoOpenClose()
    {
        if (isLocked) return;

        if (isPlayerInOpenZone && !isOpen)
            OpenDoor();
        else if (!isPlayerInOpenZone && isOpen)
            CloseDoor();
    }

    private void OnUnlockClicked()
    {
        isLocked = false;
        ApplyLockVisuals();

        if (unlockButton != null) unlockButton.gameObject.SetActive(false);
        if (lockButton != null) lockButton.gameObject.SetActive(true);
    }

    private void OnLockClicked()
    {
        isLocked = true;
        ApplyLockVisuals();

        if (lockButton != null) lockButton.gameObject.SetActive(false);
        if (unlockButton != null) unlockButton.gameObject.SetActive(true);

        if (isOpen) CloseDoor();
    }

    private void OnKnockClicked()
    {
        if (doorCoroutine != null) StopCoroutine(doorCoroutine);
        doorCoroutine = StartCoroutine(KnockAnimation());
    }

    public void OpenDoor()
    {
        if (isOpen || isLocked) return;
        if (doorCoroutine != null) StopCoroutine(doorCoroutine);
        doorCoroutine = StartCoroutine(AnimateDoor(true));
    }

    public void CloseDoor()
    {
        if (!isOpen) return;
        if (doorCoroutine != null) StopCoroutine(doorCoroutine);
        doorCoroutine = StartCoroutine(AnimateDoor(false));
    }

    private IEnumerator AnimateDoor(bool opening)
    {
        isOpen = opening;

        float elapsed = 0f;
        Vector3[] startPositions = new Vector3[doorParts.Length];
        Vector3[] endPositions = opening ? targetOpenPositions : closedPositions;

        for (int i = 0; i < doorParts.Length; i++)
        {
            if (doorParts[i] != null)
                startPositions[i] = doorParts[i].localPosition;
        }

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openDuration);
            float curveT = openCurve.Evaluate(t);

            for (int i = 0; i < doorParts.Length; i++)
            {
                if (doorParts[i] != null)
                {
                    doorParts[i].localPosition = Vector3.Lerp(startPositions[i], endPositions[i], curveT);
                }
            }

            yield return null;
        }

        for (int i = 0; i < doorParts.Length; i++)
        {
            if (doorParts[i] != null)
                doorParts[i].localPosition = endPositions[i];
        }

        SpawnEffect(opening ? openEffect : closeEffect);
        doorCoroutine = null;
    }

    private IEnumerator KnockAnimation()
    {
        float duration = 0.35f;
        float elapsed = 0f;
        float knockAmount = 0.15f;

        Vector3[] originalPositions = new Vector3[doorParts.Length];
        Vector2[] knockDirections = new Vector2[doorParts.Length];
        
        for (int i = 0; i < doorParts.Length; i++)
        {
            if (doorParts[i] != null)
            {
                originalPositions[i] = doorParts[i].localPosition;
                
                // Направление стука — от целевой позиции к закрытой
                Vector2 dir = (closedPositions[i] - targetOpenPositions[i]).normalized;
                knockDirections[i] = dir.magnitude > 0.01f ? dir : Vector2.up;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float shake = Mathf.Sin(t * Mathf.PI * 6) * knockAmount * (1f - t);

            for (int i = 0; i < doorParts.Length; i++)
            {
                if (doorParts[i] != null)
                {
                    doorParts[i].localPosition = originalPositions[i] + (Vector3)(knockDirections[i] * shake);
                }
            }

            yield return null;
        }

        for (int i = 0; i < doorParts.Length; i++)
        {
            if (doorParts[i] != null)
                doorParts[i].localPosition = originalPositions[i];
        }

        doorCoroutine = null;
    }

    private void SpawnEffect(GameObject effectPrefab)
    {
        if (effectPrefab == null) return;

        Vector3 spawnPos = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;
        Instantiate(effectPrefab, spawnPos, Quaternion.identity);
    }

    private void ApplyLockVisuals()
    {
        if (lockedVisuals != null)
        {
            for (int i = 0; i < lockedVisuals.Length; i++)
            {
                if (lockedVisuals[i] != null)
                    lockedVisuals[i].SetActive(isLocked);
            }
        }

        if (unlockedVisuals != null)
        {
            for (int i = 0; i < unlockedVisuals.Length; i++)
            {
                if (unlockedVisuals[i] != null)
                    unlockedVisuals[i].SetActive(!isLocked);
            }
        }
    }

    public bool IsOpen() => isOpen;
    public bool IsLocked() => isLocked;
}