using UnityEngine;
using System.Collections;

public class BunkerDoor : MonoBehaviour
{
    [Header("Идентификатор для сохранения")]
    [SerializeField] private string doorID = "Door_01"; 

    [Header("Визуал двери")]
    [SerializeField] private SpriteRenderer[] lockedVisuals;   
    [SerializeField] private SpriteRenderer[] unlockedVisuals; 

    [Header("Зоны и коллайдеры")]
    [SerializeField] private Collider2D openTriggerZone; // Триггер открытия
    private Collider2D _mainCollider; // Коллайдер для клика мышкой

    [Header("Две панели UI (с CanvasGroup)")]
    [SerializeField] private CanvasGroup lockedUIPanelGroup;   // Панель для заблокированной двери
    [SerializeField] private CanvasGroup unlockedUIPanelGroup; // Панель для разблокированной двери

    [Header("Настройки анимаций")]
    [SerializeField] private float knockDuration = 0.5f;      
    [SerializeField] private float knockIntensity = 0.1f;     
    [SerializeField] private float openOffset = 2.5f;         
    [SerializeField] private float moveDuration = 0.6f;       
    [SerializeField] private float fadeDuration = 0.2f;       // Скорость Fade

    private Transform _doorTransform;
    private Vector3 _closedPosition;
    private bool _isUnlocked;
    private bool _isOpen;
    private bool _isKnocking;
    private bool _isMoving;
    private Coroutine _fadeCoroutine; 

    private void Awake()
    {
        _doorTransform = transform;
        _closedPosition = _doorTransform.localPosition;
        _mainCollider = GetComponent<Collider2D>();
        
        LoadState();
        UpdateVisuals();
        
        // Изначально скрываем обе панели
        SetPanelState(lockedUIPanelGroup, false);
        SetPanelState(unlockedUIPanelGroup, false);
    }

    private void LoadState()
    {
        _isUnlocked = PlayerPrefs.GetInt($"Door_{doorID}", 0) == 1;
    }

    private void SaveState()
    {
        PlayerPrefs.SetInt($"Door_{doorID}", _isUnlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Вызывается коридором при покупке (включает/выключает кликабельность)
    public void SetInteractionEnabled(bool canInteract)
    {
        if (_mainCollider != null) _mainCollider.enabled = canInteract;
        if (openTriggerZone != null) openTriggerZone.enabled = canInteract;
    }

    private void UpdateVisuals()
    {
        if (lockedVisuals != null)
            foreach (var renderer in lockedVisuals) 
                if (renderer != null) renderer.gameObject.SetActive(!_isUnlocked);

        if (unlockedVisuals != null)
            foreach (var renderer in unlockedVisuals) 
                if (renderer != null) renderer.gameObject.SetActive(_isUnlocked);
    }

    // ------------------- УПРАВЛЕНИЕ UI БЕЗ ОШИБОК -------------------
    private void SetPanelState(CanvasGroup group, bool show)
    {
        if (group == null) return; // Если CanvasGroup нет - просто выходим (скрипт не крашится)
        
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
    }

    // Клик по двери
    private void OnMouseDown()
    {
        if (_isUnlocked) ShowUI(unlockedUIPanelGroup);
        else ShowUI(lockedUIPanelGroup);
    }

    public void ShowUI(CanvasGroup groupToOpen)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        // Сначала скрываем обе панели
        _fadeCoroutine = StartCoroutine(FadeUI(lockedUIPanelGroup, false));
        _fadeCoroutine = StartCoroutine(FadeUI(unlockedUIPanelGroup, false));
        // Затем открываем нужную
        _fadeCoroutine = StartCoroutine(FadeUI(groupToOpen, true));
    }

    public void CloseUI()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeUI(lockedUIPanelGroup, false));
        _fadeCoroutine = StartCoroutine(FadeUI(unlockedUIPanelGroup, false));
    }

    private IEnumerator FadeUI(CanvasGroup group, bool show)
    {
        if (group == null) yield break; // Проверка на null внутри корутины

        float targetAlpha = show ? 1f : 0f;
        float startAlpha = group.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = targetAlpha;
        group.interactable = show;
        group.blocksRaycasts = show;
    }
    // --------------------------------------------------------------

    // Действия с дверью
    public void OnUnlockDoor()
    {
        _isUnlocked = true;
        SaveState();
        UpdateVisuals();
        CloseUI(); 
    }

    public void OnLockDoor()
    {
        _isUnlocked = false;
        SaveState();
        UpdateVisuals();
        CloseUI();

        if (_isOpen) StartCoroutine(MoveDoor(false));
    }

    public void OnKnockDoor()
    {
        if (_isKnocking) return; 
        StartCoroutine(KnockRoutine());
    }

    private IEnumerator KnockRoutine()
    {
        _isKnocking = true;
        float elapsed = 0f;
        Vector3 startPos = _doorTransform.localPosition;

        while (elapsed < knockDuration)
        {
            float offset = Mathf.Sin(elapsed * 60f) * knockIntensity;
            _doorTransform.localPosition = startPos + new Vector3(offset, 0, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _doorTransform.localPosition = startPos;
        _isKnocking = false;
    }

    private IEnumerator MoveDoor(bool open)
    {
        if (_isMoving) yield break; 
        _isMoving = true;
        _isOpen = open;

        Vector3 startPos = _doorTransform.localPosition;
        Vector3 targetPos = open ? _closedPosition + new Vector3(openOffset, 0, 0) : _closedPosition;

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            float t = elapsed / moveDuration;
            _doorTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _doorTransform.localPosition = targetPos;
        _isMoving = false;
    }
}