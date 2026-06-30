using UnityEngine;
using UnityEngine.UI;

public class InteractableBunkerObject : MonoBehaviour
{
    [Header("Основные настройки")]
    [SerializeField] private CanvasGroup interactionUI; // Панель, которая вылазит при клике
    [SerializeField] private bool canBeSold = false;
    [SerializeField] private bool canBeMoved = false;
    [SerializeField] private int sellPrice = 0;
    [SerializeField] private int buyCost = 0; // Цена для покупки из магазина

    [Header("Визуал для перемещения")]
    [SerializeField] private Color validPlacementColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidPlacementColor = new Color(1f, 0f, 0f, 0.5f);

    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private Collider2D myCollider;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) originalColors[i] = renderers[i].color;
        }
        myCollider = GetComponent<Collider2D>();

        if (interactionUI != null)
        {
            interactionUI.alpha = 0f;
            interactionUI.blocksRaycasts = false;
            interactionUI.interactable = false;
            interactionUI.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !BunkerPlacementSystem.Instance.IsPlacing)
        {
            if (IsMouseOverThisObject())
            {
                OpenInteractionUI();
            }
        }
    }

    private bool IsMouseOverThisObject()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        return hit.collider != null && hit.collider.transform == transform;
    }

    public void OpenInteractionUI()
    {
        if (interactionUI != null)
        {
            interactionUI.gameObject.SetActive(true);
            BunkerUIManager.Instance.OpenUI(interactionUI);
        }
    }

    public void StartMoveMode()
    {
        if (!canBeMoved) return;
        BunkerUIManager.Instance.CloseUI(interactionUI);
        BunkerPlacementSystem.Instance.StartMovingExistingObject(this, validPlacementColor, invalidPlacementColor);
    }

    public void RequestSell()
    {
        if (!canBeSold) return;
        // Здесь можно вызвать UI подтверждения, но для старта сразу продаем
        ExecuteSell();
    }

    public void ExecuteSell()
    {
        CurrencyManager.Instance.AddGold(sellPrice);
        // BunkerSaveSystem.Instance.MarkAsSold(gameObject.name); // Сохраняем факт продажи
        Destroy(gameObject);
    }

    public void SetPlacementColor(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                Color c = originalColors[i];
                c = Color.Lerp(c, color, 0.5f);
                renderers[i].color = c;
            }
        }
    }

    public void ResetColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].color = originalColors[i];
        }
    }

    public Collider2D GetCollider() => myCollider;
    public int GetBuyCost() => buyCost;
}