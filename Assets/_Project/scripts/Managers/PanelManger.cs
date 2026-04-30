using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PanelManagerSimple : MonoBehaviour
{
    // 💡 Просто перетащи сюда ВСЕ панели из этой сцены — сколько угодно!
    [SerializeField] private List<GameObject> panels = new List<GameObject>();
    //[SerializeField] private List<GameObject> btns = new List<GameObject>();

    private List<CanvasGroup> canvasGroups = new List<CanvasGroup>();
    //private List<CanvasGroup> canvasBtnGroups = new List<CanvasGroup>();

    void Awake()
    {
        // Кэшируем CanvasGroup для всех панелей из списка
        foreach (var panel in panels)
        {
            panel.SetActive(true);
            if (panel == null) continue;

            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();

            canvasGroups.Add(cg);
            HidePanel(cg);
        }
    }

    private CanvasGroup GetCanvasGroup(GameObject panel)
    {
        if (panel == null) return null;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        return cg;
    }

    /// Показать одну панель, остальные скрыть
    public void ShowPanel(GameObject panelToShow)
    {
        var targetCG = GetCanvasGroup(panelToShow);
        if (targetCG == null) return;

        // Скрываем все панели
        foreach (var cg in canvasGroups)
        {
            if (cg != null) HidePanel(cg);
        }

        // Показываем нужную
        targetCG.alpha = 1f;
        targetCG.interactable = true;
        targetCG.blocksRaycasts = true;
    }

    /// Скрыть конкретную панель
    public void HidePanel(GameObject panelToHide)
    {
        var cg = GetCanvasGroup(panelToHide);
        if (cg != null) HidePanel(cg);
    }

    private void HidePanel(CanvasGroup cg)
    {
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }
}