using UnityEngine;

/// <summary>
/// Управление анимациями игрока: idle и walk
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Header("Настройки")]
    [Tooltip("Ссылка на ScriptableObject с параметрами игрока")]
    public PlayerStatsSO playerStats;

    [Tooltip("Аниматор игрока")]
    [SerializeField] private Animator animator;

    [Tooltip("Порог скорости для перехода в ходьбу")]
    [SerializeField] private float walkThreshold = 0.1f;

    private PlayerMovement playerMovement;

    private void Awake()
    {
        if (spriteRenderer == null)
{
    spriteRenderer = GetComponent<SpriteRenderer>();
    if (spriteRenderer == null)
    {
        Debug.LogWarning("SpriteRenderer не найден. Поворот через Flip не будет работать.");
    }
}
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator не найден!");
                return;
            }
        }

        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogWarning("PlayerMovement не найден. Анимации могут работать некорректно.");
        }
    }

    private void Update()
    {
        
        if (animator == null) return;

        // Получаем направление движения
        Vector2 movementDirection = Vector2.zero;
        if (playerMovement != null)
        {
            movementDirection = playerMovement.GetMovementDirection();
        }

        // Вычисляем скорость
        float speed = movementDirection.magnitude;

        // Передаем в аниматор
        animator.SetFloat("Speed", speed);

        // Опционально: поворачиваем спрайт в сторону движения
if (speed > walkThreshold)
{
    // Если движемся влево — флипаем по X
    if (movementDirection.x < 0)
    {
        spriteRenderer.flipX = true;
    }
    else if (movementDirection.x > 0)
    {
        spriteRenderer.flipX = false;
    }
    // Не меняем Y — если нужно, можно добавить
}
    }
}