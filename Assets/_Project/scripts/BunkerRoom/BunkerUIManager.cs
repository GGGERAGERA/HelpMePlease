using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class BunkerUIManager : MonoBehaviour
{
    public static BunkerUIManager Instance;

    [Header("Ссылки")]
    [SerializeField] private Camera mainCam;

    private Stack<CanvasGroup> uiStack = new Stack<CanvasGroup>();
    private GraphicRaycaster[] raycasters;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        mainCam = mainCam != null ? mainCam : Camera.main;
        raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        HandleEscapeInput();
        HandleClickOutsideUI();
    }

    private void HandleEscapeInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (BunkerPlacementSystem.Instance != null && BunkerPlacementSystem.Instance.IsPlacing)
        {
            BunkerPlacementSystem.Instance.CancelPlacement();
        }
        else if (uiStack.Count > 0)
        {
            CloseTopUI();
        }
    }

    private void HandleClickOutsideUI()
    {
        if (!Input.GetMouseButtonDown(0) || uiStack.Count == 0) return;

        if (!IsPointerOverUI())
        {
            CloseTopUI();
        }
    }

    public void OpenUI(CanvasGroup panel)
    {
        if (panel == null) return;

        if (uiStack.Contains(panel))
        {
            panel.alpha = 1f;
            panel.blocksRaycasts = true;
            panel.interactable = true;
            return;
        }

        panel.gameObject.SetActive(true);
        StartCoroutine(FadeCanvasGroup(panel, 1f, 0.2f));
        uiStack.Push(panel);
    }

    public void CloseUI(CanvasGroup panel)
    {
        if (panel == null) return;

        StartCoroutine(FadeCanvasGroup(panel, 0f, 0.2f, () => {
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }));

        var tempStack = new Stack<CanvasGroup>();
        while (uiStack.Count > 0)
        {
            var p = uiStack.Pop();
            if (p != panel) tempStack.Push(p);
        }
        while (tempStack.Count > 0) uiStack.Push(tempStack.Pop());
    }

    private void CloseTopUI()
    {
        if (uiStack.Count > 0) CloseUI(uiStack.Pop());
    }

    private bool IsPointerOverUI()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        foreach (var raycaster in raycasters)
        {
            results.Clear();
            raycaster.Raycast(pointerData, results);
            if (results.Count > 0) return true;
        }
        return false;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float target, float duration, System.Action onComplete = null)
    {
        float start = cg.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, time / duration);
            yield return null;
        }
        cg.alpha = target;
        onComplete?.Invoke();
    }
}