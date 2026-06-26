using UnityEngine;

public class HoverOutlineRaycast : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Автоматически находить все SpriteRenderer'ы на объекте и дочерних")]
    [SerializeField] private bool autoFindRenderers = true;
    
    [Tooltip("Рендереры для обводки (если autoFindRenderers выключен)")]
    [SerializeField] private SpriteRenderer[] targetRenderers;
    
    [Header("Raycast Settings")]
    [Tooltip("Слои для проверки (можно исключить пол и прочее)")]
    [SerializeField] private LayerMask raycastLayerMask = ~0;

    private Camera mainCam;
    private Material[] outlineMaterials;
    private bool isHovered;

    private void Awake()
    {
        mainCam = Camera.main;

        // Автоматически ищем все SpriteRenderer'ы
        if (autoFindRenderers && (targetRenderers == null || targetRenderers.Length == 0))
        {
            targetRenderers = GetComponentsInChildren<SpriteRenderer>();
        }

        // Создаём копии материалов для каждого рендерера
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            outlineMaterials = new Material[targetRenderers.Length];
            
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    // .material создаёт копию, чтобы не влиять на другие объекты
                    outlineMaterials[i] = targetRenderers[i].material;
                }
            }
        }

        HideOutline();
    }

    private void Update()
    {
        if (mainCam == null || outlineMaterials == null || outlineMaterials.Length == 0)
            return;

        // Получаем позицию мыши в мировых координатах
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        
        // Проверяем ВСЕ коллайдеры под курсором
        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero, 0f, raycastLayerMask);

        // Ищем наш объект среди всех попаданий
        bool isHitOnThisObject = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform == transform)
            {
                isHitOnThisObject = true;
                break;
            }
        }

        // Если состояние изменилось - обновляем обводку
        if (isHitOnThisObject != isHovered)
        {
            isHovered = isHitOnThisObject;

            if (isHovered)
                ShowOutline();
            else
                HideOutline();
        }
    }

    // Включаем обводку на всех материалах
    private void ShowOutline()
    {
        for (int i = 0; i < outlineMaterials.Length; i++)
        {
            if (outlineMaterials[i] != null)
            {
                outlineMaterials[i].EnableKeyword("_OUTLINE_ON");
            }
        }
    }

    // Выключаем обводку на всех материалах
    private void HideOutline()
    {
        for (int i = 0; i < outlineMaterials.Length; i++)
        {
            if (outlineMaterials[i] != null)
            {
                outlineMaterials[i].DisableKeyword("_OUTLINE_ON");
            }
        }
    }

    // Публичный метод для внешнего управления обводкой
    public void SetOutline(bool enabled)
    {
        if (enabled)
            ShowOutline();
        else
            HideOutline();
    }

    private void OnDestroy()
    {
        // Очищаем копии материалов чтобы избежать утечек памяти
        if (outlineMaterials != null)
        {
            for (int i = 0; i < outlineMaterials.Length; i++)
            {
                if (outlineMaterials[i] != null)
                    Destroy(outlineMaterials[i]);
            }
        }
    }
}